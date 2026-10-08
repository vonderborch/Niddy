# FileSystemExtensions

`Niddy.Core.IO` · Niddy.Core · [source](../../../src/Niddy.Core/IO/FileSystemExtensions.cs)

C# 14 static extensions on `File`, `Directory` and `Path`. They include deletes that ride out locked and read-only files, emptying and copying directories, matching and comparing paths, and cleaning up file names. You call them on the type they extend, after `using Niddy.Core.IO;`, for example `Directory.DeleteWithRetry(dir)` or `Path.Comparison`. For backups see [BackupExtensions](Backups/BackupExtensions.md), and for archives see [Archive](Archives/Archive.md).

## API

| Member | Description |
|---|---|
| `bool File.DeleteWithRetry(string filePath, int maxAttempts = 5, int baseDelayMs = 100, int maxDelayMs = 2000, bool clearReadOnly = true, bool releaseHandles = true, bool throwOnFailure = true)` | Deletes a file if it exists, retrying while it's locked or read-only. See [Safe deletes](#safe-deletes). |
| `bool Directory.DeleteWithRetry(string directory, bool recursive = true, int maxAttempts = 5, int baseDelayMs = 100, int maxDelayMs = 2000, bool clearReadOnly = true, bool releaseHandles = true, bool throwOnFailure = true)` | The same for a directory. It's recursive by default. |
| `Directory.CreateEmpty(string directory)` | Makes sure the directory exists and is empty. An existing directory is deleted first with `Directory.DeleteWithRetry`, then the directory and any missing parents are created. To create a directory without emptying it, use the BCL's `Directory.CreateDirectory`. |
| `Directory.Copy(string source, string destination, IReadOnlyList<string>? excludedDirectories = null, IReadOnlyList<string>? excludedFiles = null)` | Copies a directory tree and skips excluded entries, which are [`.gitignore`-style patterns](#matching-lists) relative to `source`: `excludedDirectories: ["bin"]` skips every `bin` folder and everything in it, and `excludedFiles: ["*.log"]` every log file. Existing files are overwritten. A file copy that fails with an `IOException` or `UnauthorizedAccessException` is tried up to 3 times; if all 3 fail, the last exception is thrown. |
| `static StringComparison Path.Comparison { get; }` | How this platform's default file system compares paths. See [Comparing paths](#comparing-paths). |
| `bool Path.IsInList(string path, string rootPath, IReadOnlyList<string> patterns)` | Whether `path`, or a directory it's in, matches one of `patterns`: `.gitignore`-style patterns relative to `rootPath`. Useful for ignore lists. See [Matching lists](#matching-lists). |
| `string Path.SanitizeFileName(string name)` | Replaces characters that aren't valid in a file name on this platform with `_`, trims spaces and dots, and collapses `__`. An empty result becomes `"unnamed"`. |

## Safe deletes

`File.DeleteWithRetry` and `Directory.DeleteWithRetry` do nothing when nothing exists at the path. Otherwise they try the delete, and after each failed attempt they:

1. Clear the read-only attribute on the target, if `clearReadOnly` is set. For a recursive directory delete, that includes everything inside it. Read-only files block deletes on Windows (every file under `.git` is read-only), and read-only directories block them on Linux and macOS. Symbolic links and junctions aren't followed.
2. Run a full garbage collection and the pending finalizers, if `releaseHandles` is set. That closes file handles that *this* process leaked, such as a `FileStream` or `FileSystemWatcher` that was never disposed. Handles held by other processes (an editor, an antivirus scan, Explorer) can't be closed from here; the retries wait for those processes to let go.
3. Wait `baseDelayMs` and try again. The wait doubles after each attempt, up to `maxDelayMs`, for at most `maxAttempts` attempts in all. With the defaults, that's 5 attempts over about 1.5 seconds.

The retrying is done by [`Retry.Execute`](../Resilience/Retry.md), with steps 1 and 2 as its `OnRetry`.

Only `IOException` and `UnauthorizedAccessException` are retried. Anything else, such as an invalid path, is thrown straight away. When every attempt fails, the method throws a [`MaxRetriesException`](../Resilience/Retry.md#exceptions) whose `InnerException` is the last failure and whose `Exceptions` lists every failure. With `throwOnFailure: false`, it returns `false` instead. `Directory.CreateEmpty` deletes with `Directory.DeleteWithRetry`, so it can throw the same exception. Both methods return `true` when the path is gone, even if something else deleted it in the meantime.

A directory delete that fails may still have removed part of the tree; the next attempt picks up where it stopped.

These methods run synchronously and sleep between attempts, so call them off the UI thread when a file might stay locked.

## Comparing paths

`Path.Comparison` is `OrdinalIgnoreCase` on Windows, macOS and Mac Catalyst, and `Ordinal` everywhere else. It's based on [`Platform.Info`](../SystemInfo/Platform.md), so it throws `UnsupportedOperatingSystemException` on an OS that wasn't declared.

Treat it as a default rather than a guarantee: a macOS volume can be case-sensitive, and so can a folder on Windows. Use it to match paths, not to decide whether two names can coexist. [`DebouncedFileWatcher`](DebouncedFileWatcher.md) uses it to merge events for the same file, and [backups](Backups/BackupExtensions.md) use it to recognize their own names.

## Matching lists

`Path.IsInList(path, rootPath, patterns)` uses the same pattern rules as `.gitignore`, relative to `rootPath`:

| Pattern | Matches |
|---|---|
| `bin` | A file or folder named `bin` at any depth: `bin`, `src/bin`. A pattern without a `/` matches a name anywhere. |
| `/bin`, `docs/drafts` | Only that path under the root. A `/` at the start or in the middle anchors a pattern to the root. |
| `*.log`, `build-??` | `*` matches any run of characters within a name and `?` one character. `*` doesn't cross a `/`. |
| `docs/**/drafts` | `**` matches any number of folders, including none: `docs/drafts`, `docs/a/b/drafts`. |
| `logs/` | Only a folder. A trailing `/` makes a pattern match directories only. |

- When a folder matches, so does everything in it: `bin` matches `src/bin/out.dll`.
- Either separator works in a pattern (`docs\drafts` is `docs/drafts`), and a leading `./` is ignored.
- Names are compared with [`Path.Comparison`](#comparing-paths), so matching ignores case on Windows and macOS.
- The root itself, and paths outside it, never match. Blank patterns are ignored. There's no `!` negation.

`Directory.Copy` tests every folder and file it visits. It doesn't go into an excluded folder, so excluding `bin` skips everything in it.

## Example

```csharp
using Niddy.Core.IO;

Directory.Copy(projectDir, stagingDir, excludedDirectories: ["bin", "obj", ".git"]);

string fileName = Path.SanitizeFileName($"{title}: draft?.txt"); // "Report_ draft_.txt" on Windows

Directory.DeleteWithRetry(checkoutDir);   // a git clone: read-only files on Windows
Directory.CreateEmpty(outputDir);         // start from an empty folder

// Clean-up that shouldn't fail the operation: try harder, but don't throw
if (!File.DeleteWithRetry(tempFile, maxAttempts: 10, maxDelayMs: 5000, throwOnFailure: false))
    logger.LogWarning("Couldn't delete {File}", tempFile);

// Paths
bool same = string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), Path.Comparison);
HashSet<string> seen = new(StringComparer.FromComparison(Path.Comparison));

string[] ignore = ["bin", "obj", "*.user", "Thumbs.db", "/docs/generated"];
foreach (string file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
{
    if (Path.IsInList(file, root, ignore))
        continue;
    Process(file);
}
```

The extension members sit beside the BCL's own `File`, `Directory` and `Path` members. If a future .NET release adds a member with the same name and signature, the BCL member wins and the call quietly switches to it.

## See also

- [BackupExtensions](Backups/BackupExtensions.md) for `File.Backup` and `Directory.Backup`.
- [Retry](../Resilience/Retry.md) to retry anything else with exponential backoff.
- [AtomicFile](AtomicFile.md) for writes that can't leave half a file behind.
- [Archive](Archives/Archive.md) to create, extract and read zip, tar and tar.gz archives.
