namespace Niddy.Core.IO.Backups;

/// <summary>
/// How <c>File.Backup</c> and <c>Directory.Backup</c> name backups. Both keep the
/// original name and extension around a <c>.{label}-</c> marker, so backups sort next to their source, open in the
/// same app, and can't be mistaken for other files. (Pass an <c>extension</c> such as <c>.bak</c> to end the name
/// with that instead.)
/// </summary>
public enum BackupNaming
{
    /// <summary>
    /// The UTC time of the backup, in ISO 8601 basic format: <c>report.backup-20260928T140312Z.txt</c>. Names sort
    /// chronologically. A second backup in the same second gets a counter: <c>report.backup-20260928T140312Z-2.txt</c>.
    /// </summary>
    Timestamp,

    /// <summary>
    /// One more than the highest existing backup number: <c>report.backup-1.txt</c>, <c>report.backup-2.txt</c>, and
    /// so on. Numbers aren't reused after older backups are deleted.
    /// </summary>
    Numbered,
}
