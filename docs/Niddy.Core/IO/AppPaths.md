# AppPaths

`Niddy.IO` · Niddy.Core · [source](../../../src/Niddy.Core/IO/AppPaths.cs)

`sealed record AppPaths(string Data, string Config, string Cache, string Logs)`: where an app keeps its files, following each platform's conventions. Nothing is created until you call `EnsureCreated()`.

| Platform | Data / Config | Cache | Logs |
|---|---|---|---|
| Windows | `%APPDATA%\[Organization\]App` | `%LOCALAPPDATA%\[Organization\]App\Cache` | `%LOCALAPPDATA%\[Organization\]App\Logs` |
| macOS | `~/Library/Application Support/App` | `~/Library/Caches/App` | `~/Library/Logs/App` |
| Linux | `$XDG_DATA_HOME/app` / `$XDG_CONFIG_HOME/app` | `$XDG_CACHE_HOME/app` | `$XDG_STATE_HOME/app/logs` |
| Android, iOS, browser | subfolders of the app's local data folder | | |

`Config` equals `Data` except on Linux. The folder name is lowercased on Linux.

## API

| Member | Description |
|---|---|
| `Data` | Files the user would miss: databases, documents. |
| `Config` | Settings files. |
| `Cache` | Files that can be recreated and may be deleted at any time. |
| `Logs` | Log files. |
| `static AppPaths For(string appName, string? organization = null)` | The standard locations. `organization` only adds a folder level on Windows. |
| `static AppPaths Portable(string baseDirectory)` | Everything under one folder (portable installs): `Data` and `Config` are the folder, `Cache` and `Logs` are subfolders. |
| `AppPaths EnsureCreated()` | Creates any missing directory; returns `this` for chaining. |

## Examples

```csharp
using Niddy.IO;

var paths = AppPaths.For("MyApp", "Contoso").EnsureCreated();
var dbPath = Path.Combine(paths.Data, "library.db");
var settingsPath = Path.Combine(paths.Config, "settings.json");

// Portable build: keep everything next to the executable
var portable = AppPaths.Portable(AppContext.BaseDirectory).EnsureCreated();
```

In a [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md), use `NiddyApp.Current!.Paths`, built from `options.AppName` and `options.Organization`.

## See also

- [NiddyLogging](../Logging/NiddyLogging.md) `CreateFactory(paths)` logs to `paths.Logs`.
- [UiStateStore](../../Niddy.Avalonia/State/UiStateStore.md) `For(paths)` saves UI state in `paths.Data`.
