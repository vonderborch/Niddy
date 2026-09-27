# PageIconAttribute

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageIconAttribute.cs)

Gives a [`[PageRegistration]`](PageRegistrationAttribute.md) page its [`PageIcon`](PageIcon.md). Repeat it to offer several sizes, e.g. a simpler path at 16 and a detailed one at 24, or bitmaps at 24 and 48 for high-DPI screens. The best one is picked for the size it's shown at. Like the registration, the generator reads it at compile time.

Set exactly one of `Data`, `Source` or `ResourceKey`. Otherwise you get NIDDY006, and the same happens for a negative `Size` or a relative `Source`. A `[PageIcon]` on a class without `[PageRegistration]` gives NIDDY007.

## API

| Member | Description |
|---|---|
| `PageIconAttribute()`, `PageIconAttribute(string data)` | |
| `string? Data` | SVG-style path data, filled with the foreground color. |
| `string? Source` | An absolute bitmap URI, e.g. `avares://MyApp/Assets/home-24.png`. |
| `string? ResourceKey` | The key of a `Geometry` or image resource, e.g. a `StreamGeometry` in App resources, so it can follow the theme. |
| `double Size` | The size this source was made for (the path's square side, or the bitmap's width in pixels). 0, the default, means any size. |

## Example

```csharp
[PageRegistration(DisplayName = "Home")]
[PageIcon("M10,20 V14 H14 V20 H19 V12 H22 L12,3 2,12 H5 V20 Z", Size = 24)]  // path data in a 24×24 box
[PageIcon(Source = "avares://MyApp/Assets/home-16.png", Size = 16)]             // a bitmap made for 16 px
public partial class HomePage : Page;

[PageRegistration]
[PageIcon(ResourceKey = "SettingsIcon")]    // <StreamGeometry x:Key="SettingsIcon">…</StreamGeometry>
public partial class SettingsPage : Page;
```

## See also

- [PageIcon](PageIcon.md), [Diagnostics](../../Niddy.Avalonia.Generators/Diagnostics.md)
