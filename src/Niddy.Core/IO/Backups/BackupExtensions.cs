using System.Globalization;
using Niddy.Core.IO.Archives;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.IO.Backups;

/// <summary>
/// Static extensions that make uniquely named backups: <c>File.Backup(path)</c> copies a file to
/// <c>report.backup-20260928T140312Z.txt</c>, and <c>Directory.Backup(dir)</c> copies or archives a directory. Old
/// backups can be pruned as new ones are made.
/// </summary>
public static class BackupExtensions
{
    extension(File)
    {
        /// <summary>
        /// Copies a file to a uniquely named backup next to it (or in <paramref name="backupDirectory"/>), for example
        /// <c>report.backup-20260928T140312Z.txt</c>. Does nothing if the file doesn't exist.
        /// </summary>
        /// <remarks>
        /// The copy never overwrites anything: if another process takes the chosen name first, the next free name is
        /// used. The source is opened with <see cref="FileShare.ReadWrite"/>, so a file another process is writing
        /// (such as a log) can still be backed up. The backup keeps the source's last-write time.
        /// </remarks>
        /// <param name="filePath">The file to back up.</param>
        /// <param name="naming">How the backup is named. See <see cref="BackupNaming"/>.</param>
        /// <param name="backupDirectory">Where to put the backup (created if missing). Null puts it next to the
        /// file.</param>
        /// <param name="keep">After the backup is made, delete the oldest backups of this file in the same place, with
        /// the same <paramref name="naming"/>, <paramref name="label"/> and <paramref name="extension"/>, until this many
        /// are left (at least 1). Null keeps them all. Clean-up is best effort: a backup that can't be deleted is left
        /// behind.</param>
        /// <param name="label">The marker between the name and the backup's timestamp or number. It must be a valid
        /// file name.</param>
        /// <param name="extension">The extension the backup ends with, such as <c>.bak</c> (the dot is optional). The
        /// file's whole name then goes before the marker: <c>report.txt.backup-1.bak</c>. An empty string ends the name
        /// with the token: <c>report.txt.backup-1</c>. Null keeps the file's own extension last:
        /// <c>report.backup-1.txt</c>.</param>
        /// <param name="timeProvider">The clock for <see cref="BackupNaming.Timestamp"/>. Null uses the system
        /// clock.</param>
        /// <returns>The backup's full path, or null if <paramref name="filePath"/> doesn't exist.</returns>
        /// <exception cref="UnsupportedOperatingSystemException">
        /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
        /// </exception>
        public static string? Backup(
            string filePath,
            BackupNaming naming = BackupNaming.Timestamp,
            string? backupDirectory = null,
            int? keep = null,
            string label = "backup",
            string? extension = null,
            TimeProvider? timeProvider = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            extension = ValidateBackupArguments(label, extension, keep);

            if (!File.Exists(filePath))
            {
                return null;
            }

            string source = Path.GetFullPath(filePath);
            string name = Path.GetFileName(source);
            (string stem, string suffix) = (Path.GetFileNameWithoutExtension(name), Path.GetExtension(name));
            if (extension is not null)
            {
                (stem, suffix) = (name, extension);
            }
            else if (stem.Length == 0)
            {
                (stem, suffix) = (name, "");   // ".gitignore" is all extension; keep it whole
            }

            string target = Path.GetFullPath(backupDirectory ?? Path.GetDirectoryName(source)!);
            Directory.CreateDirectory(target);

            BackupPattern pattern = new($"{stem}.{label}-", suffix, IsDirectory: false);
            string backup = CreateBackup(target, pattern, naming, timeProvider, path =>
            {
                FileStream output;
                try
                {
                    output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                }
                catch (IOException) when (File.Exists(path) || Directory.Exists(path))
                {
                    return false;   // another process took the name
                }

                try
                {
                    using (output)
                    using (FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    {
                        input.CopyTo(output);
                    }

                    File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(source));
                    return true;
                }
                catch
                {
                    File.DeleteWithRetry(path, maxAttempts: 1, throwOnFailure: false);
                    throw;
                }
            });

            if (keep is { } count)
            {
                PruneBackups(target, pattern, naming, count);
            }

            return backup;
        }
    }

