using System.IO.Compression;

namespace Niddy.Helpers;

/// <summary>
/// Helper methods for file system operations.
/// </summary>
public static class IOHelpers
{
    /// <summary>
    /// Creates the directory if it does not already exist.
    /// </summary>
    public static void CreateDirectoryIfNotExists(string directory)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Deletes the file if it exists.
    /// </summary>
    public static void DeleteFileIfExists(string file)
    {
        if (File.Exists(file))
            File.Delete(file);
    }

    /// <summary>
    /// Deletes the directory and all its contents if it exists.
    /// </summary>
    public static void DeleteDirectoryIfExists(string directory)
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, true);
    }

    /// <summary>
    /// Deletes the directory if it exists and <paramref name="forceOverride"/> is true.
    /// </summary>
    /// <returns>True if the directory is clear (either didn't exist or was deleted), false if it
    /// exists and <paramref name="forceOverride"/> is false.</returns>
    public static bool CleanDirectory(string path, bool forceOverride)
    {
        if (!Directory.Exists(path))
            return true;

        if (!forceOverride)
            return false;

        Directory.Delete(path, true);
        return true;
    }

    /// <summary>
    /// Deletes the file if it exists and <paramref name="forceOverride"/> is true.
    /// </summary>
    /// <returns>True if the file is clear (either didn't exist or was deleted), false if it
    /// exists and <paramref name="forceOverride"/> is false.</returns>
    public static bool CleanFile(string path, bool forceOverride)
    {
        if (!File.Exists(path))
            return true;

        if (!forceOverride)
            return false;

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Recursively copies a directory, optionally excluding specific subdirectories and files
    /// (relative to <paramref name="source"/>).
    /// </summary>
    public static void CopyDirectory(
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

    private static void CopyDirectoryInternal(
        string source,
        string rootSource,
        string destination,
        IReadOnlyList<string> excludedDirectories,
        IReadOnlyList<string> excludedFiles)
    {
        CreateDirectoryIfNotExists(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            if (!PathHelpers.PathIsInList(file, rootSource, excludedFiles, checkWithWildcard: true))
            {
                var destPath = Path.Combine(destination, Path.GetFileName(file));
                SafeCopyFile(file, destPath);
            }
        }

        foreach (var dir in Directory.GetDirectories(source))
        {
            if (!PathHelpers.PathIsInList(dir, rootSource, excludedDirectories, checkWithWildcard: true))
            {
                var destPath = Path.Combine(destination, Path.GetFileName(dir));
                CopyDirectoryInternal(dir, rootSource, destPath, excludedDirectories, excludedFiles);
            }
        }
    }

    /// <summary>
    /// Archives a directory to a zip file. Optionally deletes the source directory afterward
    /// using exponential backoff retries.
    /// </summary>
    /// <param name="directoryToArchive">The directory to archive.</param>
    /// <param name="archivePath">The destination archive path.</param>
    /// <param name="deleteAfter">Whether to delete the source directory after archiving.</param>
    /// <param name="maxRetries">Maximum number of delete retry attempts.</param>
    /// <param name="baseDelayMs">Base delay in milliseconds for exponential backoff.</param>
    /// <param name="maxDelayMs">Maximum delay cap in milliseconds.</param>
    public static void ArchiveDirectory(
        string directoryToArchive,
        string archivePath,
        bool deleteAfter = false,
        int maxRetries = 10,
        int baseDelayMs = 100,
        int maxDelayMs = 3000)
    {
        DeleteFileIfExists(archivePath);
        ZipFile.CreateFromDirectory(directoryToArchive, archivePath);

        if (!deleteAfter)
            return;

        Retry.Execute(
            () => Directory.Delete(directoryToArchive, true),
            maxRetries,
            baseDelayMs,
            maxDelayMs);
    }

    /// <summary>
    /// Sanitizes a string for use as a file or directory name.
    /// Replaces invalid characters with underscores, trims dots and spaces, collapses double underscores.
    /// </summary>
    public static string SanitizeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "unnamed";

        var sanitized = name;
        foreach (var c in Path.GetInvalidFileNameChars())
            sanitized = sanitized.Replace(c, '_');

        sanitized = sanitized.Trim().Trim('.');

        while (sanitized.Contains("__"))
            sanitized = sanitized.Replace("__", "_");

        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    private static void SafeCopyFile(string source, string destination, int numTries = 3, int sleepMs = 500)
    {
        if (source == destination)
            return;

        for (var i = 0; i < numTries; i++)
        {
            try
            {
                File.Copy(source, destination, true);
                return;
            }
            catch
            {
                Thread.Sleep(sleepMs);
            }
        }

        throw new IOException($"Could not copy '{source}' to '{destination}' after {numTries} attempts.");
    }
}
