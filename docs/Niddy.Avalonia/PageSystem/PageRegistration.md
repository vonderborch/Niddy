# PageRegistration

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageRegistration.cs)

A page registered with the [`PageRegistry`](PageRegistry.md), and its place in the page tree. You get one from `PageRegistry.Register`, `PageRegistry.Find`, `SomePage.PageId.Registration`, `PageControlDataContext.AvailablePages` or `CurrentPage`, and a page's own `Registration`. `ToString()` returns `DisplayName`.

## API

| Member | Description |
|---|---|
| `PageId PageId`, `Type PageType` | |
| `string DisplayName` | Defaults to the class name without a `Page` suffix. |
| `PageIcon? Icon` | See [PageIcon](PageIcon.md). |
| `KeyGesture? Shortcut` | Parsed from the registration's shortcut string. |
| `bool KeepAlive` | |
| `IReadOnlyList<PageId> Parents` | Pages it names as parents, plus pages naming it as a child. Changes as pages are registered. |
| `IReadOnlyList<PageRegistration> Children` | Its declared children in order, then pages naming it as a parent. Only registered pages are included. |
| `bool IsTopLevel` | The explicit `TopLevel` if set, else `Parents.Count == 0`. |

## Example

```csharp
var settings = PageRegistry.Find<SettingsPage>()!;
foreach (var child in settings.Children)
    Console.WriteLine($"{child.DisplayName} {(child.Shortcut is { } k ? PageShortcut.Format(k) : "")}");
```

## See also

- [PageRegistry](PageRegistry.md), [PageId](PageId.md)
