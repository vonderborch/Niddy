using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using Niddy.Core.IO;
using Niddy.Core.IO.Archives;

namespace Niddy.Core.Tests;

[Collection(PlatformStateCollection.Name)]
public sealed class ArchiveTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-archive-").FullName;

    public void Dispose() => Directory.DeleteWithRetry(_dir, baseDelayMs: 0, throwOnFailure: false);

    private string PathOf(string relativePath) => Path.Combine(_dir, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private string CreateFile(string relativePath, string content = "x")
    {
        string path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A sample folder: nested files, an empty folder and a dot-file.</summary>
    private string CreateSource()
    {
        CreateFile("src/a.txt", "alpha");
        CreateFile("src/sub/b.txt", "bravo");
        CreateFile("src/.hidden", "secret");
        Directory.CreateDirectory(PathOf("src/empty"));
        return PathOf("src");
    }

    /// <summary>Every file and folder under a directory, as sorted relative paths with <c>/</c>, folders ending in <c>/</c>.</summary>
    private static string[] Tree(string directory) =>
        Directory.EnumerateFileSystemEntries(directory, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
            .Select(path => Path.GetRelativePath(directory, path).Replace(Path.DirectorySeparatorChar, '/') + (Directory.Exists(path) ? "/" : ""))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string Extension(ArchiveFormat format) => format switch
    {
        ArchiveFormat.Zip => ".zip",
        ArchiveFormat.Tar => ".tar",
        _ => ".tar.gz",
    };

    private sealed class SyncProgress : IProgress<ArchiveProgress>
    {
        public List<ArchiveProgress> Reports { get; } = [];

        public void Report(ArchiveProgress value) => Reports.Add(value);
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }

    private string CreateZip(string name, params (string Name, string Content)[] entries)
    {
        string path = PathOf(name);
        using ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach ((string entryName, string content) in entries)
        {
            using StreamWriter writer = new(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }

        return path;
    }

    private string CreateTar(string name, params TarEntry[] entries)
    {
        string path = PathOf(name);
        using FileStream stream = File.Create(path);
        using TarWriter writer = new(stream, TarEntryFormat.Pax);
        foreach (TarEntry entry in entries)
        {
            writer.WriteEntry(entry);
        }

        return path;
    }

    private static PaxTarEntry TarFile(string name, string content) =>
        new(TarEntryType.RegularFile, name) { DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)) };

    // ---- Round trips ----

    public static TheoryData<ArchiveFormat, bool> FormatsAndModes()
    {
        TheoryData<ArchiveFormat, bool> data = new();
        foreach (ArchiveFormat format in Enum.GetValues<ArchiveFormat>())
        {
            data.Add(format, false);
            data.Add(format, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(FormatsAndModes))]
    public async Task Folder_RoundTripsThroughAPath(ArchiveFormat format, bool useAsync)
    {
        string source = CreateSource();
        DateTime written = new(2021, 5, 6, 7, 8, 10, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(source, "a.txt"), written);
        string archive = PathOf("out/archive" + Extension(format));
        string destination = PathOf("extracted");

        if (useAsync)
        {
            await Archive.CreateAsync(source, archive, cancellationToken: TestContext.Current.CancellationToken);
            await Archive.ExtractAsync(archive, destination, cancellationToken: TestContext.Current.CancellationToken);
        }
        else
        {
            Archive.Create(source, archive);
            Archive.Extract(archive, destination);
        }

        Assert.Equal([".hidden", "a.txt", "empty/", "sub/", "sub/b.txt"], Tree(destination));
        Assert.Equal("alpha", File.ReadAllText(Path.Combine(destination, "a.txt")));
        Assert.Equal("bravo", File.ReadAllText(Path.Combine(destination, "sub", "b.txt")));
        // Zip stores times to 2 seconds.
        Assert.Equal(written, File.GetLastWriteTimeUtc(Path.Combine(destination, "a.txt")));
    }

    [Theory]
    [MemberData(nameof(FormatsAndModes))]
    public async Task Folder_RoundTripsThroughAStream(ArchiveFormat format, bool useAsync)
    {
        string source = CreateSource();
        using MemoryStream archive = new();
        string destination = PathOf("extracted");
        ArchiveCreateOptions options = new() { Format = format, IncludeBaseDirectory = true };

        if (useAsync)
        {
            await Archive.CreateAsync(source, archive, options, TestContext.Current.CancellationToken);
            archive.Position = 0;
            await Archive.ExtractAsync(archive, destination, cancellationToken: TestContext.Current.CancellationToken);
        }
        else
        {
            Archive.Create(source, archive, options);
            archive.Position = 0;
            Archive.Extract(archive, destination);   // format sniffed
        }

        Assert.True(archive.CanRead, "the stream is left open");
        Assert.Equal(["src/", "src/.hidden", "src/a.txt", "src/empty/", "src/sub/", "src/sub/b.txt"], Tree(destination));
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.TarGz)]
    public void SingleFile_IsOneEntryNamedAfterIt(ArchiveFormat format)
    {
        string file = CreateFile("notes.txt", "hi");
        string archive = PathOf("notes" + Extension(format));

        Archive.Create(file, archive, new ArchiveCreateOptions { IncludeBaseDirectory = true });

        ArchiveEntryInfo entry = Assert.Single(Archive.GetEntries(archive));
        Assert.Equal(new ArchiveEntryInfo("notes.txt", 2, entry.LastModified, false), entry);
        Assert.Equal("hi", Archive.ReadEntryText(archive, "notes.txt"));
    }

    [Fact]
    public void Extract_KeepsUnixPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix permissions");
            return;
        }

        string source = CreateSource();
        File.SetUnixFileMode(Path.Combine(source, "a.txt"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        foreach (ArchiveFormat format in Enum.GetValues<ArchiveFormat>())
        {
            string archive = PathOf("p" + Extension(format));
            Archive.Create(source, archive);
            Archive.Extract(archive, PathOf("out-" + format));
            Assert.True(File.GetUnixFileMode(PathOf($"out-{format}/a.txt")).HasFlag(UnixFileMode.UserExecute), format.ToString());
        }
    }

    // ---- Formats ----

    [Theory]
    [InlineData("a.zip", ArchiveFormat.Zip)]
    [InlineData("A.ZIP", ArchiveFormat.Zip)]
    [InlineData("dir/a.tar", ArchiveFormat.Tar)]
    [InlineData("a.tar.gz", ArchiveFormat.TarGz)]
    [InlineData("a.TGZ", ArchiveFormat.TarGz)]
    [InlineData("a.gz", null)]
    [InlineData("a.zip.bak", null)]
    [InlineData("zip", null)]
    public void FormatFromPath_ReadsTheExtension(string path, ArchiveFormat? expected) =>
        Assert.Equal(expected, Archive.FormatFromPath(path));

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.TarGz)]
    public void Reading_SniffsTheFormatWhenTheExtensionDoesNotSayIt(ArchiveFormat format)
    {
        CreateFile("src/a.txt", "alpha");
        string archive = PathOf("archive.bin");
        Archive.Create(PathOf("src"), archive, new ArchiveCreateOptions { Format = format });

        Assert.Equal("alpha", Archive.ReadEntryText(archive, "a.txt"));
        using FileStream stream = File.OpenRead(archive);
        Assert.True(Archive.ContainsEntry(stream, "a.txt"));
    }

    [Fact]
    public void Reading_UnknownBytes_IsInvalidData()
    {
        string path = CreateFile("junk.bin", new string('j', 1000));

        Assert.Throws<InvalidDataException>(() => Archive.GetEntries(path));
    }

    [Fact]
    public void Reading_NonSeekableStreamWithoutAFormat_Throws()
    {
        CreateFile("src/a.txt", "alpha");
        using MemoryStream archive = new();
        Archive.Create(PathOf("src"), archive, new ArchiveCreateOptions { Format = ArchiveFormat.TarGz });
        archive.Position = 0;

        Assert.Throws<ArgumentException>(() => Archive.GetEntries(new NonSeekableStream(archive)));
        Assert.Equal("alpha", Archive.ReadEntryText(new NonSeekableStream(archive), "a.txt", format: ArchiveFormat.TarGz));
    }

    [Fact]
    public void Create_ToAStreamWithoutAFormat_Throws()
    {
        string source = CreateSource();

        Assert.Throws<ArgumentException>(() => Archive.Create(source, new MemoryStream(), new ArchiveCreateOptions()));
    }

    [Fact]
    public void Create_ToAPathWithAnUnknownExtension_Throws()
    {
        string source = CreateSource();

        Assert.Throws<ArgumentException>(() => Archive.Create(source, PathOf("archive.bin")));
        Assert.False(File.Exists(PathOf("archive.bin")));
    }

    // ---- Create rules ----

    [Fact]
    public void Create_Overwrite()
    {
        string source = CreateSource();
        string archive = CreateFile("archive.zip", "old");

        Assert.Throws<IOException>(() => Archive.Create(source, archive));
        Assert.Equal("old", File.ReadAllText(archive));

        Archive.Create(source, archive, new ArchiveCreateOptions { Overwrite = true });
        Assert.Equal("alpha", Archive.ReadEntryText(archive, "a.txt"));
    }

    [Fact]
    public void Create_IntoTheSource_Throws()
    {
        string source = CreateSource();

        Assert.Throws<ArgumentException>(() => Archive.Create(source, Path.Combine(source, "self.zip")));
        Assert.False(File.Exists(Path.Combine(source, "self.zip")));
    }

    [Fact]
    public void Create_MissingSource_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => Archive.Create(PathOf("missing"), PathOf("a.zip")));
        Assert.False(File.Exists(PathOf("a.zip")));
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.TarGz)]
    public void Create_Exclusions(ArchiveFormat format)
    {
        CreateFile("src/a.txt");
        CreateFile("src/bin/out.dll");
        CreateFile("src/lib/bin/deep.dll");
        CreateFile("src/lib/keep.txt");
        CreateFile("src/.DS_Store");
        CreateFile("src/lib/.DS_Store");
        CreateFile("src/docs/drafts/x.md");
        CreateFile("src/docs/final.md");
        string archive = PathOf("out" + Extension(format));

        Archive.Create(PathOf("src"), archive, new ArchiveCreateOptions
        {
            ExcludedDirectories = ["bin", "docs/drafts"],
            ExcludedFiles = [".DS_Store"],
        });

        Assert.Equal(
            ["a.txt", "docs", "docs/final.md", "lib", "lib/keep.txt"],
            Archive.GetEntries(archive).Select(entry => entry.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Create_SkipsLinkedFolders()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating links needs privileges on Windows");
        string source = CreateSource();
        Directory.CreateSymbolicLink(Path.Combine(source, "loop"), source);
        string archive = PathOf("a.zip");

        Archive.Create(source, archive);

        Assert.DoesNotContain(Archive.GetEntries(archive), entry => entry.Name.StartsWith("loop", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_Cancelled_LeavesNoArchive()
    {
        string source = CreateSource();
        using CancellationTokenSource cancelled = new();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Archive.CreateAsync(source, PathOf("a.tar.gz"), cancellationToken: cancelled.Token));

        Assert.Equal(["src/", "src/.hidden", "src/a.txt", "src/empty/", "src/sub/", "src/sub/b.txt"], Tree(_dir));
    }

    // ---- Extract ----

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.Tar)]
    public void Extract_Filter_TakesOnlyMatchingEntries(ArchiveFormat format)
    {
        CreateFile("src/a.json", "{}");
        CreateFile("src/sub/b.json", "[]");
        CreateFile("src/sub/c.txt");
        string archive = PathOf("a" + Extension(format));
        Archive.Create(PathOf("src"), archive);

        Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions
        {
            Filter = entry => entry.Name.EndsWith(".json", StringComparison.Ordinal),
        });

        Assert.Equal(["a.json", "sub/", "sub/b.json"], Tree(PathOf("out")));
    }

    [Fact]
    public void Extract_ExistingFiles_NeedOverwrite()
    {
        string source = CreateSource();
        string archive = PathOf("a.zip");
        Archive.Create(source, archive);
        CreateFile("out/a.txt", "mine");

        Assert.Throws<IOException>(() => Archive.Extract(archive, PathOf("out")));
        Assert.Equal("mine", File.ReadAllText(PathOf("out/a.txt")));

        Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions { Overwrite = true });
        Assert.Equal("alpha", File.ReadAllText(PathOf("out/a.txt")));
    }

    [Fact]
    public void Extract_CorruptArchiveIntoANewFolder_LeavesNothing()
    {
        string source = CreateSource();
        CreateFile("src/big.txt", new string('z', 100_000));
        string archive = PathOf("a.tar.gz");
        Archive.Create(source, archive);
        byte[] bytes = File.ReadAllBytes(archive);
        File.WriteAllBytes(archive, bytes[..(bytes.Length / 2)]);

        Assert.ThrowsAny<Exception>(() => Archive.Extract(archive, PathOf("out/nested")));

        Assert.Empty(Directory.GetFileSystemEntries(PathOf("out")));
    }

    // ---- Security ----

    public static TheoryData<string> UnsafeNames() => new("../evil.txt", "sub/../../evil.txt", "/abs-evil.txt", "..\\evil.txt");

    [Theory]
    [MemberData(nameof(UnsafeNames))]
    public void Extract_ZipEntryOutsideTheDestination_Throws(string name)
    {
        string archive = CreateZip("evil.zip", ("ok.txt", "ok"), (name, "evil"));

        Assert.Throws<IOException>(() => Archive.Extract(archive, PathOf("box/out")));

        Assert.Equal(["box/", "evil.zip"], Tree(_dir));
    }

    [Theory]
    [MemberData(nameof(UnsafeNames))]
    public void Extract_TarEntryOutsideTheDestination_Throws(string name)
    {
        string archive = CreateTar("evil.tar", TarFile("ok.txt", "ok"), TarFile(name, "evil"));

        Assert.Throws<IOException>(() => Archive.Extract(archive, PathOf("box/out")));

        Assert.Equal(["box/", "evil.tar"], Tree(_dir));
    }

    [Theory]
    [InlineData("../../outside")]
    [InlineData("/etc")]
    public void Extract_TarSymlinkOutsideTheDestination_Throws(string target)
    {
        string archive = CreateTar(
            "evil.tar",
            new PaxTarEntry(TarEntryType.SymbolicLink, "link") { LinkName = target },
            TarFile("link/evil.txt", "evil"));

        Assert.Throws<IOException>(() => Archive.Extract(archive, PathOf("box/out")));

        Assert.Equal(["box/", "evil.tar"], Tree(_dir));
    }

    [Fact]
    public void Extract_TarDotDotThroughALinkedFolder_Throws()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating links needs privileges on Windows");
        // "up" points at the destination itself, so "up/x/../.." would really be its parent's parent.
        string archive = CreateTar(
            "evil.tar",
            new PaxTarEntry(TarEntryType.Directory, "x/"),
            new PaxTarEntry(TarEntryType.SymbolicLink, "up") { LinkName = "." },
            new PaxTarEntry(TarEntryType.SymbolicLink, "up/x/escape") { LinkName = "../.." });

        Assert.Throws<IOException>(() => Archive.Extract(archive, PathOf("box/out")));

        Assert.Equal(["box/", "evil.tar"], Tree(_dir));
    }

    [Fact]
    public void Extract_TarLinksInsideTheDestination_AreKept()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating links needs privileges on Windows");
        string archive = CreateTar(
            "ok.tar",
            TarFile("data/real.txt", "real"),
            new PaxTarEntry(TarEntryType.SymbolicLink, "bin/soft") { LinkName = "../data/real.txt" },
            new PaxTarEntry(TarEntryType.HardLink, "hard.txt") { LinkName = "data/real.txt" });

        Archive.Extract(archive, PathOf("out"));

        Assert.Equal("../data/real.txt", new FileInfo(PathOf("out/bin/soft")).LinkTarget);
        Assert.Equal("real", File.ReadAllText(PathOf("out/bin/soft")));
        Assert.Equal("real", File.ReadAllText(PathOf("out/hard.txt")));
    }

    // ---- Limits ----

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.TarGz)]
    public void Extract_MaxTotalBytes_StopsAndLeavesNothing(ArchiveFormat format)
    {
        File.WriteAllBytes(CreateFile("src/zeros.bin"), new byte[4 * 1024 * 1024]);
        string archive = PathOf("bomb" + Extension(format));
        Archive.Create(PathOf("src"), archive);
        Assert.True(new FileInfo(archive).Length < 100_000);

        Assert.Throws<InvalidDataException>(() =>
            Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions { MaxTotalBytes = 1024 * 1024 }));

        Assert.False(Directory.Exists(PathOf("out")));
        Assert.Equal(["bomb" + Extension(format), "src/", "src/zeros.bin"], Tree(_dir));
    }

    [Fact]
    public void Extract_MaxEntries()
    {
        string source = CreateSource();
        string archive = PathOf("a.tar");
        Archive.Create(source, archive);

        Assert.Throws<InvalidDataException>(() => Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions { MaxEntries = 4 }));
        Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions { MaxEntries = 5 });
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.TarGz)]
    public void SingleEntry_MaxBytes(ArchiveFormat format)
    {
        CreateFile("src/a.txt", new string('a', 5000));
        string archive = PathOf("a" + Extension(format));
        Archive.Create(PathOf("src"), archive);

        Assert.Throws<InvalidDataException>(() => Archive.ReadEntryBytes(archive, "a.txt", maxBytes: 4999));
        Assert.Throws<InvalidDataException>(() => Archive.ExtractEntry(archive, "a.txt", PathOf("one.txt"), maxBytes: 100));
        Assert.False(File.Exists(PathOf("one.txt")));
        using (Stream stream = Archive.OpenEntry(archive, "a.txt", maxBytes: 100))
        {
            Assert.Throws<InvalidDataException>(() => stream.CopyTo(Stream.Null));
        }

        Assert.Equal(5000, Archive.ReadEntryBytes(archive, "a.txt", maxBytes: 5000).Length);
    }

    // ---- Progress ----

    [Fact]
    public void Progress_CreateAndZipExtract_ReachTheTotals()
    {
        string source = CreateSource();
        File.WriteAllBytes(Path.Combine(source, "big.bin"), new byte[3 * 1024 * 1024 + 7]);
        long bytes = 5 + 5 + 6 + 3 * 1024 * 1024 + 7;
        string archive = PathOf("a.zip");
        SyncProgress create = new();
        SyncProgress extract = new();

        Archive.Create(source, archive, new ArchiveCreateOptions { Progress = create });
        Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions { Progress = extract });

        foreach (List<ArchiveProgress> reports in (List<ArchiveProgress>[])[create.Reports, extract.Reports])
        {
            Assert.True(reports.Count > 6, "per entry and per MiB");
            Assert.Equal(reports.OrderBy(r => r.BytesDone).ThenBy(r => r.EntriesDone), reports);
            Assert.Equal(new ArchiveProgress(6, 6, bytes, bytes, reports[^1].CurrentEntry), reports[^1]);
        }
    }

    [Fact]
    public void Progress_TarExtract_HasNoTotals()
    {
        string source = CreateSource();
        string archive = PathOf("a.tar.gz");
        Archive.Create(source, archive);
        SyncProgress progress = new();

        Archive.Extract(archive, PathOf("out"), new ArchiveExtractOptions { Progress = progress });

        Assert.Equal(new ArchiveProgress(5, null, 16, null, "sub/b.txt"), progress.Reports[^1]);
    }

    // ---- Single entries ----

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.TarGz)]
    public async Task SingleEntry_Lookups(ArchiveFormat format)
    {
        string source = CreateSource();
        string archive = PathOf("a" + Extension(format));
        Archive.Create(source, archive);

        Assert.Equal("bravo", Archive.ReadEntryText(archive, "sub/b.txt"));
        Assert.Equal("bravo", Archive.ReadEntryText(archive, "sub\\b.txt"));
        Assert.Equal("bravo", Archive.ReadEntryText(archive, "./sub/b.txt"));
        Assert.Equal("bravo"u8.ToArray(), await Archive.ReadEntryBytesAsync(archive, "/sub/b.txt", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("bravo", await Archive.ReadEntryTextAsync(archive, "sub/b.txt", Encoding.ASCII, cancellationToken: TestContext.Current.CancellationToken));

        Assert.True(Archive.ContainsEntry(archive, "a.txt"));
        Assert.False(Archive.ContainsEntry(archive, "sub"));
        Assert.False(Archive.ContainsEntry(archive, "A.TXT"));
        Assert.False(await Archive.ContainsEntryAsync(archive, "missing.txt", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Null(Archive.TryOpenEntry(archive, "empty"));
        Assert.Null(await Archive.TryOpenEntryAsync(archive, "missing.txt", cancellationToken: TestContext.Current.CancellationToken));
        Assert.Throws<FileNotFoundException>(() => Archive.OpenEntry(archive, "missing.txt"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => Archive.OpenEntryAsync(archive, "sub", cancellationToken: TestContext.Current.CancellationToken));

        // Nothing above left the archive open.
        using FileStream exclusive = new(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.TarGz)]
    public async Task OpenEntry_StreamsAndClosesTheArchiveOnDispose(ArchiveFormat format)
    {
        string source = CreateSource();
        string archive = PathOf("a" + Extension(format));
        Archive.Create(source, archive);

        await using (Stream stream = await Archive.OpenEntryAsync(archive, "a.txt", cancellationToken: TestContext.Current.CancellationToken))
        {
            Assert.False(stream.CanSeek);
            Assert.Throws<IOException>(() => new FileStream(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None).Dispose());
            using StreamReader reader = new(stream);
            Assert.Equal("alpha", await reader.ReadToEndAsync(TestContext.Current.CancellationToken));
        }

        using FileStream exclusive = new(archive, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [Theory]
    [InlineData(ArchiveFormat.Zip, false)]
    [InlineData(ArchiveFormat.Tar, true)]
    public async Task ExtractEntry_WritesOneFile(ArchiveFormat format, bool useAsync)
    {
        string source = CreateSource();
        DateTime written = new(2022, 2, 2, 2, 2, 2, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(Path.Combine(source, "sub", "b.txt"), written);
        using MemoryStream archive = new();
        Archive.Create(source, archive, new ArchiveCreateOptions { Format = format });
        string target = CreateFile("out/b.txt", "mine");

        async Task Run(bool overwrite)
        {
            archive.Position = 0;
            if (useAsync)
            {
                await Archive.ExtractEntryAsync(archive, "sub/b.txt", target, overwrite, cancellationToken: TestContext.Current.CancellationToken);
            }
            else
            {
                Archive.ExtractEntry(archive, "sub/b.txt", target, overwrite);
            }
        }

        await Assert.ThrowsAsync<IOException>(() => Run(overwrite: false));
        Assert.Equal("mine", File.ReadAllText(target));

        await Run(overwrite: true);
        Assert.Equal("bravo", File.ReadAllText(target));
        Assert.Equal(written, File.GetLastWriteTimeUtc(target));
        Assert.Equal(["b.txt"], Tree(PathOf("out")));
    }

    // ---- GetEntries ----

    [Theory]
    [InlineData(ArchiveFormat.Zip)]
    [InlineData(ArchiveFormat.Tar)]
    [InlineData(ArchiveFormat.TarGz)]
    public async Task GetEntries_ListsFilesAndFolders(ArchiveFormat format)
    {
        string source = CreateSource();
        string archive = PathOf("a" + Extension(format));
        Archive.Create(source, archive);

        IReadOnlyList<ArchiveEntryInfo> entries = await Archive.GetEntriesAsync(archive, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            [(".hidden", 6L, false), ("a.txt", 5L, false), ("empty", 0L, true), ("sub", 0L, true), ("sub/b.txt", 5L, false)],
            entries.Select(entry => (entry.Name, entry.Length, entry.IsDirectory)));
    }

    [Fact]
    public void GetEntries_NormalizesNamesFromOtherTools()
    {
        string archive = CreateTar(
            "other.tar",
            new PaxTarEntry(TarEntryType.Directory, "./"),
            new PaxTarEntry(TarEntryType.Directory, "./docs/"),
            TarFile("./docs/readme.md", "# hi"),
            new PaxTarEntry(TarEntryType.Fifo, "./pipe"));

        Assert.Equal(["docs", "docs/readme.md"], Archive.GetEntries(archive).Select(entry => entry.Name));
        Assert.Equal("# hi", Archive.ReadEntryText(archive, "docs/readme.md"));
        Archive.Extract(archive, PathOf("out"));
        Assert.Equal(["docs/", "docs/readme.md"], Tree(PathOf("out")));
    }
}
