# UiStateStore

`Niddy.Avalonia.State` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/State/UiStateStore.cs) · [UiState](../../../src/Niddy.Avalonia/State/UiState.cs)

Saves UI state that users expect to be remembered, such as window placements ([`WindowMemory`](WindowMemory.md)) and splitter positions ([`LayoutMemory`](LayoutMemory.md)), to a JSON file. It's a [`SettingsStore<UiState>`](../../Niddy.Core/Settings/SettingsStore.md). Changes are saved shortly after they happen (debounced, half a second by default).

[`NiddyApp`](../Hosting/NiddyApp.md) sets `Default` to `ui-state.json` in the app's data folder when `NiddyAppOptions.RememberWindowState` is on (the default) and `Default` is still null. Without `NiddyApp`, set `Default` yourself. While `Default` is null, nothing is remembered.

## API

| Member | Description |
|---|---|
| `UiStateStore(string filePath, TimeSpan? saveDelay = null)` | |
| `static UiStateStore For(AppPaths paths)` | `ui-state.json` in `paths.Data`. |
| `static UiStateStore? Default { get; set; }` | Used when no store is passed to `WindowMemory`/`LayoutMemory`. |
| `WindowPlacement? GetWindow(string key)` / `void SetWindow(string key, WindowPlacement placement)` | |
| `IReadOnlyList<string>? GetLayout(string key)` / `void SetLayout(string key, IReadOnlyList<string> sizes)` | |
| `void Flush()` | Saves pending changes now. Windows flush when they close. |
| `bool HasPendingChanges` | |
| Inherited | `Current`, `Load`, `Save`, `Reset`, `Changed`, `Watch`… See [SettingsStore](../../Niddy.Core/Settings/SettingsStore.md). |

### `UiState`

| Property | Description |
|---|---|
| `ConcurrentDictionary<string, WindowPlacement> Windows` | Keyed by `WindowMemory` key. |
| `ConcurrentDictionary<string, string[]> Layouts` | Keyed by `LayoutMemory` key. A grid saves `key/columns` and `key/rows`. |

### `WindowPlacement`

`record WindowPlacement(double Width, double Height, int? X = null, int? Y = null, WindowState State = Normal)`. The size and position are from the normal (not maximized) state. The size is in device-independent pixels, the position in screen pixels. A minimized window is restored as normal.

## Example

```csharp
// Without NiddyApp: set the store once at startup
UiStateStore.Default = UiStateStore.For(AppPaths.For("MyApp"));

// On exit, make sure the last change reaches disk
desktop.ShutdownRequested += (_, _) => UiStateStore.Default?.Flush();

// Forget every remembered window and layout
UiStateStore.Default?.Reset();
```

## See also

- [WindowMemory](WindowMemory.md), [LayoutMemory](LayoutMemory.md), [AppPaths](../../Niddy.Core/IO/AppPaths.md)