    extension(Directory)
    {
        /// <summary>
        /// Copies a directory to a uniquely named backup next to it (or in <paramref name="backupDirectory"/>), for
        /// example <c>data.backup-20260928T140312Z</c>, or archives it to <c>data.backup-20260928T140312Z.zip</c> (or
        /// <c>.tar</c>, <c>.tar.gz</c>). Does
        /// nothing if the directory doesn't exist.
        /// </summary>
        /// <remarks>
        /// An archive never overwrites anything: if another process takes the chosen name first, the next free name is
        /// used. A copied directory takes the first name that's free when the copy starts. If the copy fails, the
        /// partial backup is deleted.
        /// </remarks>
        /// <param name="directory">The directory to back up.</param>
        /// <param name="naming">How the backup is named. See <see cref="BackupNaming"/>.</param>
        /// <param name="backupDirectory">Where to put the backup (created if missing). Null puts it next to the
        /// directory. It can't be inside <paramref name="directory"/>.</param>
        /// <param name="keep">After the backup is made, delete the oldest backups of this directory in the same place,
        /// with the same <paramref name="naming"/>, <paramref name="label"/>, <paramref name="archive"/> and
        /// <paramref name="extension"/>, until this many are left (at least 1). Null keeps them all. Clean-up is best
        /// effort: a backup that can't be deleted is left behind.</param>
        /// <param name="archive">Write an archive in this format (see <see cref="Archive"/>) instead of copying
        /// the directory. Null copies it.</param>
        /// <param name="label">The marker between the name and the backup's timestamp or number. It must be a valid
        /// file name.</param>
        /// <param name="extension">The extension the backup ends with, such as <c>.bak</c> (the dot is optional):
        /// <c>data.backup-1.bak</c>. Null means none for a copy, and <c>.zip</c>, <c>.tar</c> or <c>.tar.gz</c> for an archive.</param>
        /// <param name="timeProvider">The clock for <see cref="BackupNaming.Timestamp"/>. Null uses the system
        /// clock.</param>
        /// <returns>The backup's full path, or null if <paramref name="directory"/> doesn't exist.</returns>
        /// <exception cref="ArgumentException"><paramref name="directory"/> is a root directory, or
        /// <paramref name="backupDirectory"/> is inside it (compared with
        /// <c>Path.Comparison</c>, so case-insensitively on Windows and macOS).</exception>
        /// <exception cref="UnsupportedOperatingSystemException">
        /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
        /// </exception>
        public static string? Backup(
            string directory,
            BackupNaming naming = BackupNaming.Timestamp,
            string? backupDirectory = null,
            int? keep = null,
            ArchiveFormat? archive = null,
            string label = "backup",
            string? extension = null,
            TimeProvider? timeProvider = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(directory);
            extension = ValidateBackupArguments(label, extension, keep);

            if (!Directory.Exists(directory))
            {
                return null;
            }

            string source = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
            string name = Path.GetFileName(source);
            if (name.Length == 0)
            {
                throw new ArgumentException("A root directory can't be backed up next to itself.", nameof(directory));
            }

            string target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(backupDirectory ?? Path.GetDirectoryName(source)!));
            if (FileSystemExtensions.IsSameOrInside(target, source, Path.Comparison))
            {
                throw new ArgumentException("The backup can't go inside the directory being backed up.", nameof(backupDirectory));
            }

            Directory.CreateDirectory(target);

            // Directories have no extension: "my.app" is a name, not "my" + ".app".
            BackupPattern pattern = new($"{name}.{label}-", extension ?? DefaultArchiveExtension(archive), IsDirectory: archive is null);
            string backup = CreateBackup(target, pattern, naming, timeProvider, path =>
            {
                if (archive is { } format)
                {
                    FileStream output;
                    try
                    {
                        output = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
                    }
                    catch (IOException) when (File.Exists(path) || Directory.Exists(path))
                    {
                        return false;   // another process took the name
                    }

                    try
                    {
                        using (output)
                        {
                            Archive.WriteArchive(source, output, new ArchiveCreateOptions { Format = format });
                        }

                        return true;
                    }
                    catch
                    {
                        File.DeleteWithRetry(path, maxAttempts: 1, throwOnFailure: false);
                        throw;
                    }
                }

                try
                {
                    Directory.Copy(source, path);
                    return true;
                }
                catch
                {
                    Directory.DeleteWithRetry(path, maxAttempts: 2, baseDelayMs: 0, throwOnFailure: false);
                    throw;
                }
            });

            if (keep is { } count)
            {
                PruneBackups(target, pattern, naming, count);
            }

