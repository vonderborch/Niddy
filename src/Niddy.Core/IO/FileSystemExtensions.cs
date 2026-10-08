using System.IO.Enumeration;
using Niddy.Core.Resilience;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.IO;

/// <summary>
/// Static extensions on <see cref="File"/>, <see cref="Directory"/> and <see cref="Path"/>: deletes that ride out
/// locked and read-only files, copying a directory tree, emptying a directory, matching and comparing paths, and
/// cleaning up file names. Call them on the types they extend, e.g. <c>Directory.DeleteWithRetry(dir)</c> or
/// <c>Path.Comparison</c>.
/// </summary>
public static class FileSystemExtensions
{
    extension(File)
    {
        /// <summary>
        /// Deletes a file, retrying while it is locked or read-only. Does nothing if no file exists at
        /// <paramref name="filePath"/>.
        /// </summary>
        /// <remarks>
        /// Only <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/> are retried; other
        /// exceptions (such as an invalid path) are thrown straight away. With the defaults, a file that
        /// stays locked is given up on after about 1.5 seconds.
        /// </remarks>
        /// <param name="filePath">The file to delete.</param>
        /// <param name="maxAttempts">How many times to try the delete before giving up (at least 1).</param>
        /// <param name="baseDelayMs">Wait before the second attempt, in milliseconds. It doubles after each
        /// failed attempt.</param>
        /// <param name="maxDelayMs">The longest wait between two attempts, in milliseconds.</param>
        /// <param name="clearReadOnly">After a failed attempt, clear the file's read-only attribute.</param>
        /// <param name="releaseHandles">After a failed attempt, run a full garbage collection and pending
        /// finalizers. This closes handles this process leaked (undisposed streams or file watchers); handles
        /// held by other processes can't be closed.</param>
        /// <param name="throwOnFailure">Throw the last error when every attempt fails. When false, return
        /// false instead.</param>
        /// <returns>True if the file is gone (deleted, or never there); false if it is still there and
        /// <paramref name="throwOnFailure"/> is false.</returns>
        /// <exception cref="MaxRetriesException">Every attempt failed, and <paramref name="throwOnFailure"/> is true.
        /// <see cref="Exception.InnerException"/> is the last <see cref="IOException"/> or
        /// <see cref="UnauthorizedAccessException"/>.</exception>
        public static bool DeleteWithRetry(
            string filePath,
            int maxAttempts = 5,
            int baseDelayMs = 100,
            int maxDelayMs = 2000,
            bool clearReadOnly = true,
            bool releaseHandles = true,
            bool throwOnFailure = true)
        {
            return DeleteWithRetries(
                filePath, isDirectory: false, recursive: false,
                maxAttempts, baseDelayMs, maxDelayMs, clearReadOnly, releaseHandles, throwOnFailure);
        }
    }

    extension(Directory)
    {
        /// <summary>
        /// Deletes a directory, retrying while something in it is locked or read-only. Does nothing if no
        /// directory exists at <paramref name="directory"/>.
        /// </summary>
        /// <remarks>
        /// Only <see cref="IOException"/> and <see cref="UnauthorizedAccessException"/> are retried; other
        /// exceptions (such as an invalid path) are thrown straight away. Each attempt carries on from
        /// wherever the previous one stopped. Symbolic links and junctions inside the directory are removed
        /// without touching what they point to.
        /// </remarks>
        /// <param name="directory">The directory to delete.</param>
        /// <param name="recursive">Delete the directory's contents too. When false, a directory that isn't
        /// empty fails like a locked one.</param>
        /// <param name="maxAttempts">How many times to try the delete before giving up (at least 1).</param>
        /// <param name="baseDelayMs">Wait before the second attempt, in milliseconds. It doubles after each
        /// failed attempt.</param>
        /// <param name="maxDelayMs">The longest wait between two attempts, in milliseconds.</param>
        /// <param name="clearReadOnly">After a failed attempt, clear the read-only attribute on the directory
        /// and (when <paramref name="recursive"/>) everything in it. On Windows, read-only files block a
        /// delete; on Linux and macOS, read-only directories do.</param>
        /// <param name="releaseHandles">After a failed attempt, run a full garbage collection and pending
        /// finalizers. This closes handles this process leaked (undisposed streams or file watchers); handles
        /// held by other processes can't be closed.</param>
        /// <param name="throwOnFailure">Throw the last error when every attempt fails. When false, return
        /// false instead.</param>
        /// <returns>True if the directory is gone (deleted, or never there); false if it is still there and
        /// <paramref name="throwOnFailure"/> is false. A failed delete may still have removed some of the
        /// contents.</returns>
        /// <exception cref="MaxRetriesException">Every attempt failed, and <paramref name="throwOnFailure"/> is true:
        /// something in the directory stayed locked, access was denied, or the directory isn't empty and
        /// <paramref name="recursive"/> is false. <see cref="Exception.InnerException"/> is the last
        /// <see cref="IOException"/> or <see cref="UnauthorizedAccessException"/>.</exception>
        public static bool DeleteWithRetry(
            string directory,
            bool recursive = true,
            int maxAttempts = 5,
            int baseDelayMs = 100,
            int maxDelayMs = 2000,
            bool clearReadOnly = true,
            bool releaseHandles = true,
            bool throwOnFailure = true)
        {
            return DeleteWithRetries(
                directory, isDirectory: true, recursive,
                maxAttempts, baseDelayMs, maxDelayMs, clearReadOnly, releaseHandles, throwOnFailure);
        }

