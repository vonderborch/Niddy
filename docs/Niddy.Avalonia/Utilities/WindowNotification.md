# WindowNotification

`Niddy.Avalonia.Utilities` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Utilities/WindowNotification.cs)

Asks for the user's attention when something finishes while the app is in the background. It bounces the dock icon once on macOS and flashes the taskbar button on Windows. Elsewhere it activates the main window. It only works with a desktop lifetime and a main window; otherwise it does nothing. It never throws.

## API

| Member | Description |
|---|---|
| `static void RequestAttention()` | |

## Example

```csharp
using Niddy.Avalonia.Utilities;

await RunLongBuildAsync();
WindowNotification.RequestAttention();
Toast.Show(this, "Build finished", ToastType.Success);
```

## See also

- [Toast](../Toast/Toast.md)
