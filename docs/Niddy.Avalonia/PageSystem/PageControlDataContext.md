# PageControlDataContext

`Niddy.Avalonia.PageSystem` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/PageSystem/PageControlDataContext.cs)

The navigation state of a [`PageView`](PageView.md). It holds the current page, the pages available to the view and the back stack, and has methods to navigate. It's a ReactiveUI `ReactiveObject`, so navigation bars can bind to it. Pages get it as `Page.PageControlDataContext`, and code outside pages uses `PageView.PageControlDataContext`.

Every navigation respects `LockToCurrentPage` and lets the current page cancel it (`OnNavigatingFrom`). Properties change after the new page is shown.

## API

### State

| Member | Description |
|---|---|
| `PageId? ParentPage` | Whose child pages this view shows; null for top-level. |
| `IReadOnlyList<PageRegistration> AvailablePages` | The child pages of `ParentPage`, or the top-level pages. Updates as pages are registered. |
| `PageRegistration? CurrentPage`, `PageId? CurrentPageId`, `string? CurrentPageName` | Null before the first navigation, or for a page pushed without a registration. |
| `PageId? CurrentParentPage` | The parent the current page was reached from, or its only parent. |
| `Page? ActivePage` | The page instance being shown. |
| `bool CanGoBack` | |
| `bool LockToCurrentPage` | While true, every navigation (including back) is refused and returns false. |
| `int MaxBackStackDepth` | Default 20. Previous pages stay alive, so this bounds memory. 0 disables the back stack. |
| `event Navigated` | After every navigation. |

### Navigation

All of these return true if the page changed.

| Method | Description |
|---|---|
| `NavigateTo(PageId pageId)` | For binding a button's `Command` with the page ID as `CommandParameter`. |
| `NavigateTo(PageId, bool addToBackStack = true, object? parameter = null)` | Any registered page. Returns false if navigation is locked, the page cancelled it, or the page is already shown and no parameter was given. Throws `ArgumentOutOfRangeException` if the page isn't registered. |
| `NavigateTo<TPage>(…)` | |
| `NavigateToChild<TPage>(…)` | A child of the current page. If the current page has a nested `PageView` for its children, that view navigates. Otherwise this one does, and `NavigateToParent` comes back. Throws if `TPage` isn't a child. |
| `NavigateToSibling<TPage>(…)` | A page with the same parent, or another top-level page. Throws if it isn't a sibling. |
| `NavigateToParent(bool addToBackStack = true, object? parameter = null)` | Up the tree (not the back stack). False if the parent is unknown (a page with several parents that was navigated to directly) or already shown around a nested view. |
| `Push(Page page, …)`, `Push<TPage>(…)` | Shows a page without registering it, e.g. one with constructor arguments. |
| `GoBack()` | Restores the previous page instance and its state. |
| `ClearBackStack()` | |

Pass `addToBackStack: false` for top-level switches such as tabs or a sidebar, so back doesn't retrace every switch.

## Examples

A simple title bar in the app's layout, bound to the app's page view:

```csharp
options.Layout = pages =>
{
    var back = new Button { Content = "Back" };
    back.Click += (_, _) => pages.GoBack();
    back.Bind(Visual.IsVisibleProperty, new Binding(nameof(PageControlDataContext.CanGoBack)) { Source = pages.PageControlDataContext });

    var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
    title.Bind(TextBlock.TextProperty, new Binding(nameof(PageControlDataContext.CurrentPageName)) { Source = pages.PageControlDataContext });

    var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { back, title } };
    DockPanel.SetDock(bar, Dock.Top);
    return new DockPanel { Children = { bar, pages } };
};
```

For a list of pages, use [PageMenu](PageMenu.md) rather than binding `AvailablePages` yourself.

Locking navigation during a critical operation:

```csharp
var nav = PageControlDataContext;
nav.LockToCurrentPage = true;
try { await PageDataContext.MigrateAsync(); }
finally { nav.LockToCurrentPage = false; }
```

Relative navigation from a page:

```csharp
NavigateToChild<AdvancedSettingsPage>();
NavigateToSibling<GeneralSettingsPage>();
NavigateToParent();
```

## See also

- [PageView](PageView.md), [Page](Page.md), [PageMenu](PageMenu.md), a ready-made menu over this state
