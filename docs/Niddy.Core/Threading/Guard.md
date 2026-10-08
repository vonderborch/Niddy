# Guard

`Niddy.Core.Threading` · Niddy.Core · [source](../../../src/Niddy.Core/Threading/Guard.cs)

A thread-safe boolean flag, backed by an `int` and changed with `Interlocked`. Its main use is "do this only once", even when several threads race to it.

`Guard` is a mutable struct. Keep it in a **non-readonly field** and don't copy it, or each copy has its own state.

## API

| Member | Description |
|---|---|
| `bool IsClaimed` | Whether it's claimed. Doesn't change it. |
| `bool TryClaim` | Claims it and returns **true only for the caller that claimed it** (false if it was already claimed). It's a property, not a method. |
| `void Claim()` | Claims it, whether or not it was already claimed. Use `TryClaim` when you need to know who claimed it first. |
| `void Release()` | Releases it, so it can be claimed again. |
| `==` / `!=` with `bool` | `if (_disposed == true)` compares with `IsClaimed`. |
| `Equals(Guard)` | Whether two guards are in the same state. |

## Examples

```csharp
using Niddy.Core.Threading;

public sealed class Connection : IDisposable
{
    private Guard _disposed;   // not readonly

    public void Dispose()
    {
        if (!_disposed.TryClaim)
        {
            return;            // someone else got here first
        }

        _socket.Dispose();
    }

    public void Send(byte[] data)
    {
        ObjectDisposedException.ThrowIf(_disposed.IsClaimed, this);
        _socket.Send(data);
    }
}
```

## See also

- [AtomicOperations](AtomicOperations.md)
