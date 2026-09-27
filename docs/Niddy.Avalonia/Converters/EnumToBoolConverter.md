# EnumToBoolConverter

`Niddy.Avalonia.Converters` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Converters/EnumToBoolConverter.cs)

Binds a radio button or toggle to one value of an enum. It's true when the bound value equals the converter parameter, and converts back to that value when checked. Unchecking does nothing, so a group of radio buttons works two-way. The parameter can be the enum value itself (`{x:Static}`) or its name (case-insensitive).

## API

| Member | Description |
|---|---|
| `static EnumToBoolConverter Instance` | A shared instance. |

## Example

```csharp
public enum Priority { Low, Normal, High }
```

```xml
<StackPanel>
    <RadioButton Content="Low"
                 IsChecked="{Binding Priority, Converter={x:Static niddy:EnumToBoolConverter.Instance}, ConverterParameter=Low}" />
    <RadioButton Content="Normal"
                 IsChecked="{Binding Priority, Converter={x:Static niddy:EnumToBoolConverter.Instance}, ConverterParameter=Normal}" />
    <RadioButton Content="High"
                 IsChecked="{Binding Priority, Converter={x:Static niddy:EnumToBoolConverter.Instance}, ConverterParameter={x:Static local:Priority.High}}" />
</StackPanel>
```

## See also

- [EnumValuesExtension](EnumValuesExtension.md)
