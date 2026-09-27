# Humanize

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/Humanize.cs)

Formats numbers, sizes and times for people to read. Every method is culture-aware through an optional `IFormatProvider` (current culture by default).

## API

| Member | Returns | Example output |
|---|---|---|
| `Bytes(long bytes, int decimals = 1, bool binary = false, IFormatProvider? provider = null)` | A file size in decimal (kB, MB…) or binary (KiB, MiB…) units | `"1.5 MB"`, `"1.4 MiB"` |
| `Count(long count, int decimals = 1, IFormatProvider? provider = null)` | A large number in short form | `"12.3K"`, `"4.5M"` |
| `Duration(TimeSpan duration, int maxUnits = 2, IFormatProvider? provider = null)` | The largest `maxUnits` units of a duration | `"5 min 3 s"`, `"2 h 5 min"` |
| `RelativeTime(DateTimeOffset time, DateTimeOffset? now = null)` | A time relative to now (or `now`) | `"3 minutes ago"`, `"in 2 days"` |
| `RelativeTime(DateTime time, DateTime? now = null)` | Same, for `DateTime` | |
| `Plural(long count, string singular, string? plural = null, IFormatProvider? provider = null)` | The count (with group separators) and the right word form; `plural` defaults to `singular + "s"` | `"1 file"`, `"1,234 files"` |
| `Ordinal(long number)` | The number with its English ordinal suffix | `"1st"`, `"22nd"`, `"113th"` |

## Examples

```csharp
using Niddy.Helpers;

Humanize.Bytes(1_500_000);                          // "1.5 MB"
Humanize.Bytes(1_500_000, binary: true);            // "1.4 MiB"
Humanize.Count(12_345);                             // "12.3K"
Humanize.Duration(TimeSpan.FromSeconds(303));       // "5 min 3 s"
Humanize.Duration(TimeSpan.FromMinutes(125), maxUnits: 1); // "2 h"
Humanize.RelativeTime(DateTimeOffset.Now.AddMinutes(-3));  // "3 minutes ago"
Humanize.Plural(1, "entry", "entries");             // "1 entry"
Humanize.Plural(4, "entry", "entries");             // "4 entries"
Humanize.Ordinal(22);                               // "22nd"
```

Pass `now` to `RelativeTime` in tests so the output is deterministic.

## See also

- [HumanizeConverters](../../Niddy.Avalonia/Converters/HumanizeConverters.md): the same formatting as XAML converters.
- [ByteHelpers](ByteHelpers.md): the older, simpler byte formatter.
