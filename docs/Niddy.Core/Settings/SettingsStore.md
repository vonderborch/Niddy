# SettingsStore\<T>

`Niddy.Settings` · Niddy.Core · [source](../../../src/Niddy.Core/Settings/SettingsStore.cs)

Typed settings kept in a JSON file. Derive a class that names the file, read with `Current`, and change with `Update`, which saves and raises `Changed`.

- The file is read the first time `Current` is used. A missing file gives `CreateDefault()`.
- A file that can't be read as `T` is renamed to `*.bad` (so it can be recovered) and the defaults are used.
- Saves are atomic ([`AtomicFile`](../IO/AtomicFile.md)), so a crash never leaves a half-written file.
- The store is thread-safe, but `Current` is a live object. Change it through `Update` so the change is saved and announced.

## API

| Member | Description |
|---|---|
| `protected SettingsStore(string filePath, JsonSerializerOptions? options = null)` | `options` defaults to [`JsonHelpers.DefaultOptions`](../Helpers/JsonHelpers.md). |
| `string FilePath` | |
| `T Current` | Loaded lazily. |
| `T Load()` | Re-reads the file and raises `Changed` (source `Load`). |
| `void Save()` / `Task SaveAsync(CancellationToken)` | Writes `Current`. |
| `T Update(Action<T> change)` | Changes, saves, raises `Changed` (source `Update`); returns the settings. |
| `T Reset()` | Replaces with `CreateDefault()`, saves, raises `Changed` (source `Reset`). |
| `void Watch(TimeSpan? delay = null)` | Reloads when something else edits the file (another instance, a text editor). The store's own saves are ignored. |
| `bool IsWatching`, `void StopWatching()` | |
| `event EventHandler<SettingsChangedEventArgs<T>> Changed` | `e.Settings`, `e.Source`. **While watching, it can be raised on a background thread**; marshal to the UI thread yourself. |
| `Dispose()` | Stops watching. |
| `protected virtual T CreateDefault()` | Defaults to `new T()`. |
| `protected virtual void OnLoaded(T settings)` | After loading or creating, before it becomes `Current`: fill in missing values or migrate old ones. |
| `protected virtual void OnChanged(T settings, SettingsChangeSource source)` | Raises `Changed`. |

`SettingsChangeSource`: `Update`, `Reset`, `Load` (including reloads while watching).

## Examples

```csharp
using Niddy.IO;
using Niddy.Settings;

public sealed class AppSettings
{
    public int Version { get; set; } = 2;
    public string Theme { get; set; } = "System";
    public List<string> RecentFiles { get; set; } = [];
}

public sealed class AppSettingsStore(AppPaths paths)
    : SettingsStore<AppSettings>(Path.Combine(paths.Config, "settings.json"))
{
    protected override void OnLoaded(AppSettings s)
    {
        if (s.Version < 2)
        {
            s.RecentFiles ??= [];
            s.Version = 2;
        }
    }
}

var store = new AppSettingsStore(AppPaths.For("MyApp"));

string theme = store.Current.Theme;
store.Update(s => s.RecentFiles.Insert(0, path));

store.Changed += (_, e) => Dispatcher.UIThread.Post(() => ApplyTheme(e.Settings.Theme));
store.Watch();
```

## See also

- [AppPaths](../IO/AppPaths.md), [JsonHelpers](../Helpers/JsonHelpers.md), [DebouncedFileWatcher](../IO/DebouncedFileWatcher.md)
- [UiStateStore](../../Niddy.Avalonia/State/UiStateStore.md) is a `SettingsStore` for window and layout state.
- [examples/settings-logging-paths](../../examples/settings-logging-paths.md)
