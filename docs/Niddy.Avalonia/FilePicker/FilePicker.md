# FilePicker

`Niddy.Avalonia.FilePicker` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/FilePicker/FilePicker.cs)

Thin wrappers around Avalonia's `IStorageProvider`, for opening and saving files and picking folders. Every method takes a `Control` that's attached to a visual tree, so the window or view can be found. Otherwise it throws `InvalidOperationException`.

- The main methods return `IStorageFile`/`IStorageFolder` handles, which work on **every platform**. Read and write through `OpenReadAsync`/`OpenWriteAsync`, and dispose the handles when you're done.
- The `*Path` methods return local file-system paths instead. They're **desktop-only**: on Android, iOS and browser, picked items usually have no local path, so they return null or skip the item.

## API

| Method | Returns |
|---|---|
| `OpenFile(source, title = "Open File", fileTypes = null)` | `Task<IStorageFile?>`, null if cancelled |
| `OpenFiles(source, title = "Open Files", fileTypes = null)` | `Task<IReadOnlyList<IStorageFile>>`, empty if cancelled |
| `SaveAs(source, title = "Save As", suggestedFileName = null, fileTypes = null)` | `Task<IStorageFile?>` |
| `OpenFolder(source, title = "Select Folder")` | `Task<IStorageFolder?>` |
| `OpenFolders(source, title = "Select Folders")` | `Task<IReadOnlyList<IStorageFolder>>` |
| `OpenFilePath`, `OpenFilePaths`, `SaveAsPath`, `OpenFolderPath`, `OpenFolderPaths` | The same, as `string?` / `IReadOnlyList<string>` paths. Desktop only. |

`fileTypes` is an `IReadOnlyList<FilePickerFileType>`. Avalonia's `FilePickerFileTypes` has common ones (`ImageAll`, `TextPlain`, `Pdf`, `All`…).

## Examples

Portable (works on mobile and browser too):

```csharp
using Niddy.Avalonia.FilePicker;

FilePickerFileType[] json = [new("JSON") { Patterns = ["*.json"], MimeTypes = ["application/json"] }];

using var file = await FilePicker.OpenFile(this, "Import project", json);
if (file is null) return;
await using (var stream = await file.OpenReadAsync())
    project = await JsonSerializer.DeserializeAsync<Project>(stream);

using var target = await FilePicker.SaveAs(this, "Export", suggestedFileName: "project.json", fileTypes: json);
if (target is not null)
{
    await using var stream = await target.OpenWriteAsync();
    await JsonSerializer.SerializeAsync(stream, project);
}
```

Desktop-only, when you need a path:

```csharp
if (await FilePicker.OpenFolderPath(this, "Choose a workspace") is { } folder)
    settings.Update(s => s.Workspace = folder);
```

## See also

- [examples/mobile-and-browser](../../examples/mobile-and-browser.md)
