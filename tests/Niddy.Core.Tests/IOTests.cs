using Niddy.IO;

namespace Niddy.Core.Tests;

public sealed class AtomicFileTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-atomic-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void WriteAllText_ReplacesTheFileAndLeavesNoTempFiles()
    {
        var path = Path.Combine(_dir, "sub", "a.txt");

        AtomicFile.WriteAllText(path, "one");
        AtomicFile.WriteAllText(path, "two");

        Assert.Equal("two", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!));
    }

    [Fact]
    public async Task WriteAsync_WhenTheWriterThrows_KeepsTheOldFile()
    {
        var path = Path.Combine(_dir, "b.bin");
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
        var path = Path.Combine(_dir, "c.txt");

        AtomicFile.WriteAllText(path, "hé");

        Assert.Equal("hé"u8.ToArray(), File.ReadAllBytes(path));
    }
}

public class FileWatcherMergeTests
{
    private static List<FileChange> Merge(params FileChange[] events)
    {
        var changes = new List<FileChange>();
        foreach (var change in events)
            DebouncedFileWatcher.Merge(changes, change);
        return changes;
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

public sealed class DebouncedFileWatcherTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-watch-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public async Task Changed_IsRaisedOnceForABurstOfWrites()
    {
        var path = Path.Combine(_dir, "watched.txt");
        using var watcher = DebouncedFileWatcher.ForFile(path, TimeSpan.FromMilliseconds(200), captureContext: false);
        var raised = new TaskCompletionSource<IReadOnlyList<FileChange>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        watcher.Changed += (_, e) =>
        {
            Interlocked.Increment(ref count);
            raised.TrySetResult(e.Changes);
        };

        for (var i = 0; i < 5; i++)
            await File.WriteAllTextAsync(path, $"write {i}", TestContext.Current.CancellationToken);

        var changes = await raised.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await Task.Delay(500, TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        var change = Assert.Single(changes);
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
        var paths = AppPaths.For("App", "Org", AppPaths.Platform.Windows,
            Env(("APPDATA", "/r"), ("LOCALAPPDATA", "/l")), "/home");

        Assert.Equal(Path.Combine("/r", "Org", "App"), paths.Data);
        Assert.Equal(paths.Data, paths.Config);
        Assert.Equal(Path.Combine("/l", "Org", "App", "Cache"), paths.Cache);
        Assert.Equal(Path.Combine("/l", "Org", "App", "Logs"), paths.Logs);
    }

    [Fact]
    public void MacOS_UsesLibraryFolders()
    {
        var paths = AppPaths.For("My App", null, AppPaths.Platform.MacOS, Env(), "/Users/me");

        Assert.Equal("/Users/me/Library/Application Support/My App", paths.Data);
        Assert.Equal("/Users/me/Library/Caches/My App", paths.Cache);
        Assert.Equal("/Users/me/Library/Logs/My App", paths.Logs);
    }

    [Fact]
    public void Linux_FollowsXdgWithDefaults()
    {
        var defaults = AppPaths.For("My App", null, AppPaths.Platform.Linux, Env(), "/home/me");
        var custom = AppPaths.For("My App", null, AppPaths.Platform.Linux, Env(("XDG_CONFIG_HOME", "/cfg"), ("XDG_STATE_HOME", "relative")), "/home/me");

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
        var paths = AppPaths.Portable("/opt/app");

        Assert.Equal(Path.GetFullPath("/opt/app"), paths.Data);
        Assert.Equal(Path.Combine(paths.Data, "Logs"), paths.Logs);
    }

    [Fact]
    public void For_RejectsNamesThatArentFolderNames() =>
        Assert.Throws<ArgumentException>(() => AppPaths.For("a/b"));

    [Fact]
    public void EnsureCreated_CreatesTheFolders()
    {
        var root = Path.Combine(Path.GetTempPath(), "niddy-paths-" + Guid.NewGuid().ToString("N"));
        try
        {
            var paths = AppPaths.Portable(root).EnsureCreated();
            Assert.True(Directory.Exists(paths.Cache));
            Assert.True(Directory.Exists(paths.Logs));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
