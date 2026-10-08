using Niddy.Core.IO;

namespace Niddy.Core.Tests;

// Path.Comparison reads the platform state other tests change.
[Collection(PlatformStateCollection.Name)]
public sealed class PathIsInListTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("niddy-list-").FullName;

    public void Dispose() => Directory.DeleteWithRetry(_root, baseDelayMs: 0, throwOnFailure: false);

    private bool IsInList(string relativePath, params string[] patterns) =>
        Path.IsInList(Path.Combine([_root, .. relativePath.Split('/')]), _root, patterns);

    [Theory]
    [InlineData("bin", "bin")]
    [InlineData("src/bin", "bin")]
    [InlineData("src/bin/out.dll", "bin")]
    [InlineData("a.user", "*.user")]
    [InlineData("src/deep/a.user", "*.user")]
    [InlineData("src/Thumbs.db", "Thumbs.db")]
    [InlineData("build-01", "build-??")]
    [InlineData("bin", "/bin")]
    [InlineData("bin/out.dll", "/bin")]
    [InlineData("docs/drafts", "docs/drafts")]
    [InlineData("docs/drafts/x.md", "docs/drafts")]
    [InlineData("docs/generated-api/x.md", "docs/generated*")]
    [InlineData("docs/a/b/drafts/x.md", "docs/**/drafts")]
    [InlineData("docs/drafts/x.md", "docs/**/drafts")]
    [InlineData("docs/drafts/x.md", "./docs/drafts/")]
    [InlineData("docs/drafts", "docs\\drafts")]
    [InlineData("anything/at/all", "**")]
    public void Matches(string relativePath, string pattern) => Assert.True(IsInList(relativePath, pattern));

    [Theory]
    [InlineData("cabin", "bin")]
    [InlineData("src/bin", "/bin")]
    [InlineData("src/docs/drafts", "docs/drafts")]
    [InlineData("a.user.txt", "*.user")]
    [InlineData("build-001", "build-??")]
    [InlineData("docs/final.md", "docs/drafts")]
    [InlineData("docs/x/y.md", "docs/*.md")]
    [InlineData("a.txt", "")]
    [InlineData("a.txt", "   ")]
    public void DoesNotMatch(string relativePath, string pattern) => Assert.False(IsInList(relativePath, pattern));

    [Fact]
    public void MatchesAnyOfSeveralPatterns()
    {
        Assert.True(IsInList("obj/x", "bin", "obj"));
        Assert.False(IsInList("src/x", "bin", "obj"));
        Assert.False(IsInList("src/x"));
    }

    [Fact]
    public void TheRootAndPathsOutsideItNeverMatch()
    {
        Assert.False(Path.IsInList(_root, _root, ["**"]));
        Assert.False(Path.IsInList(Path.Combine(_root, "..", "other"), _root, ["**", "other"]));
    }

    [Fact]
    public void ATrailingSlashMatchesOnlyDirectories()
    {
        Directory.CreateDirectory(Path.Combine(_root, "logs"));
        File.WriteAllText(Path.Combine(_root, "cache"), "x");

        Assert.True(IsInList("logs", "logs/"));
        Assert.False(IsInList("cache", "cache/"));
        // A parent of the path is always a directory.
        Assert.True(IsInList("cache/x", "cache/"));
    }

    [Fact]
    public void CaseFollowsPathComparison()
    {
        bool ignoresCase = Path.Comparison == StringComparison.OrdinalIgnoreCase;

        Assert.Equal(ignoresCase, IsInList("BIN/out.dll", "bin"));
        Assert.Equal(ignoresCase, IsInList("a.USER", "*.user"));
    }

    [Fact]
    public void RejectsBadArguments()
    {
        Assert.Throws<ArgumentException>(() => Path.IsInList("", _root, []));
        Assert.Throws<ArgumentException>(() => Path.IsInList("a", "", []));
        Assert.Throws<ArgumentNullException>(() => Path.IsInList("a", _root, null!));
    }

    [Fact]
    public void DirectoryCopy_SkipsMatchesAtAnyDepth()
    {
        string source = Path.Combine(_root, "src");
        foreach (string file in new[] { "a.txt", "a.log", "bin/x.dll", "lib/bin/y.dll", "lib/keep.txt", "lib/b.log" })
        {
            string path = Path.Combine([source, .. file.Split('/')]);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "x");
        }

        string destination = Path.Combine(_root, "dest");
        Directory.Copy(source, destination, excludedDirectories: ["bin"], excludedFiles: ["*.log"]);

        Assert.Equal(
            ["a.txt", "lib/keep.txt"],
            Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(destination, file).Replace(Path.DirectorySeparatorChar, '/'))
                .Order(StringComparer.Ordinal));
    }
}
