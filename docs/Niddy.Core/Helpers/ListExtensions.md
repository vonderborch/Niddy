# ListExtensions

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/ListExtensions.cs)

Extension methods for `List<T>`.

## API

| Member | Description |
|---|---|
| `List<T> CombineLists<T>(this List<T> list1, List<T> list2)` | A new list with the items of both. |
| `bool CompareLists<T>(this List<T> list1, List<T> list2) where T : IComparable<T>` | Whether both lists hold the same items, in any order (sorts copies to compare). |
| `bool IsContained<T>(this List<T> list, T value, bool caseSensitive = true)` | `Contains`, with case-insensitive matching for strings when `caseSensitive` is false. |

## Example

```csharp
using Niddy.Helpers;

var all = defaults.CombineLists(userEntries);
bool unchanged = saved.CompareLists(current);
bool hasReadme = fileNames.IsContained("readme.md", caseSensitive: false);
```
