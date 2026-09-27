# PageIcon

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageIcon.cs)

A page's icon, e.g. for a [`PageMenu`](PageMenu.md), in one or more sizes. Each size is a `PageIconSource`: vector path data, a bitmap, a geometry, an image or a resource. `GetSource` picks the best one for the size it's shown at, and [`PageIconView`](PageIconView.md) draws it. Give a page its icon with [`[PageIcon]`](PageIconAttribute.md) (once per size) or pass one to [`PageRegistry.Register`](PageRegistry.md).

## API

### `PageIcon`

| Member | Description |
|---|---|
| `PageIcon(params IEnumerable<PageIconSource> sources)` | Throws `ArgumentException` with no sources. |
| `static PageIcon FromData(string data, double size = 0)` | A one-source vector icon. |
| `IReadOnlyList<PageIconSource> Sources` | |
| `PageIconSource GetSource(double size, double scaling = 1)` | Picks, in order: a source made for exactly that size (bitmaps by physical pixels, `size × scaling`); a scalable source for any size; the scalable source made for the nearest size; the smallest bitmap at least as big; a bitmap for any size; the largest bitmap. |

Equality compares the sources.

### `PageIconSource`

| Member | Description |
|---|---|
| `FromData(string data, double size = 0)` | SVG-style path data, filled with the foreground color. `size` is the side of the square the path is drawn in, like an SVG `viewBox` (24 for a 24×24 icon set). 0 fits the path's bounds. |
| `FromUri(string uri, double size = 0)` | An absolute `avares://` or `file://` bitmap, loaded the first time it's shown. `size` is its width in pixels; 0 means any size. |
| `FromResource(object key, double size = 0)` | A `Geometry` or `IImage` resource, looked up from where the icon is shown, so it can follow the theme. |
| `FromGeometry(Geometry, double size = 0)`, `FromImage(IImage, double size = 0)` | |
| `double Size` | The size it was made for, or 0 for any size. |
| `bool IsScalable` | True for path data, geometry and resources. |
| `string? Data`, `string? Uri`, `object? ResourceKey` | Whichever applies. |

## Example

```csharp
var icon = new PageIcon(
    PageIconSource.FromData("M3 12 L12 3 L21 12 V21 H3 Z", 24),       // scalable, designed at 24
    PageIconSource.FromUri("avares://MyApp/Assets/home-16.png", 16),  // pixel-perfect at 16
    PageIconSource.FromUri("avares://MyApp/Assets/home-32.png", 32)); // used at 16 on a 2× screen

PageRegistry.Register<HomePage>(icon: icon);

var best = icon.GetSource(size: 16, scaling: 2);   // home-32.png
```

## See also

- [PageIconAttribute](PageIconAttribute.md), [PageIconView](PageIconView.md), [PageMenu](PageMenu.md)
