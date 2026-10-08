# PageRegistrationAttribute

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageRegistrationAttribute.cs)

Registers a [`Page`](Page.md) and places it in the page tree. The [`Niddy.Avalonia.Generators`](../../Niddy.Avalonia.Generators/PageRegistrationGenerator.md) package reads it at compile time. **Without that package the attribute does nothing**; use [`PageRegistry.Register`](PageRegistry.md) instead. Mistakes are reported as [compile-time diagnostics](../../Niddy.Avalonia.Generators/Diagnostics.md) (NIDDY001–008).

The tree can be declared from either end. A parent lists its `Children`, and a child lists its `Parents`, e.g. a plugin page adding itself under an app page. Both are combined, and a page can have several parents.

Pages are listed in [`PageRegistry.Sort`](PageRegistry.md#ordering) order, alphabetically by default. `SortKey` moves one page without changing its name.

## API

| Property | Description |
|---|---|
| `string? DisplayName` | Shown in menus. Defaults to the class name without a `Page` suffix (`SettingsPage` → "Settings"). |
| `bool KeepAlive` | Keep one instance and reuse it on every navigation, keeping its state. Good for tabs and sidebar sections. Default false (a new page each time). |
| `Type[]? Parents` | The pages this is a child of. They don't have to be registered yet. |
| `Type[]? Children` | Child pages. Each must be registered itself to appear. They are listed in `PageRegistry.Sort` order, so alphabetically by default; this order is used only with `PageRegistry.DeclaredOrder` (or as a tie-break). |
| `bool TopLevel` | If unset, the page is top-level when it has no parents. `true` also lists a child page at the top level. `false` hides a parentless page from menus. |
| `string? Shortcut` | A key gesture that navigates to the page, e.g. `"Primary+1"` or `"Ctrl+Shift+S"`. See [PageShortcut](PageShortcut.md). |
| `string? SortKey` | What the page sorts by in the default alphabetical order, instead of its display name. E.g. `"0"` puts a dashboard first and `"zzz"` puts settings last. |

The class must be a non-abstract, non-generic page that isn't nested as private or protected, with a public or internal parameterless constructor (otherwise NIDDY003).

## Examples

```csharp
[PageRegistration(DisplayName = "Home", Shortcut = "Primary+1")]
[PageIcon("M10,20 V14 H14 V20 H19 V12 H22 L12,3 2,12 H5 V20 Z", Size = 24)]
public partial class HomePage : Page<MainDataContext, HomePageDataContext>;

// Last in the menu, whatever the other pages are called:
[PageRegistration(KeepAlive = true, SortKey = "zzz", Children = [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage)])]
public partial class SettingsPage : Page<MainDataContext, SettingsPageDataContext>;

[PageRegistration]
public partial class GeneralSettingsPage : Page<MainDataContext, GeneralSettingsPageDataContext>;

// Under two parents, and also at the top level:
[PageRegistration(Parents = [typeof(SettingsPage), typeof(HelpPage)], TopLevel = true)]
public partial class AboutPage : Page;

// Parentless, but kept out of menus:
[PageRegistration(DisplayName = "Log in", TopLevel = false)]
public partial class LoginPage : Page;
```

## See also

- [PageRegistry](PageRegistry.md), [PageIconAttribute](PageIconAttribute.md), [Diagnostics](../../Niddy.Avalonia.Generators/Diagnostics.md)
