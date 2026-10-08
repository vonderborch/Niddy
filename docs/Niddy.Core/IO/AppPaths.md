# AppPaths

`Niddy.Core.IO` · Niddy.Core · [source](../../../src/Niddy.Core/IO/AppPaths.cs)

`sealed record AppPaths(string Data, string Config, string Cache, string Logs)`: where an app keeps its files, following each platform's conventions. Nothing is created until you call `EnsureCreated()`.

| Platform | Data / Config | Cache | Logs |
|---|---|---|---|
| Windows | `%APPDATA%\[Organization\]App` | `%LOCALAPPDATA%\[Organization\]App\Cache` | `%LOCALAPPDATA%\[Organization\]App\Logs` |
| macOS, Mac Catalyst | `~/Library/Application Support/App` | `~/Library/Caches/App` | `~/Library/Logs/App` |
| Linux, FreeBSD | `$XDG_DATA_HOME/app` / `$XDG_CONFIG_HOME/app` | `$XDG_CACHE_HOME/app` | `$XDG_STATE_HOME/app/logs` |
| Android, iOS, browser, WASI | subfolders of the app's local data folder | | |

`Config` equals `Data` except on Linux and FreeBSD, where the folder name is also lowercased (spaces become `-`). The platform comes from [`Platform.Info`](../SystemInfo/Platform.md), so `For` throws `UnsupportedOperatingSystemException` if the app declared its supported OSes and this one isn't among them.

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
using Niddy.Core.IO;

AppPaths paths = AppPaths.For("MyApp", "Contoso").EnsureCreated();
string dbPath = Path.Combine(paths.Data, "library.db");
string settingsPath = Path.Combine(paths.Config, "settings.json");

// Portable build: keep everything next to the executable
AppPaths portable = AppPaths.Portable(AppContext.BaseDirectory).EnsureCreated();
```

In a [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md), use `NiddyApp.Current!.Paths`, built from `options.AppName` and `options.Organization`.

## See also

- [NiddyLogging](../Logging/NiddyLogging.md) `CreateFactory(paths)` logs to `paths.Logs`.
- [UiStateStore](../../Niddy.Avalonia/State/UiStateStore.md) `For(paths)` saves UI state in `paths.Data`.
