# IOHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/IOHelpers.cs)

Small file-system helpers that don't throw when there's nothing to do.

## API

| Member | Description |
|---|---|
| `CreateDirectoryIfNotExists(string directory)` | Creates the directory (and parents) if missing. |
| `DeleteFileIfExists(string file)` | Deletes the file if it exists. |
| `DeleteDirectoryIfExists(string directory)` | Deletes the directory recursively if it exists. |
| `bool CleanDirectory(string path, bool forceOverride)` | Makes sure nothing is at `path`: true if the directory didn't exist or was deleted (only when `forceOverride`), false if it exists and `forceOverride` is false. |
| `bool CleanFile(string path, bool forceOverride)` | The same for a file. |
| `CopyDirectory(string source, string destination, IReadOnlyList<string>? excludedDirectories = null, IReadOnlyList<string>? excludedFiles = null)` | Copies a directory tree, skipping excluded entries (matched as in [`PathHelpers.PathIsInList`](PathHelpers.md)). |
| `ArchiveDirectory(string directoryToArchive, string archivePath, bool deleteAfter = false, int maxRetries = 10, int baseDelayMs = 100, int maxDelayMs = 3000)` | Zips a directory (replacing an existing archive), optionally deleting the source, retrying on locked files. |
| `string SanitizeFileName(string name)` | Replaces characters that aren't valid in file names on this platform with `_`, trims spaces and dots, collapses `__`; empty results become `"unnamed"`. |

## Example

```csharp
using Niddy.Helpers;

IOHelpers.CreateDirectoryIfNotExists(backupDir);
IOHelpers.CopyDirectory(projectDir, stagingDir, excludedDirectories: ["bin", "obj", ".git"]);
IOHelpers.ArchiveDirectory(stagingDir, Path.Combine(backupDir, "project.zip"), deleteAfter: true);

string fileName = IOHelpers.SanitizeFileName($"{title}: draft?.txt"); // "Report_ draft_.txt" on Windows
```

## See also

- [AtomicFile](../IO/AtomicFile.md) for writes that can't leave half a file behind.
