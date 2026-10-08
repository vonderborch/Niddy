# PageRegistry

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageRegistry.cs) · [IPageProvider](../../../src/Niddy.Avalonia/PageSystem/PageProvider.cs)

The registered pages and the page tree they form. Pages are registered in one of two ways, and you can use both together:

- **Automatically.** Add the [`Niddy.Avalonia.Generators`](../../Niddy.Avalonia.Generators/PageRegistrationGenerator.md) package and tag pages with [`[PageRegistration]`](PageRegistrationAttribute.md). This covers pages in referenced projects that use the generator, and pages in assemblies loaded at runtime (e.g. plugins via `Assembly.LoadFrom`) as soon as they load. No reflection is used.
- **Explicitly.** Call `Register`, e.g. in `NiddyApp.Configure`, before the first `PageView` is shown.

A page type is registered once. Registering it again with the same settings is harmless. Registering it with different settings, or in a way that makes a page its own ancestor, throws `InvalidOperationException`.

## API

| Member | Description |
|---|---|
| `Register<TPage>(string? displayName = null, bool keepAlive = false, IEnumerable<Type>? parents = null, IEnumerable<Type>? children = null, bool? topLevel = null, PageIcon? icon = null, string? shortcut = null, string? sortKey = null)` | Registers a page with a parameterless constructor. `topLevel: null` means "top-level when it has no parents". `sortKey` is what the page sorts by instead of its display name (see [Ordering](#ordering)). Returns the [`PageRegistration`](PageRegistration.md). |
| `Register<TPage>(Func<TPage> factory, …same…)` | Creates the page with a factory, e.g. from a DI container. |
| `Include<TProvider>()` where `TProvider : IPageProvider, new()` | Registers a provider's pages, once. Generated providers use it for referenced projects. |
| `Find(PageId)`, `Find<TPage>()` | The registration, or null. Also `SettingsPage.PageId.Registration`. |
| `GetPages(PageId? parent = null)` | The child pages of `parent`, or for null the top-level pages, in `Sort` order (reversed if `SortDescending`). See [Ordering](#ordering). |
| `GetAllPages()` | Every page, in registration order. |
| `Sort` | A `Comparison<PageRegistration>` that decides the order of `GetPages`, and so of every `PageView` and `PageMenu`. Defaults to `Alphabetical`. Setting it to null throws `ArgumentNullException`. |
| `SortDescending` | Reverses the `Sort` order. Default false. |
| `Alphabetical` | The default `Sort`: by `SortKey` (the display name unless overridden), ignoring case, in the current culture; ties by display name, then by the page class's full name. |
| `DeclaredOrder` | A `Sort` that treats every page as equal, so pages keep their declared order (see below). |
| `event PagesChanged` | Raised when pages are registered, and when `Sort` or `SortDescending` changes. **May be on a background thread** (e.g. a plugin loading). |

`Register` throws `ArgumentException` if a parent or child isn't a page type or the shortcut is invalid.

### Ordering

`GetPages` first lists pages in their declared order:

- top-level pages in registration order (with the generator, that is file path, then position in the file);
- a page's children in the order it names them in `children`, then the pages that name it in `parents`, in registration order.

It then sorts them with `Sort`. The sort is stable, so pages that compare equal keep their declared order, and `DeclaredOrder` keeps it entirely. By default the order is alphabetical, including a parent's declared `children`.

To move one page without writing a comparison, give it a sort key: `sortKey: "0"` (or `SortKey = "0"` on [`[PageRegistration]`](PageRegistrationAttribute.md)) puts a dashboard first, and `"zzz"` puts settings last. Keys are compared with the current culture, where punctuation such as `~` doesn't reliably sort after letters.

For a fixed order, set `Sort` (or [`NiddyAppOptions.PageSort`](../Hosting/NiddyAppOptions.md), which `NiddyApp` copies here during startup):

```csharp
Type[] order = [typeof(WorkflowsPage), typeof(RunsPage), typeof(IssuesPage), typeof(SettingsPage)];
PageRegistry.Sort = (a, b) => Array.IndexOf(order, a.PageType).CompareTo(Array.IndexOf(order, b.PageType));
```

Pages missing from `order` get index -1 and so come first; put them last with `(uint)Array.IndexOf(...)` if you prefer. `Sort` and `SortDescending` apply everywhere, since the registry is global. Changing either raises `PagesChanged`, so open menus re-sort.

### `IPageProvider` and `[PageProvider]`

`IPageProvider.RegisterPages()` registers a set of pages. The generator emits one per assembly, marked with an assembly-level `[PageProvider(typeof(...))]` so the registry finds it when the assembly loads. You don't normally implement it yourself.

`PageProviderAttribute(Type providerType)` is that marker, with a single property `Type ProviderType`. It's hidden from IntelliSense (`EditorBrowsable(Never)`) because only the generator should emit it. The provider type needs a public parameterless constructor.

## Examples

```csharp
public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        PageRegistry.Register<HomePage>(displayName: "Home", shortcut: "Primary+1",
            icon: PageIcon.FromData("M10,20 V14 H14 V20 H19 V12 H22 L12,3 2,12 H5 V20 Z", 24));
        PageRegistry.Register<SettingsPage>(keepAlive: true, shortcut: "Primary+Shift+S", sortKey: "zzz");   // last
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
    foreach (PageRegistration page in PageRegistry.GetPages(parent))
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
