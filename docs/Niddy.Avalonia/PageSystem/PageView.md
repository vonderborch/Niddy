# PageView

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageView.axaml.cs)

Shows one [`Page`](Page.md) at a time and navigates between them, with a back stack. Its navigation state is [`PageControlDataContext`](PageControlDataContext.md), which its pages share and menus and navigation bars can bind to. [`NiddyApp`](../Hosting/NiddyApp.md) creates the app's page view (`NiddyApp.Current!.Shell.Pages`). Add more inside pages to show their child pages.

- The first page is `StartingPage`. It's shown when the view is added to a window or page, unless a page was already navigated to. Changing it later resets the view.
- **Nested views.** A page view inside a page shows that page's child pages if it has any, and otherwise the top-level pages. Set `ParentPage` to choose. Its `AppDataContext` defaults to that of the view showing the page.
- **Back requests.** The platform back request (Android back button, iOS back gesture, browser back) goes back a page. An open overlay dialog is dismissed first, and with nested views the most recently attached one handles it first.
- **Shortcuts.** Pressing the [shortcut](PageShortcut.md) of one of `AvailablePages` anywhere in the window navigates to it. Keys already handled, e.g. by a text box, are ignored.

## API

| Member | Description |
|---|---|
| `Type? StartingPage` | `{x:Type local:HomePage}` in AXAML, `HomePage.PageId` in code. |
| `Type? ParentPage` | Whose child pages `AvailablePages` lists. Null means top-level pages, or the containing page's children. |
| `object? AppDataContext` | Every page's app data context. Separate from the view's own `DataContext`. |
| `bool HandleBackRequests` | Default true. |
| `bool HandleShortcuts` | Default true. |
| `IPageTransition? PageTransition` | e.g. `new PageSlide(TimeSpan.FromMilliseconds(200))`. Runs in reverse when going back. Null switches instantly. |
| `PageControlDataContext PageControlDataContext` | |
| `event Navigated` | After every navigation, including back. |
| `CanGoBack`, `NavigateTo(PageId, …)`, `NavigateTo<TPage>(…)`, `Push(page, …)`, `Push<TPage>(…)`, `GoBack()`, `ClearBackStack()` | Shortcuts to `PageControlDataContext`. |

## Examples

Without `NiddyApp`:

```xml
<Window xmlns:niddy="https://github.com/vonderborch/Niddy" ...>
    <niddy:DialogOverlayHost>
        <niddy:ToastHost>
            <niddy:PageView StartingPage="{x:Type local:HomePage}" AppDataContext="{Binding}" />
        </niddy:ToastHost>
    </niddy:DialogOverlayHost>
</Window>
```

From code:

```csharp
var pages = NiddyApp.Current!.Shell.Pages;
pages.NavigateTo<DetailsPage>(parameter: order);             // arrives in OnNavigatedTo
pages.NavigateTo(HomePage.PageId, addToBackStack: false);    // tab or sidebar switches
pages.Push(new OrderPage(order));                            // an unregistered page instance
if (pages.CanGoBack) pages.GoBack();                         // returns to the same instance
pages.Navigated += (_, e) => Log($"{e.From} -> {e.To}");
```

A nested view for child pages, with its own tab strip:

```xml
<DockPanel>
    <pageSystem:PageMenu Style="HorizontalStrip" Placement="TopLeft" Target="{Binding #Children}" />
    <pageSystem:PageView x:Name="Children" StartingPage="{x:Type local:GeneralSettingsPage}" HandleShortcuts="False" />
</DockPanel>
```

## See also

- [Page](Page.md), [PageControlDataContext](PageControlDataContext.md), [PageMenu](PageMenu.md), [AppShell](../Hosting/AppShell.md)
