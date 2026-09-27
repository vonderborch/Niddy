# WindowMemory

`Niddy.Avalonia.State` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/State/WindowMemory.cs)

Remembers a window's size, position and maximized or full-screen state, and restores them the next time a window with the same key opens. A saved position that's no longer on any screen (e.g. a monitor was unplugged) is ignored, so the window opens where it normally would. It uses [`UiStateStore.Default`](UiStateStore.md) unless a store is passed. With no store, it does nothing.

[`NiddyApp`](../Hosting/NiddyApp.md) remembers the main window under the key `"Main"` on desktop, unless `RememberWindowState` is off.

## API

| Member | Description |
|---|---|
| `Key` attached property (`GetKey`/`SetKey`) | Set it before the window is shown. Setting it restores the saved placement and starts tracking. |
| `static IDisposable Attach(Window window, string key, UiStateStore? store = null)` | The same in code, with an explicit store. Dispose to stop tracking. |

## Example

```xml
<Window xmlns:niddy="https://github.com/vonderborch/Niddy"
        niddy:WindowMemory.Key="Settings" ...>
```

```csharp
var window = new LogViewerWindow();
WindowMemory.Attach(window, "LogViewer");
window.Show();
```

## See also

- [UiStateStore](UiStateStore.md), [LayoutMemory](LayoutMemory.md)
