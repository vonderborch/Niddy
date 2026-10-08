using System.Runtime.CompilerServices;
using Niddy.Core.IO;
using Niddy.Core.Resilience;

namespace Niddy.Core.Tests;

public sealed class FileSystemExtensionsTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-io-").FullName;

    // Tests leave read-only entries behind on purpose.
    public void Dispose() => Directory.DeleteWithRetry(_dir, baseDelayMs: 0, throwOnFailure: false);

    // On Linux and macOS, a read-only directory is what blocks a delete, and root ignores that.
    private static bool ReadOnlyBlocksDeletes => OperatingSystem.IsWindows() || Environment.UserName != "root";

    private string CreateFile(params string[] parts)
    {
        string path = Path.Combine([_dir, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private static void MakeReadOnly(string path) =>
        File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);

    /// <summary>A tree that plain <c>Directory.Delete</c> can't remove: a read-only file (Windows) inside a
    /// read-only directory (Linux, macOS).</summary>
    private string CreateReadOnlyTree()
    {
        string root = Path.Combine(_dir, "tree");
        string file = CreateFile("tree", "locked", "a.txt");
        CreateFile("tree", "b.txt");
        MakeReadOnly(file);
        MakeReadOnly(Path.GetDirectoryName(file)!);
        return root;
    }

    [Fact]
    public void DeleteFile_WhenMissing_ReturnsTrue()
    {
        Assert.True(File.DeleteWithRetry(Path.Combine(_dir, "missing.txt")));
    }

    [Fact]
    public void DeleteFile_WhenPathIsADirectory_LeavesItAlone()
    {
        Assert.True(File.DeleteWithRetry(_dir));
        Assert.True(Directory.Exists(_dir));
    }

    [Fact]
    public void DeleteFile_ReadOnlyFile_IsDeleted()
    {
        string path = CreateFile("a.txt");
        MakeReadOnly(path);

        Assert.True(File.DeleteWithRetry(path, baseDelayMs: 0));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteFile_WaitsForALockToBeReleased()
    {
        string path = CreateFile("a.txt");
        FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        _ = Task.Delay(150, TestContext.Current.CancellationToken).ContinueWith(_ => stream.Dispose(), TaskScheduler.Default);

        Assert.True(File.DeleteWithRetry(path, baseDelayMs: 50, releaseHandles: false));
        Assert.False(File.Exists(path));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LeakOpenStream(string path) =>
        _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

    [Fact]
    public void DeleteFile_ReleasesHandlesLeakedByThisProcess()
    {
        string path = CreateFile("a.txt");
        LeakOpenStream(path);

        Assert.True(File.DeleteWithRetry(path, maxAttempts: 3, baseDelayMs: 0));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void DeleteFile_RejectsBadArguments()
    {
        string path = CreateFile("a.txt");

        Assert.Throws<ArgumentOutOfRangeException>(() => File.DeleteWithRetry(path, maxAttempts: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => File.DeleteWithRetry(path, baseDelayMs: -1));
        Assert.Throws<ArgumentException>(() => File.DeleteWithRetry(""));
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void DeleteDirectory_WhenMissing_ReturnsTrue()
    {
        Assert.True(Directory.DeleteWithRetry(Path.Combine(_dir, "missing")));
    }

    [Fact]
    public void DeleteDirectory_ClearsReadOnlyEntries()
    {
        string root = CreateReadOnlyTree();

        Assert.True(Directory.DeleteWithRetry(root, baseDelayMs: 0));
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void DeleteDirectory_WithoutClearReadOnly_FailsOnReadOnlyEntries()
    {
        Assert.SkipUnless(ReadOnlyBlocksDeletes, "Running as root, so read-only directories don't block deletes.");
        string root = CreateReadOnlyTree();

        Assert.False(Directory.DeleteWithRetry(root, maxAttempts: 2, baseDelayMs: 0, clearReadOnly: false, throwOnFailure: false));
        Assert.True(Directory.Exists(root));

        MaxRetriesException ex = Assert.Throws<MaxRetriesException>(() => Directory.DeleteWithRetry(root, maxAttempts: 2, baseDelayMs: 0, clearReadOnly: false));
        Assert.Equal(2, ex.Attempts);
        Assert.True(ex.InnerException is IOException or UnauthorizedAccessException, ex.ToString());
    }

    [Fact]
    public void DeleteDirectory_NotRecursive_ThrowsWhenNotEmpty()
    {
        CreateFile("sub", "a.txt");
        string sub = Path.Combine(_dir, "sub");

        MaxRetriesException ex = Assert.Throws<MaxRetriesException>(() => Directory.DeleteWithRetry(sub, recursive: false, maxAttempts: 2, baseDelayMs: 0));
        Assert.IsType<IOException>(ex.InnerException, exactMatch: false);
        Assert.False(Directory.DeleteWithRetry(sub, recursive: false, maxAttempts: 1, throwOnFailure: false));
        Assert.True(File.Exists(Path.Combine(sub, "a.txt")));

        File.Delete(Path.Combine(sub, "a.txt"));
        Assert.True(Directory.DeleteWithRetry(sub, recursive: false));
        Assert.False(Directory.Exists(sub));
    }

    [Fact]
    public void DeleteDirectory_DoesNotFollowLinks()
    {
        // Creating symbolic links on Windows needs developer mode or elevation.
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Symbolic links need extra privileges on Windows.");
        Assert.SkipUnless(ReadOnlyBlocksDeletes, "Running as root, so read-only directories don't block deletes.");

        string target = CreateFile("outside", "keep.txt");
        MakeReadOnly(target);
        string root = CreateReadOnlyTree();   // a first attempt fails, so read-only attributes get cleared
        Directory.CreateSymbolicLink(Path.Combine(root, "link"), Path.GetDirectoryName(target)!);

        Assert.True(Directory.DeleteWithRetry(root, baseDelayMs: 0));

        Assert.False(Directory.Exists(root));
        Assert.True(File.Exists(target));
        Assert.True(File.GetAttributes(target).HasFlag(FileAttributes.ReadOnly));
    }

    [Fact]
    public void CreateEmpty_CreatesMissingParents()
    {
        string path = Path.Combine(_dir, "a", "b", "c");

        Directory.CreateEmpty(path);

        Assert.True(Directory.Exists(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(path));
    }

    [Fact]
    public void CreateEmpty_EmptiesAnExistingDirectory()
    {
        string file = CreateFile("out", "sub", "a.txt");
        MakeReadOnly(file);
        string path = Path.Combine(_dir, "out");

        Directory.CreateEmpty(path);

        Assert.True(Directory.Exists(path));
        Assert.Empty(Directory.EnumerateFileSystemEntries(path));
    }
}
