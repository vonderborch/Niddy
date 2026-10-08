# ListExtensions

`Niddy.Core.Collections` · Niddy.Core · [source](../../../src/Niddy.Core/Collections/ListExtensions.cs)

C# 14 extension members on `List<T>`.

## API

| Member | Description |
|---|---|
| `List<T> list.CombineLists(List<T> other)` | A new list with this list's items followed by `other`'s. Neither list is changed. |
| `bool list.CompareLists(List<T> other)` (where `T : IComparable<T>`) | Whether both lists hold the same items, in any order (sorts copies to compare). |
| `bool list.IsContained(T value, bool caseSensitive = true)` | `Contains`, with case-insensitive matching for strings when `caseSensitive` is false. |

## Example

```csharp
using Niddy.Core.Collections;

List<string> all = defaults.CombineLists(userEntries);
bool unchanged = saved.CompareLists(current);
bool hasReadme = fileNames.IsContained("readme.md", caseSensitive: false);
```
