namespace Niddy.Helpers;

/// <summary>
/// Helper methods for working with file system paths.
/// </summary>
public static class PathHelpers
{
    /// <summary>
    /// Returns true if <paramref name="path"/>, resolved relative to <paramref name="rootPath"/>,
    /// matches any entry in <paramref name="paths"/>.
    /// </summary>
    /// <param name="path">The absolute path to test.</param>
    /// <param name="rootPath">The root used to compute the relative path.</param>
    /// <param name="paths">The list of paths or patterns to match against.</param>
    /// <param name="checkWithWildcard">
    /// When true, entries prefixed with <c>*</c> match by filename only and entries
    /// suffixed with <c>*</c> match any path that starts with the given prefix.
    /// </param>
    /// <param name="checkEntryName">
    /// When true, also matches if the filename component of the relative path equals the entry.
    /// </param>
    public static bool PathIsInList(
        string path,
        string rootPath,
        IReadOnlyList<string> paths,
        bool checkWithWildcard = false,
        bool checkEntryName = false)
    {
        var relativePath = Path.GetRelativePath(rootPath, path);

        foreach (var originalEntry in paths)
        {
            // Normalize trailing slash
            var entry = originalEntry.EndsWith('/')
                ? originalEntry[..^1]
                : originalEntry;

            if (relativePath == entry)
                return true;

            if (checkWithWildcard && entry.Contains('*'))
            {
                var baseEntry = entry.Replace("*", "");

                if (entry.StartsWith('*') && Path.GetFileName(relativePath) == baseEntry)
                    return true;

                if (entry.EndsWith('*') && relativePath.StartsWith(baseEntry))
                    return true;
            }

            if (checkEntryName && Path.GetFileName(relativePath) == entry)
                return true;
        }

        return false;
    }
}
