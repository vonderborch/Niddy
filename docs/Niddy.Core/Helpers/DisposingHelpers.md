# DisposingHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/DisposingHelpers.cs)

Disposes objects that may or may not implement `IDisposable`.

## API

| Member | Description |
|---|---|
| `static void DisposeIfPossible<T>(T obj)` | Disposes `obj` if it is `IDisposable`; does nothing otherwise (including for null). |
| `static void DisposeIfPossible(this object obj)` | Extension-method form. |

## Example

```csharp
using Niddy.Helpers;

// A cache of arbitrary values, some of which own resources
foreach (var value in cache.Values)
    value.DisposeIfPossible();
cache.Clear();
```
