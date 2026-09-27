# Debouncer

`Niddy.Threading` · Niddy.Core · [source](../../../src/Niddy.Core/Threading/Debouncer.cs)

Runs an action once calls stop arriving for `Delay`. Each `Invoke` restarts the wait and replaces the pending action, so only the last one runs. Use it for search-as-you-type, autosave and resize handling. To run *at most once per interval* instead, use [`Throttler`](Throttler.md).

Actions run on the `SynchronizationContext` that was current when the debouncer was created (the UI thread, if you create it there), or on the thread pool if there was none or `captureContext` is false.

## API

| Member | Description |
|---|---|
| `Debouncer(TimeSpan delay, TimeProvider? timeProvider = null, bool captureContext = true)` | `timeProvider` is for tests (e.g. `FakeTimeProvider`). |
| `TimeSpan Delay` | |
| `bool IsPending` | Whether an action is waiting to run. |
| `void Invoke(Action action)` | Schedules it, replacing any pending action. |
| `void Invoke(Func<CancellationToken, Task> action)` | The token is cancelled as soon as another call arrives, so stale async work (an outdated search) can stop early. |
| `void Cancel()` | Drops the pending action and cancels a running one's token. |
| `void Flush()` | Runs the pending action now, on the calling thread. Call it before closing so the last change isn't lost. |
| `event EventHandler<Exception> Error` | An exception from an action. If nothing handles this event, the exception is rethrown where the action ran. |
| `Dispose()` | Drops the pending action and cancels a running one. |

## Examples

Search as you type, in a ReactiveUI data context:

```csharp
using Niddy.Threading;

public sealed class SearchPageDataContext : DataContextBase, IDisposable
{
    private readonly Debouncer _search = new(TimeSpan.FromMilliseconds(300));

    public string Query
    {
        get;
        set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            _search.Invoke(async ct => Results = await _api.SearchAsync(value, ct));
        }
    } = "";

    public void Dispose() => _search.Dispose();
}
```

Autosave that isn't lost on close:

```csharp
private readonly Debouncer _autosave = new(TimeSpan.FromSeconds(2));

void OnTextChanged() => _autosave.Invoke(() => AtomicFile.WriteAllText(path, Editor.Text));
void OnClosing() => _autosave.Flush();
```

## See also

- [Throttler](Throttler.md)
- [DebouncedFileWatcher](../IO/DebouncedFileWatcher.md)
- [examples/long-running-work](../../examples/long-running-work.md)
