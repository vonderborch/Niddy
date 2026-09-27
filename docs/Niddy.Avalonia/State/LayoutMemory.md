# LayoutMemory

`Niddy.Avalonia.State` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/State/LayoutMemory.cs)

Remembers the sizes users give to resizable layouts and restores them next time. It supports the column widths and row heights of a `Grid` with `GridSplitter`s, and the proportions of a `ProportionalStackPanel` (Dock). It uses [`UiStateStore.Default`](UiStateStore.md) unless a store is passed.

Saved sizes are only restored if the number of columns, rows or panes still matches. Changing the layout in a new version therefore doesn't break anything; it just starts fresh.

## API

| Member | Description |
|---|---|
| `Key` attached property (`GetKey`/`SetKey`) | On a `Grid` or `ProportionalStackPanel`. Attaches once the control has loaded. |
| `static IDisposable Attach(Control control, string key, UiStateStore? store = null)` | The same in code. Throws `NotSupportedException` for other controls. Dispose to stop tracking. |

## Example

```xml
<Grid xmlns:niddy="https://github.com/vonderborch/Niddy"
      ColumnDefinitions="250,4,*" niddy:LayoutMemory.Key="MainSplit">
    <TreeView />
    <GridSplitter Grid.Column="1" />
    <ContentControl Grid.Column="2" />
</Grid>
```

```xml
<ProportionalStackPanel Orientation="Horizontal" niddy:LayoutMemory.Key="EditorPanes">
    <Border ProportionalStackPanel.Proportion="0.3" />
    <ProportionalStackPanelSplitter />
    <Border />
</ProportionalStackPanel>
```

## See also

- [UiStateStore](UiStateStore.md), [WindowMemory](WindowMemory.md)
