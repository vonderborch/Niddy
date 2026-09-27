# ToastHandle

`Niddy.Avalonia.Toast` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Toast/ToastHandle.cs)

A toast that's showing, returned by [`Toast.Show`](Toast.md) and `Toast.ShowProgress`. Use it to update, complete or dismiss the toast. It's an `IProgress<double>`, so it can be passed straight to code that reports progress. Its members are safe to call from any thread.

## API

| Member | Description |
|---|---|
| `Task Dismissed` | Completes when the toast goes away: timed out, clicked, its action run, or `Dismiss` called. |
| `bool IsDismissed` | |
| `void Update(string message, ToastType? type = null)` | Changes the message and, optionally, the style. |
| `void Report(double value)` | Progress from **0 to 1**, which makes the bar determinate. `double.NaN` makes it indeterminate again. (Note that `Dialog.Progress` uses 0–100.) |
| `void Complete(string? message = null, ToastType type = Success, TimeSpan? duration = null)` | Turns a progress toast into an ordinary one. Removes the bar and any action, and dismisses it after `duration` (default `Toast.DefaultDuration`). |
| `void Dismiss()` | Removes the toast now. |

## Example

```csharp
var toast = Toast.ShowProgress(this, "Downloading…");
await Task.Run(async () =>
{
    for (var i = 0; i <= 10; i++)
    {
        toast.Report(i / 10.0);        // fine from a background thread
        await Task.Delay(200);
    }
});
toast.Complete("Downloaded");
await toast.Dismissed;
```

## See also

- [Toast](Toast.md)
