# Archive

`Niddy.Core.IO.Archives` · Niddy.Core · [source](../../../../src/Niddy.Core/IO/Archives/Archive.cs)

Creates, extracts and reads zip, tar and tar.gz archives using only what's built into .NET (`System.IO.Compression` and `System.Formats.Tar`). You can archive a file or folder, extract all or part of an archive, and extract, read or stream a single file from one. Extraction checks every entry's path, so it's safe to point at archives you didn't make, and it can cap how much it writes.

## API

Every method has a `string archivePath` form and a `Stream archive` form, plus an `…Async` twin of each that takes a trailing `CancellationToken cancellationToken = default`. A stream passed in is left open. The table shows the path forms.

| Member | Description |
|---|---|
| `ArchiveFormat? FormatFromPath(string path)` | The format a path's extension names (`.zip`, `.tar`, `.tar.gz`, `.tgz`, in any case), or null. |
| `Create(string source, string archivePath, ArchiveCreateOptions? options = null)` | Archives a file or folder. Written to a temporary file and moved into place, so a failure never leaves a partial archive. |
| `Create(string source, Stream destination, ArchiveCreateOptions options)` | Writes the archive to a stream. `options.Format` is required. |
| `Extract(string archivePath, string destinationDirectory, ArchiveExtractOptions? options = null)` | Extracts everything, or the entries `options.Filter` accepts. See [Extracting](#extracting). |
| `ExtractEntry(string archivePath, string entryName, string destinationFile, bool overwrite = false, ArchiveFormat? format = null, long? maxBytes = null)` | Extracts one file, through a temporary file, keeping its last-write time. |
| `Stream OpenEntry(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null)` | Opens one file for reading without extracting it. Dispose the stream to close the archive. |
| `Stream? TryOpenEntry(…)` | The same, but returns null when there's no such file. |
| `byte[] ReadEntryBytes(string archivePath, string entryName, ArchiveFormat? format = null, long? maxBytes = null)` | Reads one file into memory. |
| `string ReadEntryText(string archivePath, string entryName, Encoding? encoding = null, ArchiveFormat? format = null, long? maxBytes = null)` | Reads one file as text, UTF-8 by default. |
| `bool ContainsEntry(string archivePath, string entryName, ArchiveFormat? format = null)` | Whether the archive has that file. |
| `IReadOnlyList<ArchiveEntryInfo> GetEntries(string archivePath, ArchiveFormat? format = null)` | The files and directories in the archive, in archive order. Links and special entries are left out. |

The async forms return `Task`, `Task<Stream>`, `Task<Stream?>`, `Task<byte[]>`, `Task<string>`, `Task<bool>` and `Task<IReadOnlyList<ArchiveEntryInfo>>` respectively, and only use async I/O.

`OpenEntry`, `ReadEntryBytes`, `ReadEntryText` and `ExtractEntry` throw `FileNotFoundException` when the archive has no file named `entryName`; a directory or link entry doesn't count. `maxBytes` caps the decompressed size; going past it throws `InvalidDataException`. A corrupt archive, or one that isn't a zip, tar or tar.gz, throws `InvalidDataException`.

The stream `OpenEntry` returns reads forwards only and has no `Length` (the size is in `GetEntries`). For a stream argument, leave the archive stream alone until the entry stream is disposed.

## Types

### ArchiveFormat

| Value | |
|---|---|
| `Zip` | `.zip`. Entries can be read in any order, so reading one entry is cheap. |
| `Tar` | `.tar`, uncompressed. Entries are read in order. |
| `TarGz` | `.tar.gz` or `.tgz`. Entries are read in order. |

### ArchiveCreateOptions

A record with `init` properties.

| Property | Default | |
|---|---|---|
| `ArchiveFormat? Format` | null | Null takes it from the archive path's extension. Required when writing to a stream. |
| `CompressionLevel CompressionLevel` | `Optimal` | For `Zip` and `TarGz`. |
| `bool IncludeBaseDirectory` | false | Put a folder's contents under a top-level folder of the same name (`data/a.txt`) instead of at the root (`a.txt`). Ignored for a single file. |
| `bool Overwrite` | false | Replace an existing archive. Without it, an existing archive is an `IOException`. |
| `IReadOnlyList<string>? ExcludedDirectories` | null | Folders to leave out with everything in them, as [`.gitignore`-style patterns](../FileSystemExtensions.md#matching-lists) relative to the source: `"bin"` is every folder named `bin`, `"/bin"` only the top-level one, `"docs/drafts"` that folder only. |
| `IReadOnlyList<string>? ExcludedFiles` | null | Files to leave out, matched the same way: `"*.log"` is every log file and `".DS_Store"` every file named `.DS_Store`. |
| `IProgress<ArchiveProgress>? Progress` | null | See [Progress](#progress). |

### ArchiveExtractOptions

A record with `init` properties.

| Property | Default | |
|---|---|---|
| `ArchiveFormat? Format` | null | Null takes it from the path's extension, or else from the archive's first bytes. |
| `bool Overwrite` | false | Replace files that already exist. Without it, an existing file is an `IOException`. |
| `Func<ArchiveEntryInfo, bool>? Filter` | null | Extract only the entries this returns true for. |
| `int? MaxEntries` | null | The most entries to extract; more is an `InvalidDataException`. |
| `long? MaxTotalBytes` | null | The most uncompressed bytes to write, counted as they're written rather than trusted from the headers; more is an `InvalidDataException`. |
| `IProgress<ArchiveProgress>? Progress` | null | See [Progress](#progress). |

### ArchiveEntryInfo

`record ArchiveEntryInfo(string Name, long Length, DateTimeOffset LastModified, bool IsDirectory)`. `Name` is the entry's normalized path (see [Entry names](#entry-names)), `Length` the uncompressed size the archive states (0 for a directory).

### ArchiveProgress

`readonly record struct ArchiveProgress(int EntriesDone, int? EntriesTotal, long BytesDone, long? BytesTotal, string? CurrentEntry)`. The totals are null when extracting a tar or tar.gz, since finding them would take an extra pass.

## Formats

An explicit format always wins. Otherwise the format comes from the path's extension (`FormatFromPath`), and failing that, when reading, from the archive's first bytes: `PK` for zip, the gzip header for tar.gz, `ustar` for tar. A stream is only sniffed if it can seek; a non-seekable stream with no format is an `ArgumentException`. `Create` to a stream always needs `Format`.

Zip keeps a directory at the end of the file, so reading one entry jumps straight to it. Tar and tar.gz have no index: `OpenEntry`, `ContainsEntry` and the other single-entry methods scan from the start until they find the entry, which for a large tar.gz means decompressing everything before it. Prefer zip when you'll often read single files.

A zip read from a non-seekable stream is buffered in memory by `ZipArchive` first.

## Entry names

Entry names use `/` and are relative to the archived folder: `docs/readme.md`. When looking an entry up, both your name and the archive's are normalized (`\` becomes `/`, a leading `./` or `/` and a trailing `/` are dropped) and compared ordinally, case-sensitively. So `"docs\\readme.md"` and `"./docs/readme.md"` both find `docs/readme.md`.

## Creating

- A folder's files and subfolders are added in ordinal order, each folder (empty or not) with its own directory entry. Hidden files are included.
- A symbolic link to a file is archived as the file's contents. A linked folder is skipped, which also avoids loops.
- File and folder last-write times are stored, and on Linux and macOS so are Unix permissions.
- A single file becomes one entry named after it.
- With a path target, the archive's folder is created if missing. An archive path inside the source is an `ArgumentException`, a missing source a `FileNotFoundException`.
- Tar archives are written in PAX format.

## Extracting

If `destinationDirectory` doesn't exist, the archive is extracted into a temporary sibling folder that's moved into place at the end, so a failure (including cancellation or a limit) leaves nothing behind except the destination's parent folder. Extracting into a folder that already exists isn't atomic: files written before a failure stay.

Files keep their last-write times, and on Linux and macOS their permissions (the owner can always read and write). Folders keep their last-write times too.

### Security

- An entry with an absolute name or a `..` segment is an `IOException`, as is any entry whose full path would land outside the destination. Nothing is stripped or rewritten.
- A tar symbolic link is recreated only if its target resolves inside the destination; a target that escapes, including through a link extracted earlier, is an `IOException`. On Windows, creating a symbolic link needs Developer Mode or admin rights.
- A tar hard link is extracted as a copy of a file already extracted inside the destination.
- Device and FIFO entries are skipped.

The defaults have no limits. For an archive you didn't make (a download, an upload, a plugin), set `MaxEntries` and `MaxTotalBytes`, or `maxBytes` for the single-entry methods, so a "zip bomb" can't fill the disk or memory.

## Progress

`Create` and `Extract` report after each entry and after every MiB within one. For `Create` and zip `Extract` the totals are known from the start; for tar they're null. `Progress<T>` posts each report to the context it was created on, so reports arrive asynchronously and may still be arriving after the method returns.

## Examples

```csharp
using Niddy.Core.IO.Archives;

// Archive a project, leaving out build output, with a progress bar
await Archive.CreateAsync(projectDir, Path.Combine(exportDir, "project.zip"), new ArchiveCreateOptions
{
    ExcludedDirectories = ["bin", "obj"],
    ExcludedFiles = [".DS_Store"],
    Overwrite = true,
    Progress = new Progress<ArchiveProgress>(p => progressBar.Value = p.BytesDone * 100.0 / (p.BytesTotal ?? 1)),
}, ct);

// Unpack a downloaded plugin, with limits
await Archive.ExtractAsync(downloadPath, pluginDir, new ArchiveExtractOptions
{
    MaxEntries = 10_000,
    MaxTotalBytes = 500L * 1024 * 1024,
}, ct);

// Read one file without unpacking anything
string json = Archive.ReadEntryText(pluginPath, "config.json", maxBytes: 1024 * 1024);

// Stream one file
await using (Stream data = await Archive.OpenEntryAsync(backupPath, "data/records.csv", cancellationToken: ct))
{
    await ImportAsync(data, ct);
}

// Extract only the JSON files
Archive.Extract(archivePath, outputDir, new ArchiveExtractOptions
{
    Filter = entry => entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase),
});
// Folders the JSON files are in are created as needed; other (empty) folders aren't.
```

## See also

- [BackupExtensions](../Backups/BackupExtensions.md): `Directory.Backup(archive: …)` makes timestamped archive backups of a folder.
- [FileSystemExtensions](../FileSystemExtensions.md#matching-lists) for how `ExcludedDirectories` and `ExcludedFiles` are matched.
- [AtomicFile](../AtomicFile.md), which `Create` and `ExtractEntry` write through.
