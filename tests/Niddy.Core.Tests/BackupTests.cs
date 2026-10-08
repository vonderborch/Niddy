using System.IO.Compression;
using Microsoft.Extensions.Time.Testing;
using Niddy.Core.IO;
using Niddy.Core.IO.Archives;
using Niddy.Core.IO.Backups;

namespace Niddy.Core.Tests;

[Collection(PlatformStateCollection.Name)]
public sealed class BackupTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-backup-").FullName;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 14, 3, 12, TimeSpan.Zero));

    public void Dispose() => Directory.DeleteWithRetry(_dir, baseDelayMs: 0, throwOnFailure: false);

    private string CreateFile(string relativePath, string content = "x")
    {
        string path = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private string[] Names(string? directory = null) =>
        Directory.EnumerateFileSystemEntries(directory ?? _dir).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    [Fact]
    public void FileBackup_WhenMissing_ReturnsNull()
    {
        Assert.Null(File.Backup(Path.Combine(_dir, "missing.txt")));
        Assert.Empty(Names());
    }

    [Fact]
    public void FileBackup_Timestamp_UsesTheUtcTimeAndCopiesTheFile()
    {
        string source = CreateFile("report.txt", "hello");
        DateTime written = new(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(source, written);

        string? backup = File.Backup(source, timeProvider: _time);

        Assert.Equal(Path.Combine(_dir, "report.backup-20260928T140312Z.txt"), backup);
        Assert.Equal("hello", File.ReadAllText(backup!));
        Assert.Equal(written, File.GetLastWriteTimeUtc(backup!));
        Assert.Equal("hello", File.ReadAllText(source));
    }

    [Fact]
    public void FileBackup_Timestamp_AddsACounterWithinTheSameSecond()
    {
        string source = CreateFile("report.txt");

        File.Backup(source, timeProvider: _time);
        File.Backup(source, timeProvider: _time);
        File.Backup(source, timeProvider: _time);

        Assert.Equal(
            ["report.backup-20260928T140312Z-2.txt", "report.backup-20260928T140312Z-3.txt", "report.backup-20260928T140312Z.txt", "report.txt"],
            Names());
    }

    [Fact]
    public void FileBackup_Numbered_CountsUpFromTheHighestExistingNumber()
    {
        string source = CreateFile("report.txt");

        Assert.EndsWith("report.backup-1.txt", File.Backup(source, BackupNaming.Numbered));
        CreateFile("report.backup-7.txt");
        Assert.EndsWith("report.backup-8.txt", File.Backup(source, BackupNaming.Numbered));
    }

    [Theory]
    [InlineData(".gitignore", ".gitignore.backup-1")]
    [InlineData("Makefile", "Makefile.backup-1")]
    [InlineData("archive.tar.gz", "archive.tar.backup-1.gz")]
    public void FileBackup_KeepsTheExtensionLast(string name, string expected)
    {
        string source = CreateFile(name);

        Assert.Equal(Path.Combine(_dir, expected), File.Backup(source, BackupNaming.Numbered));
    }

    [Theory]
    [InlineData("report.txt", ".bak", "report.txt.backup-1.bak")]
    [InlineData("report.txt", "bak", "report.txt.backup-1.bak")]
    [InlineData("report.txt", "", "report.txt.backup-1")]
    [InlineData(".gitignore", ".bak", ".gitignore.backup-1.bak")]
    [InlineData("Makefile", ".old", "Makefile.backup-1.old")]
    public void FileBackup_Extension_KeepsTheWholeNameAndEndsWithIt(string name, string extension, string expected)
    {
        string source = CreateFile(name);

        Assert.Equal(Path.Combine(_dir, expected), File.Backup(source, BackupNaming.Numbered, extension: extension));
    }

    [Fact]
    public void FileBackup_Extension_KeepCountsOnlyThatExtension()
    {
        string source = CreateFile("report.txt");
        File.Backup(source, BackupNaming.Numbered);                    // report.backup-1.txt
        File.Backup(source, BackupNaming.Numbered, extension: "");     // report.txt.backup-1

        for (int i = 0; i < 3; i++)
        {
            File.Backup(source, BackupNaming.Numbered, keep: 1, extension: ".bak");
        }

        Assert.Equal(["report.backup-1.txt", "report.txt", "report.txt.backup-1", "report.txt.backup-3.bak"], Names());
    }

    [Fact]
    public void FileBackup_Keep_DeletesTheOldestMatchingBackupsOnly()
    {
        string source = CreateFile("report.txt");
        CreateFile("report.backup-notes.txt");                    // not a number
        CreateFile("report.old-1.txt");                           // another label
        CreateFile("report.backup-20200101T000000Z.txt");         // another naming style
        CreateFile("other.backup-1.txt");                         // another file

        for (int i = 0; i < 4; i++)
        {
            File.Backup(source, BackupNaming.Numbered, keep: 2);
        }

        Assert.Equal(
        [
            "other.backup-1.txt", "report.backup-20200101T000000Z.txt", "report.backup-3.txt", "report.backup-4.txt",
            "report.backup-notes.txt", "report.old-1.txt", "report.txt",
        ], Names());
    }

    [Fact]
    public void FileBackup_Keep_OrdersTimestampsByTimeThenCounter()
    {
        string source = CreateFile("report.txt");

        File.Backup(source, keep: 2, timeProvider: _time);   // …140312Z
        File.Backup(source, keep: 2, timeProvider: _time);   // …140312Z-2
        _time.Advance(TimeSpan.FromHours(1));
        File.Backup(source, keep: 2, timeProvider: _time);   // …150312Z

        Assert.Equal(["report.backup-20260928T140312Z-2.txt", "report.backup-20260928T150312Z.txt", "report.txt"], Names());
    }

    [Fact]
    public void FileBackup_ToAnotherDirectory_CreatesIt()
    {
        string source = CreateFile("report.txt");
        string backups = Path.Combine(_dir, "backups", "reports");

        string? backup = File.Backup(source, BackupNaming.Numbered, backupDirectory: backups, label: "pre-migration");

        Assert.Equal(Path.Combine(backups, "report.pre-migration-1.txt"), backup);
        Assert.True(File.Exists(backup));
    }

    [Fact]
    public void FileBackup_RejectsBadArguments()
    {
        string source = CreateFile("report.txt");

        Assert.Throws<ArgumentException>(() => File.Backup(source, label: "a/b"));
        Assert.Throws<ArgumentException>(() => File.Backup(source, label: ""));
        Assert.Throws<ArgumentOutOfRangeException>(() => File.Backup(source, keep: 0));
        Assert.Throws<ArgumentException>(() => File.Backup(source, extension: "."));
        Assert.Throws<ArgumentException>(() => File.Backup(source, extension: ".b/k"));
        Assert.Equal(["report.txt"], Names());
    }

    [Fact]
    public void FileBackup_WhileAnotherStreamIsWritingIt()
    {
        string source = CreateFile("app.log", "line 1\n");
        using FileStream writer = new(source, FileMode.Append, FileAccess.Write, FileShare.Read);

        string? backup = File.Backup(source, BackupNaming.Numbered);

        Assert.Equal("line 1\n", File.ReadAllText(backup!));
    }

    [Fact]
    public void DirectoryBackup_WhenMissing_ReturnsNull()
    {
        Assert.Null(Directory.Backup(Path.Combine(_dir, "missing")));
    }

    [Fact]
    public void DirectoryBackup_CopiesTheTreeWithoutSplittingDotsInTheName()
    {
        CreateFile(Path.Combine("my.app", "a.txt"), "a");
        CreateFile(Path.Combine("my.app", "sub", "b.txt"), "b");

        string? backup = Directory.Backup(Path.Combine(_dir, "my.app") + Path.DirectorySeparatorChar, BackupNaming.Numbered);

        Assert.Equal(Path.Combine(_dir, "my.app.backup-1"), backup);
        Assert.Equal("a", File.ReadAllText(Path.Combine(backup!, "a.txt")));
        Assert.Equal("b", File.ReadAllText(Path.Combine(backup!, "sub", "b.txt")));
    }

    [Fact]
    public void DirectoryBackup_Zip_WritesAnArchive()
    {
        CreateFile(Path.Combine("data", "a.txt"), "a");
        CreateFile(Path.Combine("data", "sub", "b.txt"), "b");

        string? backup = Directory.Backup(Path.Combine(_dir, "data"), archive: ArchiveFormat.Zip, timeProvider: _time);

        Assert.Equal(Path.Combine(_dir, "data.backup-20260928T140312Z.zip"), backup);
        using ZipArchive archive = ZipFile.OpenRead(backup!);
        Assert.Equal(["a.txt", "sub/", "sub/b.txt"], archive.Entries.Select(e => e.FullName.Replace('\\', '/')).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void DirectoryBackup_Extension_AppliesToCopiesAndZips()
    {
        string data = Path.GetDirectoryName(CreateFile(Path.Combine("data", "a.txt"), "a"))!;

        string? copy = Directory.Backup(data, BackupNaming.Numbered, extension: "bak");
        string? archive = Directory.Backup(data, BackupNaming.Numbered, archive: ArchiveFormat.Zip, extension: ".zip.bak");

        Assert.Equal(Path.Combine(_dir, "data.backup-1.bak"), copy);
        Assert.Equal("a", File.ReadAllText(Path.Combine(copy!, "a.txt")));
        Assert.Equal(Path.Combine(_dir, "data.backup-1.zip.bak"), archive);
        using ZipArchive zip = ZipFile.OpenRead(archive!);
        Assert.Equal("a.txt", Assert.Single(zip.Entries).FullName);
    }

    [Fact]
    public void DirectoryBackup_Keep_PrunesCopiesAndZipsSeparately()
    {
        string data = Path.GetDirectoryName(CreateFile(Path.Combine("data", "a.txt")))!;

        for (int i = 0; i < 3; i++)
        {
            Directory.Backup(data, BackupNaming.Numbered, keep: 1);
        }

        for (int i = 0; i < 3; i++)
        {
            Directory.Backup(data, BackupNaming.Numbered, keep: 2, archive: ArchiveFormat.Zip);
        }

        Assert.Equal(["data", "data.backup-2.zip", "data.backup-3", "data.backup-3.zip"], Names());
    }

    [Fact]
    public void DirectoryBackup_TarGz_WritesAnArchiveAndPrunes()
    {
        string data = Path.GetDirectoryName(CreateFile(Path.Combine("data", "a.txt"), "a"))!;

        string? backup = null;
        for (int i = 0; i < 3; i++)
        {
            backup = Directory.Backup(data, BackupNaming.Numbered, keep: 2, archive: ArchiveFormat.TarGz);
        }

        Assert.Equal(Path.Combine(_dir, "data.backup-3.tar.gz"), backup);
        Assert.Equal(["data", "data.backup-2.tar.gz", "data.backup-3.tar.gz"], Names());
        Assert.Equal("a", Archive.ReadEntryText(backup!, "a.txt"));
    }

    [Fact]
    public void DirectoryBackup_Keep_LeavesOtherFormatsAlone()
    {
        string data = Path.GetDirectoryName(CreateFile(Path.Combine("data", "a.txt")))!;

        Directory.Backup(data, BackupNaming.Numbered, archive: ArchiveFormat.Zip);
        Directory.Backup(data, BackupNaming.Numbered, archive: ArchiveFormat.Tar);
        for (int i = 0; i < 2; i++)
        {
            Directory.Backup(data, BackupNaming.Numbered, keep: 1, archive: ArchiveFormat.TarGz);
        }

        Assert.Equal(["data", "data.backup-1.tar", "data.backup-1.zip", "data.backup-2.tar.gz"], Names());
    }

    [Fact]
    public void DirectoryBackup_RefusesToBackUpIntoItself()
    {
        string data = Path.GetDirectoryName(CreateFile(Path.Combine("data", "a.txt")))!;

        Assert.Throws<ArgumentException>(() => Directory.Backup(data, backupDirectory: Path.Combine(data, "backups")));
        Assert.Throws<ArgumentException>(() => Directory.Backup(data, backupDirectory: data, archive: ArchiveFormat.Zip));
        Assert.Equal(["a.txt"], Names(data));
    }

    [Theory]
    [InlineData("/a/data", "/a/data", "Ordinal", true)]
    [InlineData("/a/data/backups", "/a/data", "Ordinal", true)]
    [InlineData("/a/database", "/a/data", "Ordinal", false)]
    [InlineData("/a", "/a/data", "Ordinal", false)]
    [InlineData("/a/Data", "/a/data", "Ordinal", false)]
    [InlineData("/a/DATA/backups", "/a/data", "Ordinal", false)]
    [InlineData("/a/Data", "/a/data", "OrdinalIgnoreCase", true)]
    [InlineData("/a/DATA/backups", "/a/data", "OrdinalIgnoreCase", true)]
    [InlineData("/a/DATABASE", "/a/data", "OrdinalIgnoreCase", false)]
    public void IsSameOrInside_UsesTheGivenComparison(string path, string directory, string comparison, bool expected)
    {
        Assert.Equal(expected, FileSystemExtensions.IsSameOrInside(path, directory, Enum.Parse<StringComparison>(comparison)));
    }

    [Fact]
    public void DirectoryBackup_IntoItself_IsRejectedWhateverTheCase()
    {
        Assert.SkipWhen(Path.Comparison == StringComparison.Ordinal, "Paths are case-sensitive here.");
        string source = Path.Combine(_dir, "Data");
        Directory.CreateDirectory(source);

        Assert.Throws<ArgumentException>(() => Directory.Backup(source, backupDirectory: Path.Combine(_dir, "DATA", "backups")));
    }

    [Fact]
    public void DirectoryBackup_Keep_CountsBackupsMadeUnderAnotherCase()
    {
        Assert.SkipWhen(Path.Comparison == StringComparison.Ordinal, "Paths are case-sensitive here.");
        Directory.CreateDirectory(Path.Combine(_dir, "Data"));
        Directory.Backup(Path.Combine(_dir, "Data"), BackupNaming.Numbered);
        Directory.Backup(Path.Combine(_dir, "Data"), BackupNaming.Numbered);

        string? backup = Directory.Backup(Path.Combine(_dir, "data"), BackupNaming.Numbered, keep: 1);

        Assert.Equal("data.backup-3", Path.GetFileName(backup));
        Assert.Equal(["Data", "data.backup-3"], Names());
    }

    [Fact]
    public void DirectoryBackup_Keep_FindsBackupsOfDotDirectories()
    {
        string source = Path.Combine(_dir, ".config");
        Directory.CreateDirectory(source);
        for (int i = 0; i < 3; i++)
        {
            Directory.Backup(source, BackupNaming.Numbered, keep: 2);
        }

        Assert.Equal([".config", ".config.backup-2", ".config.backup-3"], Names());
    }
}
