# PageIconView

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageIconView.cs)

A control that shows a [`PageIcon`](PageIcon.md) at `IconSize`, picking the best source for that size and the screen's scaling. Vector icons are filled with `Foreground`, which is inherited like text color, so they follow the theme and a selected item's highlight. It takes no space without an icon.

## API

| Property | Description |
|---|---|
| `PageIcon? Icon` | |
| `double IconSize` | Width and height in DIPs. Default 20. |
| `IBrush? Foreground` | Inherited. |
| `PageIconSource? CurrentSource` | The source picked for the current size and scaling. |

## Example

```xml
<ItemsControl ItemsSource="{Binding $parent[pageSystem:Page].PageControlDataContext.AvailablePages}">
    <ItemsControl.ItemTemplate>
        <DataTemplate x:DataType="pageSystem:PageRegistration">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <pageSystem:PageIconView Icon="{Binding Icon}" IconSize="24" />
                <TextBlock Text="{Binding DisplayName}" />
            </StackPanel>
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

## See also

- [PageIcon](PageIcon.md), [PageMenu](PageMenu.md)
