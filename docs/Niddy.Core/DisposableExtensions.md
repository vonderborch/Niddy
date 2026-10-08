# DisposableExtensions

`Niddy.Core` · Niddy.Core · [source](../../src/Niddy.Core/DisposableExtensions.cs)

A C# 14 extension member for values that may or may not implement `IDisposable`.

## API

| Member | Description |
|---|---|
| `void value.DisposeIfPossible()` | Available on any value of type `T`. Disposes it if it's `IDisposable`, and otherwise does nothing (including for null). |

## Example

```csharp
using Niddy.Core;

// A cache of arbitrary values, some of which own resources
foreach (object value in cache.Values)
    value.DisposeIfPossible();
cache.Clear();
```
