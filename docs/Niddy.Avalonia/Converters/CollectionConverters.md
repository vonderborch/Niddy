# CollectionConverters

`Niddy.Avalonia.Converters` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Converters/CollectionConverters.cs)

Converters for collections, e.g. to show an empty-state message.

## API

| Member | True for |
|---|---|
| `static IValueConverter IsEmpty` | Null, an empty collection, enumerable or string, or a count of zero or less. |
| `static IValueConverter IsNotEmpty` | The opposite. |

A binding to the collection itself only updates when the property is replaced, not when items are added. **Bind to `Items.Count`** for a collection that changes.

## Example

```xml
<Panel>
    <ListBox ItemsSource="{Binding Items}"
             IsVisible="{Binding Items.Count, Converter={x:Static niddy:CollectionConverters.IsNotEmpty}}" />
    <TextBlock Text="Nothing here yet" HorizontalAlignment="Center"
               IsVisible="{Binding Items.Count, Converter={x:Static niddy:CollectionConverters.IsEmpty}}" />
</Panel>
```

## See also

- [BoolToValueConverter](BoolToValueConverter.md)