            return backup;
        }
    }

    private static string DefaultArchiveExtension(ArchiveFormat? archive) => archive switch
    {
        ArchiveFormat.Zip => ".zip",
        ArchiveFormat.Tar => ".tar",
        ArchiveFormat.TarGz => ".tar.gz",
        _ => "",
    };

    /// <summary>A backup's name is <c>{Prefix}{token}{Suffix}</c>, where the token is a number or a timestamp.</summary>
    private sealed record BackupPattern(string Prefix, string Suffix, bool IsDirectory)
    {
        public string Name(string token) => Prefix + token + Suffix;
    }

    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

    /// <returns><paramref name="extension"/> with a leading dot (unless it's null or empty).</returns>
    private static string? ValidateBackupArguments(string label, string? extension, int? keep)
    {
        ArgumentException.ThrowIfNullOrEmpty(label);
        if (label.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The label must be a valid file name.", nameof(label));
        }

        if (keep is { } count)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(count, 1, nameof(keep));
        }

        if (string.IsNullOrEmpty(extension))
        {
            return extension;
        }

        if (extension[0] != '.')
        {
            extension = "." + extension;
        }

        if (extension.Length == 1 || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The extension must be valid in a file name, such as \".bak\".", nameof(extension));
        }

        return extension;
    }

    private static string CreateBackup(
        string directory,
        BackupPattern pattern,
        BackupNaming naming,
        TimeProvider? timeProvider,
        Func<string, bool> tryCreate)
    {
        string stamp = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);
        long next = naming == BackupNaming.Numbered
            ? FindBackups(directory, pattern, naming).Select(b => b.Order.Primary).DefaultIfEmpty(0).Max() + 1
            : 1;

        // Bounded so a directory that keeps rejecting names can't spin forever.
        for (int i = 0; i < 10_000; i++, next++)
        {
            string token = naming switch
            {
                BackupNaming.Numbered => next.ToString(CultureInfo.InvariantCulture),
                _ => next == 1 ? stamp : $"{stamp}-{next.ToString(CultureInfo.InvariantCulture)}",
            };
            string path = Path.Combine(directory, pattern.Name(token));
            if (File.Exists(path) || Directory.Exists(path))
            {
                continue;
            }

            if (tryCreate(path))
            {
                return path;
            }
        }

        throw new IOException($"Couldn't find a free backup name in '{directory}'.");
    }

    private static void PruneBackups(string directory, BackupPattern pattern, BackupNaming naming, int keep)
    {
        foreach ((string path, _) in FindBackups(directory, pattern, naming).OrderByDescending(b => b.Order).Skip(keep))
        {
            if (pattern.IsDirectory)
            {
                Directory.DeleteWithRetry(path, throwOnFailure: false);
            }
            else
            {
                File.DeleteWithRetry(path, throwOnFailure: false);
            }
        }
    }

    /// <summary>
    /// The existing backups matching <paramref name="pattern"/> and <paramref name="naming"/>, with a key that sorts
    /// them oldest first: the number, or the time and then the same-second counter.
    /// </summary>
    private static List<(string Path, (long Primary, long Secondary) Order)> FindBackups(
        string directory, BackupPattern pattern, BackupNaming naming)
    {
        // Names match with the platform's casing: on macOS and Windows, "Data" and "data" are the same directory, so
        // backups made under either name count as one set.
        StringComparison comparison = Path.Comparison;
        EnumerationOptions options = new()
        {
            MatchCasing = comparison == StringComparison.Ordinal ? MatchCasing.CaseSensitive : MatchCasing.CaseInsensitive,
            AttributesToSkip = 0,   // the default skips hidden entries, i.e. backups of dot-files on Unix
            IgnoreInaccessible = false,
        };
        IEnumerable<string> entries = pattern.IsDirectory
            ? Directory.EnumerateDirectories(directory, pattern.Prefix + "*", options)
            : Directory.EnumerateFiles(directory, pattern.Prefix + "*", options);

        List<(string, (long, long))> backups = new();
        foreach (string path in entries)
        {
            string name = Path.GetFileName(path);
            if (!name.StartsWith(pattern.Prefix, comparison)
                || !name.EndsWith(pattern.Suffix, comparison)
                || name.Length <= pattern.Prefix.Length + pattern.Suffix.Length)
            {
                continue;
            }

            ReadOnlySpan<char> token = name.AsSpan(pattern.Prefix.Length, name.Length - pattern.Prefix.Length - pattern.Suffix.Length);
            if (TryParseBackupToken(token, naming, out (long, long) order))
            {
                backups.Add((path, order));
            }
        }

        return backups;
    }

    private static bool TryParseBackupToken(ReadOnlySpan<char> token, BackupNaming naming, out (long, long) order)
    {
        order = default;
        if (naming == BackupNaming.Numbered)
        {
            if (!IsDigits(token) || !long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out long number))
            {
                return false;
            }

            order = (number, 0);
            return true;
        }

        int stampLength = TimestampFormat.Length - 4;   // minus the four quote characters
        if (token.Length < stampLength
            || !DateTime.TryParseExact(token[..stampLength], TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out DateTime time))
        {
            return false;
        }

        long counter = 1L;
        ReadOnlySpan<char> rest = token[stampLength..];
        if (rest.Length > 0
            && (rest[0] != '-' || !IsDigits(rest[1..])
                || !long.TryParse(rest[1..], NumberStyles.None, CultureInfo.InvariantCulture, out counter)))
        {
            return false;
        }

        order = (time.Ticks, counter);
        return true;
    }

    private static bool IsDigits(ReadOnlySpan<char> text) => text.Length > 0 && !text.ContainsAnyExceptInRange('0', '9');
}