        /// <summary>
        /// Makes sure <paramref name="directory"/> exists and is empty: an existing directory is deleted first, with
        /// <c>Directory.DeleteWithRetry</c>'s defaults, then the directory (and any missing parents) is created.
        /// </summary>
        /// <param name="directory">The directory.</param>
        /// <exception cref="MaxRetriesException">The existing directory couldn't be deleted.</exception>
        public static void CreateEmpty(string directory)
        {
            Directory.DeleteWithRetry(directory);
            Directory.CreateDirectory(directory);
        }

        /// <summary>
        /// Recursively copies a directory, optionally excluding specific subdirectories and files
        /// (relative to <paramref name="source"/>). Existing files in <paramref name="destination"/> are overwritten.
        /// </summary>
        /// <param name="source">The directory to copy.</param>
        /// <param name="destination">Where to copy it to. Created if missing.</param>
        /// <param name="excludedDirectories">Directories to skip with everything in them, as <c>.gitignore</c>-style
        /// patterns relative to <paramref name="source"/> (see <c>Path.IsInList</c>): <c>bin</c> is every folder named
        /// <c>bin</c>, <c>/bin</c> only the top-level one.</param>
        /// <param name="excludedFiles">Files to skip, matched the same way: <c>*.log</c> is every log file.</param>
        public static void Copy(
            string source,
            string destination,
            IReadOnlyList<string>? excludedDirectories = null,
            IReadOnlyList<string>? excludedFiles = null)
        {
            CopyDirectoryInternal(
                source, source, destination,
                excludedDirectories ?? [],
                excludedFiles ?? []);
        }
    }

    extension(Path)
    {
        /// <summary>
        /// How to compare paths on this platform: <see cref="StringComparison.OrdinalIgnoreCase"/> on Windows, macOS and
        /// Mac Catalyst, whose file systems ignore case by default, and <see cref="StringComparison.Ordinal"/> everywhere
        /// else. Based on <see cref="Platform.Info"/>.
        /// </summary>
        /// <remarks>
        /// It's a default, not a guarantee: a macOS volume can be formatted case-sensitive and a Windows folder can be made
        /// case-sensitive. Use it for matching paths, not for deciding whether two names can live side by side.
        /// </remarks>
        /// <exception cref="UnsupportedOperatingSystemException">
        /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
        /// </exception>
        public static StringComparison Comparison => PathComparisonFor(Platform.Info.OS);

