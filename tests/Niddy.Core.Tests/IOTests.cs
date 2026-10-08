using Niddy.Core.IO;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-atomic-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void WriteAllText_ReplacesTheFileAndLeavesNoTempFiles()
    {
        string path = Path.Combine(_dir, "sub", "a.txt");

        AtomicFile.WriteAllText(path, "one");
        AtomicFile.WriteAllText(path, "two");

        Assert.Equal("two", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public async Task WriteAsync_WhenTheWriterThrows_KeepsTheOldFile()
    {
        string path = Path.Combine(_dir, "b.bin");
        await AtomicFile.WriteAllBytesAsync(path, new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => AtomicFile.WriteAsync(path, async (stream, ct) =>
        {
            await stream.WriteAsync(new byte[] { 9 }, ct);
            throw new InvalidOperationException();
        }, TestContext.Current.CancellationToken));

        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Single(Directory.GetFiles(_dir));
    }

    [Fact]
    public void WriteAllText_HasNoByteOrderMark()
    {
        string path = Path.Combine(_dir, "c.txt");

        AtomicFile.WriteAllText(path, "hé");

        Assert.Equal("hé"u8.ToArray(), File.ReadAllBytes(path));
    }
}

public class FileWatcherMergeTests
{
    private static List<FileChange> Merge(params FileChange[] events) => MergeWith(StringComparison.Ordinal, events);

    private static List<FileChange> MergeWith(StringComparison comparison, params FileChange[] events)
    {
        List<FileChange> changes = new();
        foreach (FileChange change in events)
        {
            DebouncedFileWatcher.Merge(changes, change, comparison);
        }

        return changes;
    }

    [Fact]
    public void PathsDifferingOnlyInCase_MergeOnlyWhenTheComparisonIgnoresCase()
    {
        Assert.Single(MergeWith(StringComparison.OrdinalIgnoreCase, C("/a", WatcherChangeTypes.Changed), C("/A", WatcherChangeTypes.Changed)));
        Assert.Equal(2, MergeWith(StringComparison.Ordinal, C("/a", WatcherChangeTypes.Changed), C("/A", WatcherChangeTypes.Changed)).Count);
    }

    private static FileChange C(string path, WatcherChangeTypes type, string? old = null) => new(path, type, old);

    [Fact]
    public void RepeatedChanges_BecomeOne() =>
        Assert.Equal([C("/a", WatcherChangeTypes.Changed)],
            Merge(C("/a", WatcherChangeTypes.Changed), C("/a", WatcherChangeTypes.Changed)));

    [Fact]
    public void CreatedThenChanged_IsCreated() =>
        Assert.Equal([C("/a", WatcherChangeTypes.Created)],
            Merge(C("/a", WatcherChangeTypes.Created), C("/a", WatcherChangeTypes.Changed)));

    [Fact]
    public void CreatedThenDeleted_IsDropped() =>
        Assert.Empty(Merge(C("/a", WatcherChangeTypes.Created), C("/a", WatcherChangeTypes.Deleted)));

    [Fact]
    public void DeletedThenCreated_IsChanged() =>
        Assert.Equal([C("/a", WatcherChangeTypes.Changed)],
            Merge(C("/a", WatcherChangeTypes.Deleted), C("/a", WatcherChangeTypes.Created)));

    [Fact]
    public void TempFileRenamedOverTarget_IsARename() =>
        Assert.Equal([C("/a", WatcherChangeTypes.Renamed, "/a.tmp")],
            Merge(C("/a", WatcherChangeTypes.Changed), C("/a", WatcherChangeTypes.Renamed, "/a.tmp")));

    [Fact]
    public void CreatedThenRenamed_IsCreatedUnderTheNewName() =>
        Assert.Equal([C("/b", WatcherChangeTypes.Created)],
            Merge(C("/a", WatcherChangeTypes.Created), C("/b", WatcherChangeTypes.Renamed, "/a")));

    [Fact]
    public void DifferentPaths_KeepTheirOrder() =>
        Assert.Equal(["/b", "/a"],
            Merge(C("/b", WatcherChangeTypes.Changed), C("/a", WatcherChangeTypes.Created), C("/b", WatcherChangeTypes.Changed))
                .Select(c => c.FullPath));
}

// The watcher reads Platform.Info, which PlatformTests briefly make throw.
[Collection(PlatformStateCollection.Name)]
public sealed class DebouncedFileWatcherTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-watch-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Changed_IsRaisedOnceForABurstOfWrites()
    {
        string path = Path.Combine(_dir, "watched.txt");
        using DebouncedFileWatcher watcher = DebouncedFileWatcher.ForFile(path, TimeSpan.FromMilliseconds(200), captureContext: false);
        TaskCompletionSource<IReadOnlyList<FileChange>> raised = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        watcher.Changed += (_, e) =>
        {
            Interlocked.Increment(ref count);
            raised.TrySetResult(e.Changes);
        };

        for (int i = 0; i < 5; i++)
        {
            await File.WriteAllTextAsync(path, $"write {i}", TestContext.Current.CancellationToken);
        }

        IReadOnlyList<FileChange> changes = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        FileChange change = Assert.Single(changes);
        Assert.Equal("watched.txt", Path.GetFileName(change.FullPath));
    }
}

