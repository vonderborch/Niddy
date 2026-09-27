# HumanizeConverters

`Niddy.Avalonia.Converters` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Converters/HumanizeConverters.cs)

Converters that format values for people using [`Humanize`](../../Niddy.Core/Helpers/Humanize.md): file sizes, durations, relative times and large counts. Values of the wrong type convert to null.

## API

| Member | Input | Output |
|---|---|---|
| `static IValueConverter FileSize` | Any integer type (or a finite double/decimal) | "1.5 MB" |
| `static IValueConverter Duration` | `TimeSpan` | "2 h 5 min" |
| `static IValueConverter RelativeTime` | `DateTimeOffset` or `DateTime` | "5 minutes ago". Worked out when the binding updates; **it doesn't tick on its own**. |
| `static IValueConverter Count` | Any integer type | "12.3K" |

`FileSize`, `Duration` and `Count` use the current culture.

## Example

```xml
<StackPanel>
    <TextBlock Text="{Binding Length, Converter={x:Static niddy:HumanizeConverters.FileSize}}" />
    <TextBlock Text="{Binding Elapsed, Converter={x:Static niddy:HumanizeConverters.Duration}}" />
    <TextBlock Text="{Binding Modified, Converter={x:Static niddy:HumanizeConverters.RelativeTime}}" />
    <TextBlock Text="{Binding Downloads, Converter={x:Static niddy:HumanizeConverters.Count}}" />
</StackPanel>
```

To keep relative times current, raise a property change on a timer (e.g. once a minute).

## See also

- [Humanize](../../Niddy.Core/Helpers/Humanize.md)
