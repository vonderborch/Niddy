# EnumValuesExtension

`Niddy.Avalonia.Converters` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Converters/EnumValuesExtension.cs)

A markup extension, `{niddy:EnumValues}`, that provides all the values of an enum, e.g. for a combo box's items. A nullable enum type gives the underlying enum's values. Throws if the type isn't an enum.

## API

| Member | Description |
|---|---|
| `EnumValuesExtension()` / `EnumValuesExtension(Type type)` | |
| `Type? Type` | The enum type. |

## Example

```xml
<ComboBox ItemsSource="{niddy:EnumValues local:Priority}"
          SelectedItem="{Binding Priority}" />

<!-- Named form -->
<ListBox ItemsSource="{niddy:EnumValues Type=local:Priority}" />
```

## See also

- [EnumToBoolConverter](EnumToBoolConverter.md)
