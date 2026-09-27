# PageMenu

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageMenu.cs) · [PageMenuStyle](../../../src/Niddy.Avalonia/PageSystem/PageMenuStyle.cs) · [PageMenuMode](../../../src/Niddy.Avalonia/PageSystem/PageMenuMode.cs)

A ready-made navigation menu. It lists registered pages with their icons, navigates the `Target` page view when one is picked, and keeps the current page highlighted. Give it the page view as its `Content`, and it arranges itself around it: a sidebar, a tab strip, a phone navigation bar or a menu button. It switches to a narrow style in mobile mode or narrow windows.

## Styles and placement

`Style` is how the menu looks. `Placement` (any of the eight [`ScreenPlacement`](../ScreenPlacement.md) values, default `TopLeft`) is where it goes. Each style reads the placement as the nearest one that fits:

| `PageMenuStyle` | Looks like | Placement |
|---|---|---|
| `VerticalStrip` (default) | A sidebar column that scrolls when it overflows. Shows the tree in `Tree` mode. | Its side picks left or right (left for `TopCenter`/`BottomCenter`). Top, center or bottom puts the items at the top, middle or bottom. |
| `HorizontalStrip` | A tab strip that scrolls sideways. | Top or bottom edge (top for `CenterLeft`/`CenterRight`). Left, center or right aligns the items. |
| `NavigationBar` | A phone-style bar with equal-width items, the icon above the name. | Top or bottom edge (bottom for `CenterLeft`/`CenterRight`). |
| `MenuButton` | A button that opens the menu in a flyout toward the page. Shows the tree in `Tree` mode. | The button sits at the placement, in a strip along the top or bottom (or the side for `CenterLeft`/`CenterRight`). |

`NarrowStyle` (default `NavigationBar`) and `NarrowPlacement` (default `BottomCenter`) are used in mobile mode or below `NarrowWidth` (600). The menu switches as the window resizes. Set either to null to keep `Style` or `Placement`.

## Modes

| `PageMenuMode` | Lists |
|---|---|
| `Flat` (default) | One level: the children of `ParentPage`, or the top-level pages. While a child page is shown, its ancestor stays highlighted, and tapping it returns there. |
| `Tree` | The whole tree below `ParentPage` as expandable, indented items. A page with several parents appears under each, and the current page's branch expands. Picking a deeper page first navigates each ancestor that shows its children in a nested `PageView`, so the page opens in place. Right and Left expand and collapse. Only `VerticalStrip` and `MenuButton` show the tree; the other styles list the top level. |

## Behavior

- **Navigation.** Menu navigation replaces the current page without adding to the back stack. Set `AddToBackStack` to change that. If navigation is refused (`LockToCurrentPage` or a cancelling `OnNavigatingFrom`), the highlight stays put.
- **Touch.** On touch the items are larger (48 px, or 56 px in a navigation bar) and so is the menu button. The flyout fits narrow screens, and the platform back request closes it.
- **Without `Content`.** The menu docks itself in its parent `DockPanel`, on the side taken from the style and placement, unless you set `DockPanel.Dock`.
- **Styling.** Pseudo-classes `:verticalstrip`, `:horizontalstrip`, `:navigationbar`, `:menubutton` and `:tree` are set.

## API

| Property | Description |
|---|---|
| `Control? Content` | The page area to arrange around: the `PageView`, or a panel holding it. |
| `PageView? Target` | The view to navigate and follow. Defaults to `Content` if it's a `PageView`, otherwise the `NiddyApp`'s page view. |
| `Type? ParentPage` | Whose children are listed; null for the top level. Pages registered later are added. |
| `PageMenuStyle Style`, `PageMenuStyle? NarrowStyle` | |
| `ScreenPlacement Placement`, `ScreenPlacement? NarrowPlacement` | |
| `double NarrowWidth` | Default 600. |
| `PageMenuMode Mode` | |
| `bool AddToBackStack` | Default false. |
| `IDataTemplate? ItemTemplate` | Draw the items yourself; the data is [`PageMenuItem`](PageMenuItem.md). |
| `PageMenuStyle ActualStyle`, `ScreenPlacement ActualPlacement` | The resolved values (read-only). |
| `ReadOnlyObservableCollection<PageMenuItem> Items` | The top level, plus the children of expanded items in a tree. |
| `PageMenuItem? SelectedItem` | The current page's item. Setting it navigates. |

## Examples

As the app's layout:

```csharp
options.Layout = pages => new PageMenu { Content = pages, Mode = PageMenuMode.Tree };
```

In AXAML:

```xml
<pageSystem:PageMenu Style="VerticalStrip" Placement="TopLeft" Mode="Tree">
    <pageSystem:PageView StartingPage="{x:Type local:HomePage}" />
</pageSystem:PageMenu>
```

A second-level tab strip inside `SettingsPage`, over its nested view:

```xml
<pageSystem:PageMenu Style="HorizontalStrip" Placement="TopCenter" NarrowStyle="{x:Null}"
                     ParentPage="{x:Type local:SettingsPage}">
    <pageSystem:PageView StartingPage="{x:Type local:GeneralSettingsPage}" />
</pageSystem:PageMenu>
```

A bottom navigation bar everywhere, and a custom item template:

```xml
<pageSystem:PageMenu Style="NavigationBar" Placement="BottomCenter">
    <pageSystem:PageMenu.ItemTemplate>
        <DataTemplate x:DataType="pageSystem:PageMenuItem">
            <StackPanel Spacing="2" HorizontalAlignment="Center">
                <pageSystem:PageIconView Icon="{Binding Icon}" IconSize="22" HorizontalAlignment="Center" />
                <TextBlock Text="{Binding DisplayName}" FontSize="11" />
            </StackPanel>
        </DataTemplate>
    </pageSystem:PageMenu.ItemTemplate>
    <pageSystem:PageView StartingPage="{x:Type local:HomePage}" />
</pageSystem:PageMenu>
```

## See also

- [PageMenuItem](PageMenuItem.md), [PageView](PageView.md), [PageIcon](PageIcon.md), [ScreenPlacement](../ScreenPlacement.md)
