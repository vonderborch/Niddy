# WindowNotification

`Niddy.Avalonia` · Niddy.Avalonia · [source](../../src/Niddy.Avalonia/WindowNotification.cs)

Asks for the user's attention when something finishes while the app is in the background. It bounces the dock icon once on macOS and flashes the taskbar button on Windows. Elsewhere it activates the main window. It only works with a desktop lifetime and a main window; otherwise it does nothing. It never throws; a platform error is logged as a warning to `NiddyAppOptions.LoggerFactory` (or `Trace`).

## API

| Member | Description |
|---|---|
| `static void RequestAttention()` | |

## Example

```csharp
using Niddy.Avalonia;
using Niddy.Avalonia.Toasts;

await RunLongBuildAsync();
WindowNotification.RequestAttention();
Toast.Show(this, "Build finished", ToastType.Success);
```

## See also

- [Toast](Toasts/Toast.md)
