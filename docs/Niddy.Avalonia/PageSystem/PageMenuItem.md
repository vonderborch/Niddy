# PageMenuItem

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageMenuItem.cs)

An item in a [`PageMenu`](PageMenu.md): one page at one place in the menu's tree. A page with several parents has an item under each. It implements `INotifyPropertyChanged`. Bind a menu's `ItemTemplate` to it.

## API

| Member | Description |
|---|---|
| `PageRegistration Registration`, `PageId PageId`, `Type PageType` | |
| `string DisplayName`, `PageIcon? Icon`, `KeyGesture? Shortcut` | From the registration. |
| `PageMenuItem? Parent` | Null at the top of the menu. |
| `int Depth` | 0 at the top. |
| `IReadOnlyList<PageMenuItem> Children`, `bool HasChildren` | Always empty in a `Flat` menu. |
| `bool IsExpanded` | Get or set. The menu expands the items leading to the current page. |

## Example

```xml
<DataTemplate x:DataType="pageSystem:PageMenuItem">
    <StackPanel Orientation="Horizontal" Spacing="8" Margin="{Binding Depth, Converter={StaticResource DepthToIndent}}">
        <pageSystem:PageIconView Icon="{Binding Icon}" />
        <TextBlock Text="{Binding DisplayName}" />
        <TextBlock Text="▸" IsVisible="{Binding HasChildren}" />
    </StackPanel>
</DataTemplate>
```

```csharp
if (menu.SelectedItem is { } item)
    Console.WriteLine($"{item.DisplayName} at depth {item.Depth}, under {item.Parent?.DisplayName ?? "(top)"}");
```

## See also

- [PageMenu](PageMenu.md)
