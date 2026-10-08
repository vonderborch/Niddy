# ScreenPlacement

`Niddy.Avalonia` · Niddy.Avalonia · [source](../../src/Niddy.Avalonia/ScreenPlacement.cs)

One of the eight positions around the edge of the screen (or of the hosting control): the four corners and the middle of each side. It's used by [`PageMenu`](PageSystem/PageMenu.md) placements, [`ToastHost`](Toasts/ToastHost.md) and `NiddyAppOptions.ToastPlacement`.

| Value | |
|---|---|
| `TopLeft`, `TopCenter`, `TopRight` | Along the top |
| `CenterLeft`, `CenterRight` | The middle of the left and right edges |
| `BottomLeft`, `BottomCenter`, `BottomRight` | Along the bottom |

## Example

```csharp
NiddyAppOptions options = new()
{
    ToastPlacement = ScreenPlacement.TopRight,
    ToastNarrowPlacement = ScreenPlacement.TopCenter,
};
```

```xml
<niddy:PageMenu Placement="CenterLeft" NarrowPlacement="BottomCenter" />
```

## See also

- [ToastHost](Toasts/ToastHost.md), [PageMenu](PageSystem/PageMenu.md)
