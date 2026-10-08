# StringExtensions

`Niddy.Core.Text` · Niddy.Core · [source](../../../src/Niddy.Core/Text/StringExtensions.cs)

C# 14 extension members that give common `string` statics a fluent form.

## API

| Member | Description |
|---|---|
| `bool value.IsNullOrEmpty()` | `string.IsNullOrEmpty(value)`. Annotated `[NotNullWhen(false)]`, so after a `false` result the compiler knows `value` isn't null. |
| `bool value.IsNullOrWhiteSpace()` | `string.IsNullOrWhiteSpace(value)`, with the same annotation. |
| `string separator.Join<T>(IEnumerable<T> values)` | `string.Join(separator, values)`. |
| `string values.Join(string separator)` | The same, called on the values. |

## Example

```csharp
using Niddy.Core.Text;

if (name.IsNullOrWhiteSpace())
    return;

string csv = values.Join(", ");
string path = "/".Join(segments);
```
