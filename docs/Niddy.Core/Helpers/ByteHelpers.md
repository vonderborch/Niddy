# ByteHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/ByteHelpers.cs)

Formats a byte count as a size string.

## API

| Member | Description |
|---|---|
| `static string FormatBytes(long bytes)` | Formats `bytes` in 1024-based units with one decimal, e.g. `"1.4 MB"` (`"512 B"` below 1 KB). |

## Example

```csharp
using Niddy.Helpers;

string size = ByteHelpers.FormatBytes(new FileInfo(path).Length);
```

Prefer [`Humanize.Bytes`](Humanize.md) in new code: it supports decimal places, binary units and cultures.
