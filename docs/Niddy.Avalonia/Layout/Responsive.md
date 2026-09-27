# Responsive

`Niddy.Avalonia.Layout` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Layout/Responsive.cs)

Attached properties that switch a control between narrow and wide layouts based on **its own width** (not the window's). The control gets the `narrow` style class below `NarrowBelow` and `wide` otherwise, so styles can rearrange it. Set `WideAbove` as well to get a `medium` class between the two widths.

## API

| Member | Description |
|---|---|
| `NarrowBelow` attached (`double`, default NaN) | Setting it starts tracking. NaN stops it and removes the classes. |
| `WideAbove` attached (`double`, default NaN) | NaN means the same as `NarrowBelow`, with no medium range. |
| `SizeClass` attached (`string?`, read with `GetSizeClass`) | `"narrow"`, `"medium"` or `"wide"`. Null until the control is tracked and measured. |
| `const string NarrowClass`, `MediumClass`, `WideClass` | The class names. |
| `static string Classify(double width, double narrowBelow, double wideAbove)` | The class for a width. |

## Example

```xml
<!-- Cards: 1 column narrow, 2 medium, 3 wide -->
<ItemsControl ItemsSource="{Binding Cards}"
              niddy:Responsive.NarrowBelow="600" niddy:Responsive.WideAbove="1000">
    <ItemsControl.Styles>
        <Style Selector="ItemsControl.wide UniformGrid">
            <Setter Property="Columns" Value="3" />
        </Style>
        <Style Selector="ItemsControl.medium UniformGrid">
            <Setter Property="Columns" Value="2" />
        </Style>
        <Style Selector="ItemsControl.narrow UniformGrid">
            <Setter Property="Columns" Value="1" />
        </Style>
    </ItemsControl.Styles>
    <ItemsControl.ItemsPanel>
        <ItemsPanelTemplate>
            <UniformGrid />
        </ItemsPanelTemplate>
    </ItemsControl.ItemsPanel>
</ItemsControl>
```

A sidebar that hides when narrow:

```xml
<DockPanel niddy:Responsive.NarrowBelow="700">
    <DockPanel.Styles>
        <Style Selector="DockPanel.narrow > Border#Sidebar">
            <Setter Property="IsVisible" Value="False" />
        </Style>
    </DockPanel.Styles>
    <Border Name="Sidebar" DockPanel.Dock="Left" Width="240" />
    <ContentControl Content="{Binding Main}" />
</DockPanel>
```

In code:

```csharp
Responsive.SetNarrowBelow(panel, 600);
bool narrow = Responsive.GetSizeClass(panel) == Responsive.NarrowClass;
```

## See also

- [PageMenu](../PageSystem/PageMenu.md), which switches its style by width in a similar way
- [examples/mobile-and-browser](../../examples/mobile-and-browser.md)
