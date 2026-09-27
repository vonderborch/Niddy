# AtomicOperations

`Niddy.Threading` · Niddy.Core · [source](../../../src/Niddy.Core/Threading/AtomicOperations.cs)

Small lock-free helpers over `Interlocked`.

## API

| Member | Description |
|---|---|
| `bool CompareAndSwap<T>(ref T location, T newValue, T expected)` | Sets `location` to `newValue` if it currently equals `expected`. True if it swapped. |
| `bool CompareAndSwap<T>(ref T location, T newValue, T expected, out T original)` | Also outputs the value that was there. |
| `T Increment<T>(ref T variable, T maximum) where T : INumber<T>` | Adds one, never going past `maximum`. Returns the value **before** the change. |
| `T Decrement<T>(ref T variable, T minimum) where T : INumber<T>` | Subtracts one, never going below `minimum`. Returns the value before. |

## Examples

```csharp
using Niddy.Threading;

private int _activeDownloads;

public bool TryStartDownload()
{
    // Allow at most 4 at once.
    return AtomicOperations.Increment(ref _activeDownloads, 4) < 4;
}

public void FinishDownload() => AtomicOperations.Decrement(ref _activeDownloads, 0);

private State _state = State.Idle;
if (AtomicOperations.CompareAndSwap(ref _state, State.Running, State.Idle))
    StartWork();
```

## See also

- [Guard](Guard.md)
