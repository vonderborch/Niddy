# Throttler

`Niddy.Threading` · Niddy.Core · [source](../../../src/Niddy.Core/Threading/Throttler.cs)

Runs an action at most once per `Interval`, however often `Invoke` is called. By default the first call runs straight away (*leading*) and the last call in each interval runs when it ends (*trailing*), so the final state is never missed. Use it for progress updates, scroll and pointer-move handling. To wait for calls to *stop*, use [`Debouncer`](Debouncer.md).

Leading calls run on the calling thread. Trailing calls run on the `SynchronizationContext` that was current when the throttler was created, or on the thread pool if there was none or `captureContext` is false.

## API

| Member | Description |
|---|---|
| `Throttler(TimeSpan interval, bool leading = true, bool trailing = true, TimeProvider? timeProvider = null, bool captureContext = true)` | |
| `Interval`, `Leading`, `Trailing` | |
| `void Invoke(Action action)` | Runs now, at the end of the current interval, or not at all, as configured. |
| `void Cancel()` | Drops a pending trailing call. |
| `event EventHandler<Exception> Error` | An exception from a trailing call; rethrown if unhandled. |
| `Dispose()` | Drops a pending call and stops the timer. |

## Example

Report progress from a fast loop without flooding the UI:

```csharp
using Niddy.Threading;

var throttle = new Throttler(TimeSpan.FromMilliseconds(100));   // created on the UI thread

await Task.Run(() =>
{
    for (var i = 0; i < files.Count; i++)
    {
        Process(files[i]);
        var done = i + 1;
        throttle.Invoke(() => Dispatcher.UIThread.Post(() => StatusText = $"{done} of {files.Count}"));
    }
});
```

## See also

- [Debouncer](Debouncer.md)
