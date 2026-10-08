# BackupExtensions

`Niddy.Core.IO.Backups` · Niddy.Core · [source](../../../../src/Niddy.Core/IO/Backups/BackupExtensions.cs)

C# 14 static extensions that make uniquely named backups. `File.Backup` copies a file, and `Directory.Backup` copies a directory or archives it. Both can prune old backups as new ones are made. Add `using Niddy.Core.IO.Backups;` to use them.

## API

| Member | Description |
|---|---|
| `string? File.Backup(string filePath, BackupNaming naming = BackupNaming.Timestamp, string? backupDirectory = null, int? keep = null, string label = "backup", string? extension = null, TimeProvider? timeProvider = null)` | Copies a file to a uniquely named backup such as `report.backup-20260928T140312Z.txt`, and can prune old backups. Returns the backup's full path, or null if the file doesn't exist. |
| `string? Directory.Backup(string directory, BackupNaming naming = BackupNaming.Timestamp, string? backupDirectory = null, int? keep = null, ArchiveFormat? archive = null, string label = "backup", string? extension = null, TimeProvider? timeProvider = null)` | The same for a directory. It makes a copy (`data.backup-…`), or with `archive`, an archive in that [`ArchiveFormat`](../Archives/Archive.md#archiveformat) (`data.backup-….zip`, `.tar` or `.tar.gz`). |

## BackupNaming

Every backup is named `{name}.{label}-{token}{extension}`. That keeps backups sorted next to their source, keeps their extension (a backed-up `.txt` still opens in a text editor), and stops them being mistaken for other files. The token comes from `BackupNaming`:

| `BackupNaming` | Names | Notes |
|---|---|---|
| `Timestamp` (default) | `report.backup-20260928T140312Z.txt` | The UTC time in ISO 8601 basic format, so names sort chronologically and don't jump around with daylight saving. A second backup in the same second gets a counter: `…140312Z-2.txt`. |
| `Numbered` | `report.backup-1.txt`, `report.backup-2.txt` | One more than the highest existing number. Numbers aren't reused after old backups are deleted. |

## Details

- **Names.** A file's extension is its last one: `archive.tar.gz` becomes `archive.tar.backup-1.gz`. A name that's all extension (`.gitignore`) is kept whole, and so is every directory name (`my.app`): `.gitignore.backup-1`, `my.app.backup-1`. `label` replaces `backup` (for example `"pre-migration"`) and must be a valid file name.
- **A new extension.** Pass `extension` (such as `".bak"`; the dot is optional) to end every backup with it instead. A file's whole name then goes before the marker: `report.txt.backup-1.bak`, `.gitignore.backup-1.bak`. `""` ends the name with the token: `report.txt.backup-1`. For a directory, `extension` replaces the default, which is nothing for a copy and `.zip`, `.tar` or `.tar.gz` for an archive: `data.backup-1.bak`.
- **Never overwrites.** A file or archive backup is created with `FileMode.CreateNew`; if another process takes the name first, the next free name is used. A copied directory takes the first name that's free when the copy starts.
- **Where.** Next to the source, or in `backupDirectory`, which is created if missing. A directory can't be backed up into itself. Paths are compared with [`Path.Comparison`](../FileSystemExtensions.md#comparing-paths), so on Windows and macOS `Data/` can't be backed up into `DATA/backups` either.
- **Files in use.** The source file is read with `FileShare.ReadWrite`, so a log that another stream is appending to can still be backed up. The backup keeps the source's last-write time.
- **Failures.** If the copy or archive fails, the partial backup is deleted and the exception is rethrown.
- **Keeping N.** With `keep`, the oldest backups are deleted after the new one is made, until `keep` are left, counting the new one.
  - Only backups of the same source, in the same place, with the same `label`, `naming` and `extension` are counted; for directories, the `archive` format must match too. Nothing else is touched.
  - Names are matched with `Path.Comparison` as well, so on Windows and macOS backups made as `Data` and as `data` count as one set.
  - Deletes use [`File.DeleteWithRetry` / `Directory.DeleteWithRetry`](../FileSystemExtensions.md#safe-deletes) without throwing, so a backup that's still locked is left for next time.
- **Tests.** Pass a `FakeTimeProvider` as `timeProvider` to get predictable `Timestamp` names.

## Example

```csharp
using Niddy.Core.IO;
using Niddy.Core.IO.Archives;
using Niddy.Core.IO.Backups;

// Back up before overwriting; keep the last 5 (returns null if there's no file yet)
File.Backup(settingsPath, keep: 5);
// …or number them and end with .bak: settings.json.backup-1.bak, settings.json.backup-2.bak, …
// File.Backup(settingsPath, BackupNaming.Numbered, keep: 5, extension: ".bak");
AtomicFile.WriteAllText(settingsPath, json);

// A zipped snapshot of a folder before a migration, in a backups folder
string? snapshot = Directory.Backup(paths.Data, BackupNaming.Numbered,
    backupDirectory: Path.Combine(paths.Cache, "backups"), archive: ArchiveFormat.Zip, label: "pre-migration");
```

## See also

- [FileSystemExtensions](../FileSystemExtensions.md) for safe deletes, copies and path matching.
- [Archive](../Archives/Archive.md) to create, extract and read archives.
- [AtomicFile](../AtomicFile.md) for writes that can't leave half a file behind.
