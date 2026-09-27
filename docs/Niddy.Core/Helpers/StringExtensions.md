# StringExtensions

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/StringExtensions.cs)

Fluent forms of common `string` statics.

## API

| Member | Description |
|---|---|
| `bool IsNullOrEmpty(this string? value)` | `string.IsNullOrEmpty(value)`. |
| `bool IsNullOrWhiteSpace(this string? value)` | `string.IsNullOrWhiteSpace(value)`. |
| `string Join<T>(this string separator, IEnumerable<T> values)` | `string.Join(separator, values)`. |
| `string Join<T>(this IEnumerable<T> values, string separator)` | The same, called on the values. |

## Example

```csharp
using Niddy.Helpers;

if (name.IsNullOrWhiteSpace())
    return;

string csv = values.Join(", ");
string path = "/".Join(segments);
```
