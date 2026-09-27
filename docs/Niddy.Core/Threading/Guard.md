# Guard

`Niddy.Threading` · Niddy.Core · [source](../../../src/Niddy.Core/Threading/Guard.cs)

A thread-safe boolean flag, backed by an `int` and changed with `Interlocked`. Its main use is "do this only once", even when several threads race to it.

`Guard` is a mutable struct. Keep it in a **non-readonly field** and don't copy it, or each copy has its own state.

## API

| Member | Description |
|---|---|
| `bool Check` | Whether it's set. Doesn't change it. |
| `bool CheckSet` | Sets it and returns **true only for the caller that set it** (false if it was already set). |
| `void MarkChecked()` | Sets it. |
| `void Reset()` | Clears it. |
| `==` / `!=` with `bool` | `if (_disposed == true)` compares with `Check`. |

## Examples

```csharp
using Niddy.Threading;

public sealed class Connection : IDisposable
{
    private Guard _disposed;   // not readonly

    public void Dispose()
    {
        if (!_disposed.CheckSet)
            return;            // someone else got here first
        _socket.Dispose();
    }

    public void Send(byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed.Check, this);
        _socket.Send(data);
    }
}
```

## See also

- [AtomicOperations](AtomicOperations.md)
