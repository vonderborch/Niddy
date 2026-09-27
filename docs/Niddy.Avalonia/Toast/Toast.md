# Toast

`Niddy.Avalonia.Toast` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Toast/Toast.cs) · [ToastType](../../../src/Niddy.Avalonia/Toast/ToastType.cs)

Shows short-lived, non-blocking notifications, optionally with an action button or a progress bar. Toasts need a [`ToastHost`](ToastHost.md) somewhere above the source control. [`NiddyApp`](../Hosting/NiddyApp.md)'s shell already has one.

Clicking a toast dismisses it, and the countdown pauses while the pointer is over it. Showing the same message and type again while it's still up doesn't stack another toast. The existing one restarts its countdown and shows a count ("×3"). Pass `key:` to merge *different* messages (e.g. a changing status line), or set `MergeDuplicates="False"` on the host to always stack.

## API

| Member | Description |
|---|---|
| `static TimeSpan DefaultDuration` | Default 3 s. `NiddyAppOptions.ToastDuration` sets it. |
| `static TimeSpan DefaultActionDuration` | Default 6 s. Used for toasts with an action button. |
| `static ToastHandle Show(Control source, string message, ToastType type = Info, TimeSpan? duration = null, string? actionText = null, Action? action = null, string? key = null)` | A duration of `TimeSpan.Zero` keeps the toast up until it's dismissed. `action` runs after the toast is dismissed by its button. |
| `static ToastHandle ShowProgress(Control source, string message, bool isIndeterminate = true, string? cancelText = null, Action? cancel = null)` | A toast with a progress bar that stays up until `Complete` or `Dismiss`. Reporting progress makes the bar determinate. |

Both methods throw `InvalidOperationException` if there's no `ToastHost` above `source`. Call them on the UI thread. The returned [`ToastHandle`](ToastHandle.md) can be used from any thread.

`ToastType`: `Info`, `Success`, `Warning`, `Error`. Each has its own color.

## Examples

```csharp
using Niddy.Avalonia.Toast;

Toast.Show(this, "Saved", ToastType.Success);

Toast.Show(this, "File deleted", actionText: "Undo", action: () => RestoreFile(file));

Toast.Show(this, "Couldn't reach the server", ToastType.Error, duration: TimeSpan.Zero);

// One toast that updates in place, instead of a stack of them
Toast.Show(this, $"Synced {count} items", key: "sync-status");
```

A progress toast:

```csharp
using var cts = new CancellationTokenSource();
var toast = Toast.ShowProgress(this, "Exporting…", isIndeterminate: false,
    cancelText: "Cancel", cancel: cts.Cancel);
try
{
    await ExportAsync(toast, cts.Token);   // calls toast.Report(0..1)
    toast.Complete("Exported");
}
catch (OperationCanceledException)
{
    // Cancel already dismissed the toast
}
catch (Exception ex)
{
    toast.Complete($"Export failed: {ex.Message}", ToastType.Error);
}
```

## See also

- [ToastHandle](ToastHandle.md), [ToastHost](ToastHost.md)
- [examples/long-running-work](../../examples/long-running-work.md)
