# PageId

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageId.cs)

Identifies a page by its type. Every page type gets a static `PageId` property through a C# 14 extension (`PageIdExtensions`), so no generator or `partial` is needed. Just import `Niddy.Avalonia.PageSystem` and write `SettingsPage.PageId`. Two IDs are equal when their page types are the same.

## API

| Member | Description |
|---|---|
| `TPage.PageId` (extension) | `PageId<TPage>`. Inside the page class itself write `SettingsPage.PageId`, since a bare `PageId` means the type. |
| `static PageId<TPage> Of<TPage>()` | Same as above. |
| `static PageId Of(Type pageType)` | Throws `ArgumentException` if the type isn't a `Page`. |
| `Type PageType` | |
| `PageRegistration? Registration` | Null if not registered (yet). |
| `implicit operator Type` | So `PageView.StartingPage = HomePage.PageId` works. |
| `==`, `!=`, `Equals`, `GetHashCode` | By page type. `ToString()` returns the type name. |
| `PageId<TPage>.Instance` | The cached ID. |

In AXAML, name pages with `{x:Type local:SettingsPage}`.

## Example

```csharp
options.StartingPage = HomePage.PageId;
NavigateTo(SettingsPage.PageId, parameter: "privacy");

PageId fromPlugin = PageId.Of(pluginAssembly.GetType("Plugin.ReportsPage")!);
bool isHome = PageControlDataContext.CurrentPageId == HomePage.PageId;
string? name = SettingsPage.PageId.Registration?.DisplayName;
```

## See also

- [Page](Page.md), [PageRegistration](PageRegistration.md)