public class AppPathsTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] vars) =>
        name => vars.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void Windows_UsesRoamingAndLocalAppData()
    {
        AppPaths paths = AppPaths.For("App", "Org", OS.Windows,
            Env(("APPDATA", "/r"), ("LOCALAPPDATA", "/l")), "/home");

        Assert.Equal(Path.Combine("/r", "Org", "App"), paths.Data);
        Assert.Equal(paths.Data, paths.Config);
        Assert.Equal(Path.Combine("/l", "Org", "App", "Cache"), paths.Cache);
        Assert.Equal(Path.Combine("/l", "Org", "App", "Logs"), paths.Logs);
    }

    [Fact]
    public void MacOS_UsesLibraryFolders()
    {
        AppPaths paths = AppPaths.For("My App", null, OS.MacOS, Env(), "/Users/me");

        Assert.Equal("/Users/me/Library/Application Support/My App", paths.Data);
        Assert.Equal("/Users/me/Library/Caches/My App", paths.Cache);
        Assert.Equal("/Users/me/Library/Logs/My App", paths.Logs);
    }

    [Fact]
    public void Linux_FollowsXdgWithDefaults()
    {
        AppPaths defaults = AppPaths.For("My App", null, OS.Linux, Env(), "/home/me");
        AppPaths custom = AppPaths.For("My App", null, OS.Linux, Env(("XDG_CONFIG_HOME", "/cfg"), ("XDG_STATE_HOME", "relative")), "/home/me");

        Assert.Equal("/home/me/.local/share/my-app", defaults.Data);
        Assert.Equal("/home/me/.config/my-app", defaults.Config);
        Assert.Equal("/home/me/.cache/my-app", defaults.Cache);
        Assert.Equal("/home/me/.local/state/my-app/logs", defaults.Logs);
        Assert.Equal("/cfg/my-app", custom.Config);
        Assert.Equal("/home/me/.local/state/my-app/logs", custom.Logs); // Relative XDG paths are ignored.
    }

    [Fact]
    public void Portable_KeepsEverythingUnderOneFolder()
    {
        AppPaths paths = AppPaths.Portable("/opt/app");

        Assert.Equal(Path.GetFullPath("/opt/app"), paths.Data);
        Assert.Equal(Path.Combine(paths.Data, "Logs"), paths.Logs);
    }

    [Fact]
    public void For_RejectsNamesThatArentFolderNames() =>
        Assert.Throws<ArgumentException>(() => AppPaths.For("a/b", null, OS.Linux, Env(), "/home/me"));

    [Fact]
    public void MacCatalyst_UsesLibraryFolders()
    {
        AppPaths paths = AppPaths.For("My App", null, OS.MacCatalyst, Env(), "/Users/me");

        Assert.Equal("/Users/me/Library/Application Support/My App", paths.Data);
    }

    [Fact]
    public void FreeBSD_FollowsXdg()
    {
        AppPaths paths = AppPaths.For("My App", null, OS.FreeBSD, Env(("XDG_DATA_HOME", "/data")), "/home/me");

        Assert.Equal("/data/my-app", paths.Data);
        Assert.Equal("/home/me/.config/my-app", paths.Config);
    }

    [Fact]
    public void EnsureCreated_CreatesTheFolders()
    {
        string root = Path.Combine(Path.GetTempPath(), "niddy-paths-" + Guid.NewGuid().ToString("N"));
        try
        {
            AppPaths paths = AppPaths.Portable(root).EnsureCreated();
            Assert.True(Directory.Exists(paths.Cache));
            Assert.True(Directory.Exists(paths.Logs));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

public class PathComparisonTests
{
    [Theory]
    [InlineData(OS.Windows, StringComparison.OrdinalIgnoreCase)]
    [InlineData(OS.MacOS, StringComparison.OrdinalIgnoreCase)]
    [InlineData(OS.MacCatalyst, StringComparison.OrdinalIgnoreCase)]
    [InlineData(OS.Linux, StringComparison.Ordinal)]
    [InlineData(OS.FreeBSD, StringComparison.Ordinal)]
    [InlineData(OS.Android, StringComparison.Ordinal)]
    [InlineData(OS.iOS, StringComparison.Ordinal)]
    [InlineData(OS.Browser, StringComparison.Ordinal)]
    public void PathComparisonFor_MatchesThePlatformsDefaultFileSystem(OS os, StringComparison expected) =>
        Assert.Equal(expected, FileSystemExtensions.PathComparisonFor(os));
}
