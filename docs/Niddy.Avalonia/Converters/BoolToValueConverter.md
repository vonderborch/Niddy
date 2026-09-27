# BoolToValueConverter

`Niddy.Avalonia.Converters` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Converters/BoolToValueConverter.cs)

Converts a bool to one of two values, such as a brush, text or thickness. Null converts to `FalseValue`. Converting back gives true when the value equals `TrueValue` (or its string form).

## API

| Member | Description |
|---|---|
| `object? TrueValue` | The value for true. |
| `object? FalseValue` | The value for false or null. |

## Example

```xml
<UserControl.Resources>
    <niddy:BoolToValueConverter x:Key="OnlineColor" TrueValue="Green" FalseValue="Gray" />
    <niddy:BoolToValueConverter x:Key="SaveLabel" TrueValue="Saving…" FalseValue="Save" />
</UserControl.Resources>

<Ellipse Fill="{Binding IsOnline, Converter={StaticResource OnlineColor}}" />
<Button Content="{Binding IsBusy, Converter={StaticResource SaveLabel}}" />
```

Attribute values are strings, which the binding converts to the target type where it can (as with `Green` above). To store a typed value instead, use property elements:

```xml
<niddy:BoolToValueConverter x:Key="ErrorBorder">
    <niddy:BoolToValueConverter.TrueValue><Thickness>2</Thickness></niddy:BoolToValueConverter.TrueValue>
    <niddy:BoolToValueConverter.FalseValue><Thickness>0</Thickness></niddy:BoolToValueConverter.FalseValue>
</niddy:BoolToValueConverter>
```

## See also

- [EnumToBoolConverter](EnumToBoolConverter.md), [CollectionConverters](CollectionConverters.md)