        /// <summary>
        /// Whether <paramref name="path"/>, or a directory it's in, matches one of <paramref name="patterns"/>, which use
        /// <c>.gitignore</c>-style rules relative to <paramref name="rootPath"/>. Useful for ignore lists.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><c>*</c> matches any run of characters within a name, <c>?</c> one character, and <c>**</c> any number
        /// of directories: <c>*.log</c>, <c>build-??</c>, <c>docs/**/drafts</c>.</item>
        /// <item>A pattern without a <c>/</c> matches a name at any depth: <c>bin</c> matches <c>bin</c> and
        /// <c>src/bin</c>. A pattern with a <c>/</c> at the start or in the middle is relative to
        /// <paramref name="rootPath"/>: <c>/bin</c> and <c>docs/drafts</c> match only there.</item>
        /// <item>A pattern ending in <c>/</c> matches only directories.</item>
        /// <item>When a directory matches, so does everything inside it.</item>
        /// <item>Either separator works in a pattern. Names are compared with <c>Path.Comparison</c>.</item>
        /// </list>
        /// A path outside <paramref name="rootPath"/>, or the root itself, never matches. Blank patterns are ignored.
        /// </remarks>
        /// <param name="path">The path to test, absolute or relative to the current directory.</param>
        /// <param name="rootPath">The directory the patterns are relative to.</param>
        /// <param name="patterns">The patterns to match against.</param>
        public static bool IsInList(string path, string rootPath, IReadOnlyList<string> patterns)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentException.ThrowIfNullOrEmpty(rootPath);
            ArgumentNullException.ThrowIfNull(patterns);

            string relativePath = Path.GetRelativePath(rootPath, path);
            if (patterns.Count == 0 || relativePath == "." || Path.IsPathRooted(relativePath))
            {
                return false;
            }

            string[] segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            if (segments[0] == "..")
            {
                return false;
            }

            bool ignoreCase = Path.Comparison == StringComparison.OrdinalIgnoreCase;
            bool? pathIsDirectory = null;

            foreach (string pattern in patterns)
            {
                if (!TryParsePattern(pattern, out string[] patternSegments, out bool directoryOnly))
                {
                    continue;
                }

                // Test the path's parent directories first, then the path itself.
                for (int length = 1; length <= segments.Length; length++)
                {
                    if (directoryOnly && length == segments.Length)
                    {
                        pathIsDirectory ??= Directory.Exists(path);
                        if (!pathIsDirectory.Value)
                        {
                            continue;
                        }
                    }

                    if (SegmentsMatch(patternSegments, segments.AsSpan(0, length), ignoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Sanitizes a string for use as a file or directory name.
        /// Replaces invalid characters with underscores, trims dots and spaces, collapses double underscores.
        /// </summary>
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return "unnamed";
            }

            string sanitized = name;
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(c, '_');
            }

            sanitized = sanitized.Trim().Trim('.');

            while (sanitized.Contains("__"))
            {
                sanitized = sanitized.Replace("__", "_");
            }

            return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
        }
    }

    internal static StringComparison PathComparisonFor(OS os) => os switch
    {
        OS.Windows or OS.MacOS or OS.MacCatalyst => StringComparison.OrdinalIgnoreCase,
        _ => StringComparison.Ordinal,
    };

    /// <summary>
    /// Whether <paramref name="path"/> is <paramref name="directory"/> or somewhere under it. Both must be full paths
    /// without a trailing separator.
    /// </summary>
    internal static bool IsSameOrInside(string path, string directory, StringComparison comparison) =>
        path.Equals(directory, comparison)
        || (path.StartsWith(directory, comparison)
            && path.Length > directory.Length
            && (path[directory.Length] == Path.DirectorySeparatorChar || path[directory.Length] == Path.AltDirectorySeparatorChar));

