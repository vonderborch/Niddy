# Page

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/Page.cs) · [event args](../../../src/Niddy.Avalonia/PageSystem/PageNavigationEventArgs.cs)

The base class of every page shown in a [`PageView`](PageView.md). A page is a `UserControl`. Most pages derive from `Page<TAppDataContext, TPageDataContext>`, which gives them three data contexts:

| Data context | What it is |
|---|---|
| `AppDataContext` (`TAppDataContext`) | The one app-wide object from `NiddyAppOptions.AppDataContext` or `PageView.AppDataContext`, shared by every page. |
| `PageControlDataContext` | The page view's navigation state ([`PageControlDataContext`](PageControlDataContext.md)), shared by every page in that view. |
| `PageDataContext` (`TPageDataContext`) | The page's own, created the first time it's shown and kept as long as the page. It's also the page's `DataContext`, so AXAML bindings bind to it. It's disposed when the page closes if it's `IDisposable`. |

Derive from the non-generic `Page` for a simple page. Its `DataContext` is the app data context.

A page's ID is its type: `SettingsPage.PageId` (see [PageId](PageId.md)). Register it with [`[PageRegistration]`](PageRegistrationAttribute.md) and the generator, or with [`PageRegistry.Register`](PageRegistry.md). You can also show it without registering it, using `Push`.

> **Avalonia 12 name clash:** Avalonia 12 has its own `Avalonia.Controls.Page`. `Page<TApp, TPage>` doesn't clash. But a class deriving from the non-generic `Page` in a file with `using Avalonia.Controls;` is ambiguous. Add `using Page = Niddy.Avalonia.PageSystem.Page;`.

## API

### `Page`

| Member | Description |
|---|---|
| `PageControlDataContext PageControlDataContext` | Throws `InvalidOperationException` before the page is first shown. |
| `PageRegistration? Registration` | Null if the page was pushed without one. |
| `AppMode AppMode` | `NiddyApp.CurrentMode`: desktop or mobile. |
| `protected bool CanGoBack` | |
| `protected NavigateTo(PageId, addToBackStack = true, parameter = null)`, `NavigateTo<TPage>(…)` | See [PageControlDataContext](PageControlDataContext.md). |
| `protected NavigateToChild<TPage>(…)`, `NavigateToSibling<TPage>(…)`, `NavigateToParent(…)` | Relative navigation along the page tree. |
| `protected Push(Page page, …)`, `GoBack()` | |
| `protected virtual OnNavigatingFrom(PageNavigatingFromEventArgs e)` | Called before the page is left. Set `e.Cancel` to stay. |
| `protected virtual OnNavigatedTo(PageNavigationEventArgs e)` | Called after the page is shown, **including on going back**. The data contexts are set by then, and `e.Parameter` holds the navigation parameter. |
| `protected virtual OnNavigatedFrom(PageNavigationEventArgs e)` | Called after another page replaces this one. The page may still come back. |
| `protected virtual OnClosed()` | Called once the page can't be shown again: it was left without adding it to the back stack, going back left it, or it dropped off the back stack. Release resources here. Kept-alive pages are never closed. |

All navigation methods return `bool`: true if the page changed.

### `Page<TAppDataContext, TPageDataContext>`

| Member | Description |
|---|---|
| `TAppDataContext AppDataContext` | Throws if the page view's app data context isn't a `TAppDataContext`, or if the page hasn't been shown yet. |
| `TPageDataContext PageDataContext` | Also the `DataContext`. |
| `protected virtual TPageDataContext CreatePageDataContext()` | Defaults to the parameterless constructor. `AppDataContext` and `PageControlDataContext` are available here. |

### `PageNavigationEventArgs`

| Member | Description |
|---|---|
| `NavigationMode Mode` | `Forward` (to a page by ID, or a pushed page) or `Back`. |
| `PageRegistration? From`, `To` | Null for pages pushed without a registration. |
| `object? Parameter` | The navigation parameter. Null when going back. |

### `PageNavigatingFromEventArgs : PageNavigationEventArgs`

| Member | Description |
|---|---|
| `bool Cancel` | Set to true to stay on the page. |
| `bool Continue()` | Performs the cancelled navigation after all, without asking the page again. Does nothing (and returns false) if another navigation happened since. |

## Examples

Code-behind:

```csharp
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.PageSystem;

[PageRegistration(KeepAlive = true, Children = [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage)])]
[PageIcon("M12 8a4 4 0 1 0 0 8a4 4 0 1 0 0-8Z", Size = 24)]
public partial class SettingsPage : Page<MainDataContext, SettingsPageDataContext>
{
    public SettingsPage() => InitializeComponent();

    // Optional: build the page data context yourself.
    protected override SettingsPageDataContext CreatePageDataContext() => new(AppDataContext.Settings);

    protected override void OnNavigatedTo(PageNavigationEventArgs e)
    {
        if (e.Mode == NavigationMode.Forward && e.Parameter is string section)
            PageDataContext.ScrollTo(section);
    }

    // A navigation guard: cancel, ask, then continue.
    protected override void OnNavigatingFrom(PageNavigatingFromEventArgs e)
    {
        if (!PageDataContext.HasChanges) return;
        e.Cancel = true;
        _ = ConfirmLeavingAsync(e);
    }

    private async Task ConfirmLeavingAsync(PageNavigatingFromEventArgs e)
    {
        if (await Dialog.Confirmation.Open(this, "Discard changes?", "Your changes will be lost."))
            e.Continue();
    }
}
```

AXAML. The root element is the non-generic `Page`, and `x:DataType` is the page data context:

```xml
<pageSystem:Page xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:pageSystem="clr-namespace:Niddy.Avalonia.PageSystem;assembly=Niddy.Avalonia"
                 xmlns:local="clr-namespace:MyApp"
                 x:Class="MyApp.SettingsPage"
                 x:DataType="local:SettingsPageDataContext">
    <DockPanel>
        <TextBox DockPanel.Dock="Top" Text="{Binding Name}" />
        <!-- Shows SettingsPage's child pages (inferred from the page this view is in) -->
        <pageSystem:PageView StartingPage="{x:Type local:GeneralSettingsPage}" />
    </DockPanel>
</pageSystem:Page>
```

A simple page with no data contexts of its own:

```csharp
using Avalonia.Controls;
using Page = Niddy.Avalonia.PageSystem.Page;

[PageRegistration(DisplayName = "About")]
public partial class AboutPage : Page
{
    public AboutPage() => InitializeComponent();
}
```

## See also

- [PageView](PageView.md), [PageControlDataContext](PageControlDataContext.md), [PageRegistrationAttribute](PageRegistrationAttribute.md)
- [DataContextBase](../DataContexts/DataContextBase.md), a good base for `TPageDataContext`
