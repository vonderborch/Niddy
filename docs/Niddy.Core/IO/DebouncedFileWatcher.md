# DebouncedFileWatcher

`Niddy.IO` · Niddy.Core · [source](../../../src/Niddy.Core/IO/DebouncedFileWatcher.cs)

Watches a directory or file and reports changes once they settle. `FileSystemWatcher` raises several events for one save (and editors often write a temp file and rename it); this waits until no event has arrived for `Delay`, then raises `Changed` once with one merged change per path.

Events are raised on the `SynchronizationContext` that was current when the watcher was created (e.g. the UI thread), or the thread pool. Watching starts immediately.

## API

| Member | Description |
|---|---|
| `DebouncedFileWatcher(string directory, string filter = "*", bool includeSubdirectories = false, TimeSpan? delay = null, bool captureContext = true)` | Watches a directory, which must exist. |
| `static DebouncedFileWatcher ForFile(string filePath, TimeSpan? delay = null, bool captureContext = true)` | Watches one file (its directory, filtered to its name). |
| `static readonly TimeSpan DefaultDelay` | 250 ms. |
| `Directory`, `Filter`, `Delay` | What is watched and how long to wait. |
| `bool Enabled` | True from creation; set false to pause. |
| `NotifyFilters NotifyFilter` | Kinds of change; defaults to names, writes, sizes and creation times. |
| `event EventHandler<FileChangesEventArgs> Changed` | `e.Changes` is an `IReadOnlyList<FileChange>`. |
| `event EventHandler<ErrorEventArgs> Error` | E.g. `InternalBufferOverflowException` when the OS dropped events; rescan. |
| `Dispose()` | Stops watching and drops unreported changes. |

Supporting types:

- `sealed record FileChange(string FullPath, WatcherChangeTypes ChangeType, string? OldFullPath = null)`: `OldFullPath` is set for renames.
- `sealed class FileChangesEventArgs(IReadOnlyList<FileChange> changes) : EventArgs`.

## Examples

```csharp
using Niddy.IO;

// Reload a config file when it's edited in another program
using var watcher = DebouncedFileWatcher.ForFile(configPath);
watcher.Changed += (_, _) => ReloadConfig();

// Refresh a library view when images are added or removed
using var images = new DebouncedFileWatcher(libraryDir, "*.png", includeSubdirectories: true,
    delay: TimeSpan.FromMilliseconds(500));
images.Changed += (_, e) =>
{
    foreach (var change in e.Changes)
        library.Refresh(change.FullPath, change.ChangeType);
};
images.Error += (_, _) => library.RescanAll();
```

## See also

- [SettingsStore.Watch](../Settings/SettingsStore.md) uses this to reload settings.
- [Debouncer](../Threading/Debouncer.md), the general-purpose debouncer underneath.