    /// <summary>
    /// Splits an <c>IsInList</c> pattern into names. A pattern that isn't anchored to the root gets a leading
    /// <c>**</c>, so it matches at any depth. Returns false for a blank pattern.
    /// </summary>
    private static bool TryParsePattern(string pattern, out string[] segments, out bool directoryOnly)
    {
        string normalized = pattern.Trim().Replace('\\', '/');
        directoryOnly = normalized.EndsWith('/');
        normalized = normalized.TrimEnd('/');
        if (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        bool anchored = normalized.Contains('/');
        segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (!anchored)
        {
            segments = ["**", .. segments];
        }

        return true;
    }

    private static bool SegmentsMatch(ReadOnlySpan<string> pattern, ReadOnlySpan<string> path, bool ignoreCase)
    {
        if (pattern.IsEmpty)
        {
            return path.IsEmpty;
        }

        if (pattern[0] == "**")
        {
            for (int skip = 0; skip <= path.Length; skip++)
            {
                if (SegmentsMatch(pattern[1..], path[skip..], ignoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        return !path.IsEmpty
            && FileSystemName.MatchesSimpleExpression(pattern[0], path[0], ignoreCase)
            && SegmentsMatch(pattern[1..], path[1..], ignoreCase);
    }

    private static void CopyDirectoryInternal(
        string source,
        string rootSource,
        string destination,
        IReadOnlyList<string> excludedDirectories,
        IReadOnlyList<string> excludedFiles)
    {
        Directory.CreateDirectory(destination);

        foreach (string file in Directory.GetFiles(source))
        {
            if (!Path.IsInList(file, rootSource, excludedFiles))
            {
                string destPath = Path.Combine(destination, Path.GetFileName(file));
                SafeCopyFile(file, destPath);
            }
        }

        foreach (string dir in Directory.GetDirectories(source))
        {
            if (!Path.IsInList(dir, rootSource, excludedDirectories))
            {
                string destPath = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectoryInternal(dir, rootSource, destPath, excludedDirectories, excludedFiles);
            }
        }
    }

    private static bool DeleteWithRetries(
        string path,
        bool isDirectory,
        bool recursive,
        int maxAttempts,
        int baseDelayMs,
        int maxDelayMs,
        bool clearReadOnly,
        bool releaseHandles,
        bool throwOnFailure)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(baseDelayMs);
        ArgumentOutOfRangeException.ThrowIfNegative(maxDelayMs);

        RetryOptions options = new()
        {
            MaxAttempts = maxAttempts,
            BaseDelay = TimeSpan.FromMilliseconds(baseDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(maxDelayMs),
            ShouldRetry = IsIOFailure,
            OnRetry = _ =>
            {
                if (clearReadOnly)
                {
                    ClearReadOnly(isDirectory ? new DirectoryInfo(path) : new FileInfo(path), recursive);
                }

                if (releaseHandles)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                return true;
            },
        };

        try
        {
            Retry.Execute(() =>
            {
                try
                {
                    if (isDirectory)
                    {
                        Directory.Delete(path, recursive);
                    }
                    else
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex) when (IsIOFailure(ex) && !Exists(path, isDirectory))
                {
                    // Missing to begin with, or something else deleted it in the meantime.
                }
            }, options);
            return true;
        }
        catch (MaxRetriesException) when (!throwOnFailure)
        {
            return false;
        }
    }

    private static bool IsIOFailure(Exception ex) => ex is IOException or UnauthorizedAccessException;

    private static readonly RetryOptions CopyRetry = new()
    {
        MaxAttempts = 3,
        BaseDelay = TimeSpan.FromMilliseconds(500),
        ShouldRetry = IsIOFailure,
    };

    private static bool Exists(string path, bool isDirectory) =>
        isDirectory ? Directory.Exists(path) : File.Exists(path);

    /// <summary>
    /// Best effort: entries that can't be read or changed are skipped, and links aren't followed.
    /// </summary>
    private static void ClearReadOnly(FileSystemInfo entry, bool recursive)
    {
        FileAttributes attributes;
        try
        {
            attributes = entry.Attributes;
            if (attributes.HasFlag(FileAttributes.ReadOnly))
            {
                entry.Attributes = attributes & ~FileAttributes.ReadOnly;
            }
        }
        catch (Exception ex) when (IsIOFailure(ex))
        {
            return;
        }

        if (!recursive || entry is not DirectoryInfo directory || attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return;
        }

        IEnumerable<FileSystemInfo> children;
        try
        {
            children = directory.GetFileSystemInfos();
        }
        catch (Exception ex) when (IsIOFailure(ex))
        {
            return;
        }

        foreach (FileSystemInfo child in children)
        {
            ClearReadOnly(child, recursive);
        }
    }

    private static void SafeCopyFile(string source, string destination)
    {
        if (source == destination)
        {
            return;
        }

        Retry.ExecuteAndRethrowLast(() => File.Copy(source, destination, true), CopyRetry);
    }
}
