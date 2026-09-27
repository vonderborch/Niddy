# PageRegistry

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageRegistry.cs) · [IPageProvider](../../../src/Niddy.Avalonia/PageSystem/PageProvider.cs)

The registered pages and the page tree they form. Pages are registered in one of two ways, and you can use both together:

- **Automatically.** Add the [`Niddy.Avalonia.Generators`](../../Niddy.Avalonia.Generators/PageRegistrationGenerator.md) package and tag pages with [`[PageRegistration]`](PageRegistrationAttribute.md). This covers pages in referenced projects that use the generator, and pages in assemblies loaded at runtime (e.g. plugins via `Assembly.LoadFrom`) as soon as they load. No reflection is used.
- **Explicitly.** Call `Register`, e.g. in `NiddyApp.Configure`, before the first `PageView` is shown.

A page type is registered once. Registering it again with the same settings is harmless. Registering it with different settings, or in a way that makes a page its own ancestor, throws `InvalidOperationException`.

## API

| Member | Description |
|---|---|
| `Register<TPage>(string? displayName = null, bool keepAlive = false, IEnumerable<Type>? parents = null, IEnumerable<Type>? children = null, bool? topLevel = null, PageIcon? icon = null, string? shortcut = null)` | Registers a page with a parameterless constructor. `topLevel: null` means "top-level when it has no parents". Returns the [`PageRegistration`](PageRegistration.md). |
| `Register<TPage>(Func<TPage> factory, …same…)` | Creates the page with a factory, e.g. from a DI container. |
| `Include<TProvider>()` where `TProvider : IPageProvider, new()` | Registers a provider's pages, once. Generated providers use it for referenced projects. |
| `Find(PageId)`, `Find<TPage>()` | The registration, or null. Also `SettingsPage.PageId.Registration`. |
| `GetPages(PageId? parent = null)` | The child pages of `parent`: its declared children in order, then pages naming it as a parent, in registration order. Null returns the top-level pages. |
| `GetAllPages()` | Every page, in registration order. |
| `event PagesChanged` | Raised when pages are registered. **May be on a background thread** (e.g. a plugin loading). |

`Register` throws `ArgumentException` if a parent or child isn't a page type or the shortcut is invalid.

### `IPageProvider` and `[PageProvider]`

`IPageProvider.RegisterPages()` registers a set of pages. The generator emits one per assembly, marked with an assembly-level `[PageProvider(typeof(...))]` so the registry finds it when the assembly loads. You don't normally implement it yourself.

## Examples

```csharp
public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        PageRegistry.Register<HomePage>(displayName: "Home", shortcut: "Primary+1",
            icon: PageIcon.FromData("M10,20 V14 H14 V20 H19 V12 H22 L12,3 2,12 H5 V20 Z", 24));
        PageRegistry.Register<SettingsPage>(keepAlive: true, shortcut: "Primary+Shift+S");
        PageRegistry.Register<GeneralSettingsPage>(parents: [typeof(SettingsPage)]);
        PageRegistry.Register(() => new ReportsPage(_services.GetRequiredService<IReports>()));
        PageRegistry.Register<LoginPage>(topLevel: false);   // not in menus

        options.StartingPage = HomePage.PageId;
    }
}
```

Reacting to plugin pages:

```csharp
PageRegistry.PagesChanged += (_, _) =>
    Dispatcher.UIThread.Post(() => Log($"{PageRegistry.GetAllPages().Count} pages"));
```

Walking the tree:

```csharp
void Print(PageId? parent, int depth)
{
    foreach (var page in PageRegistry.GetPages(parent))
    {
        Console.WriteLine($"{new string(' ', depth * 2)}{page.DisplayName}");
        Print(page.PageId, depth + 1);
    }
}
Print(null, 0);
```

## See also

- [PageRegistrationAttribute](PageRegistrationAttribute.md), [PageRegistration](PageRegistration.md), [Generator diagnostics](../../Niddy.Avalonia.Generators/Diagnostics.md)
- [examples/plugin-pages](../../examples/plugin-pages.md)
