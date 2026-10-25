# Niddy: a guide for AI assistants

This one file tells you how to use the whole library in an app. It's written for AI coding assistants, but people can read it too. Every API here matches the source. For the complete API of a class, open its page from the [docs index](README.md): `docs/<Package>/<Folder>/<Class>.md`.

**Stack:** .NET 10, C# 14, Avalonia 12. Packages:

| Package | Use it for | Namespaces |
|---|---|---|
| `Niddy.Core` | Anything (no UI): results, settings, logging, paths, files, archives, JSON, retry, secure storage, threading, platform info | Namespaces follow the folders: `Niddy.Core` (`DisposeIfPossible`), `.Collections`, `.GitHub`, `.IO`, `.IO.Archives`, `.IO.Backups`, `.Logging`, `.Logging.Files`, `.Logging.Batching`, `.Resilience`, `.Results`, `.Security`, `.Serialization`, `.Settings`, `.SystemInfo`, `.Text`, `.Threading`, `.Web` |
| `Niddy.Avalonia` | Avalonia apps: app hosting, pages, dialogs, toasts, tables, state memory, converters | Namespaces follow the folders: `Niddy.Avalonia` (`FilePicker`, `UriLauncher`, `WindowNotification`, `ScreenPlacement`), `.Hosting`, `.PageSystem` (everything for pages: `Page`, `PageView`, `[PageRegistration]`, `PageRegistry`, `[PageIcon]`, `PageMenu`), `.Dialogs`, `.Toasts`, `.Tables` (`TableGrid`), `.State`, `.Converters`, `.DataContexts`, `.Layout`, `.Theme` |
| `Niddy.Avalonia.Generators` | Compile-time registration of `[PageRegistration]` pages. **Without it, `[PageRegistration]` does nothing.** | generates `Niddy.Generated.Pages_<Assembly>` |

XAML: `xmlns:niddy="https://github.com/vonderborch/Niddy"` covers every public `Niddy.Avalonia` namespace. For the `Page` root element, examples use `xmlns:pageSystem="clr-namespace:Niddy.Avalonia.PageSystem;assembly=Niddy.Avalonia"`.

---

## Rules of thumb

1. **Build apps on `NiddyApp`.** Derive `App` from `NiddyApp`, set `options.StartingPage` in `Configure`, and write pages. Don't write a `MainWindow` or `App.axaml`. `NiddyApp` builds the window (desktop) or view (mobile/browser), with dialogs, toasts, theme, window memory and exception handling.
2. **Screens are pages.** Derive from `Page<TAppDataContext, TPageDataContext>`. The **second type argument is the page's data context class**, not the page. Register pages with `[PageRegistration]` plus the generator package.
3. **Navigate with page IDs or types, never by creating windows:** `NavigateTo<SettingsPage>()`, `NavigateTo(SettingsPage.PageId)`, `Push(new DetailsPage(x))`, `GoBack()`.
4. **Use `Dialog.*` for any modal question** and `Toast.*` for any passing message. Always pass `this` (a page or control in the visual tree) as the parent or source.
5. **Keep one code path for every platform.** Use `FilePicker` handle methods, not `*Path`. Use `UriLauncher.TryLaunchAsync`, not `Process.Start`. Use `Responsive` for layout switches. Check `Page.AppMode` for behavior switches.
6. **Put state in the right store:** user settings → `SettingsStore<T>`. Window size and splitters → `WindowMemory` / `LayoutMemory` (automatic). Secrets → `ISecureStorage`, never settings. Folders → `AppPaths`.
7. **Return expected failures as values** (`Result`/`Result<T>`). Reserve exceptions for bugs. Unhandled exceptions are caught, logged and shown by `GlobalExceptionHandler`.

---

## App skeleton

```csharp
// App.cs
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;
using Niddy.Core.IO;
using Niddy.Core.Logging;

public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        AppPaths paths = AppPaths.For("MyApp").EnsureCreated();     // NiddyApp.Paths isn't usable inside Configure
        options.AppName = "MyApp";
        options.Title = "My App";
        options.StartingPage = HomePage.PageId;                 // required
        options.AppDataContext = new MainDataContext(paths);    // every page's AppDataContext
        options.Layout = pages => new PageMenu { Content = pages, Mode = PageMenuMode.Tree };   // optional nav menu
        options.LoggerFactory = NiddyLogging.CreateFactory(paths);
        options.ReportIssueLink = "https://github.com/me/myapp/issues/new";
    }
}

// Program.cs (desktop)
public static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
}
```

Other `NiddyAppOptions` settings: `Organization`, `Mode` (`AppMode.Auto|Desktop|Mobile`), `PageTransition`, `HandleBackRequests`, `UseFluentTheme`, `Theme` (`AppTheme?`), `DialogMode`, `ToastPlacement`, `ToastNarrowPlacement`, `ToastDuration`, `Width/Height/MinWidth/MinHeight`, `MobilePreviewWidth/Height`, `RememberWindowState`, `ConfigureWindow`, `HandleUnhandledExceptions`, `ShowUnhandledExceptionDialog`, `OnUnhandledException`, and `PageSort` / `PageSortDescending` (menu order, see below). `options.Mode = AppMode.Mobile` on desktop previews the phone layout in a 390×844 window.

At runtime: `NiddyApp.Current!.Shell.Pages` (the root `PageView`), `.Shell.Toasts`, `.Shell.Dialogs`, `NiddyApp.Current.Paths`, `NiddyApp.CurrentMode`.

Details: [NiddyApp](Niddy.Avalonia/Hosting/NiddyApp.md), [NiddyAppOptions](Niddy.Avalonia/Hosting/NiddyAppOptions.md), [examples/app-setup](examples/app-setup.md).

---

## Pages

```csharp
using Niddy.Avalonia.DataContexts;
using Niddy.Avalonia.PageSystem;
using Niddy.Core.IO;

[PageRegistration(DisplayName = "Home", Shortcut = "Primary+1")]           // Primary = Cmd on Apple platforms, Ctrl elsewhere
[PageIcon("M10,20 V14 H14 V20 H19 V12 H22 L12,3 2,12 H5 V20 Z", Size = 24)] // SVG path data, or Source = "avares://…"
public partial class HomePage : Page<MainDataContext, HomePageDataContext>
{
    public HomePage() => InitializeComponent();

    protected override HomePageDataContext CreatePageDataContext() => new(AppDataContext.Paths);   // optional

    protected override void OnNavigatedTo(PageNavigationEventArgs e)     // also runs when coming back
    {
        if (e.Parameter is string search) PageDataContext.Search = search;
    }
}

public sealed class HomePageDataContext(AppPaths paths) : DataContextBase { /* ReactiveUI properties */ }
```

```xml
<pageSystem:Page xmlns="https://github.com/avaloniaui"
                 xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                 xmlns:pageSystem="clr-namespace:Niddy.Avalonia.PageSystem;assembly=Niddy.Avalonia"
                 xmlns:local="clr-namespace:MyApp"
                 x:Class="MyApp.HomePage"
                 x:DataType="local:HomePageDataContext">
    <!-- DataContext is the page data context -->
</pageSystem:Page>
```

- **Three data contexts:** `AppDataContext` (shared by all pages, from `options.AppDataContext`), `PageDataContext` (the page's own, which is also `DataContext`, created on first show and disposed on close if `IDisposable`), and `PageControlDataContext` (navigation state).
- **`[PageRegistration]` properties:**
  - `DisplayName` (defaults to the class name minus "Page").
  - `KeepAlive` (reuse one instance; good for tabs).
  - `Parents` / `Children` (build the page tree from either end).
  - `TopLevel`.
  - `Shortcut`.
  - `SortKey` (sorts by this instead of the display name, e.g. `"0"` for first, `"zzz"` for last).
- **Navigation, from inside a page:** `NavigateTo<T>(addToBackStack = true, parameter = null)`, `NavigateTo(PageId, …)`, `NavigateToChild<T>()`, `NavigateToSibling<T>()`, `NavigateToParent()`, `Push(Page)`, `GoBack()`, `CanGoBack`. Each returns `bool`.
- **Navigation, from outside a page:** use `NiddyApp.Current!.Shell.Pages` (a `PageView`) with the same methods.
- **Guards:** override `OnNavigatingFrom(PageNavigatingFromEventArgs e)`, set `e.Cancel = true`, ask the user, then call `e.Continue()`.
- **Clean-up:** override `OnClosed()` (never called for kept-alive pages).
- **Nested pages:** put a `<pageSystem:PageView StartingPage="{x:Type local:ChildPage}" />` inside a page. It shows that page's children.
- **Menus:** `PageMenu` lists registered pages (`Mode`: `Flat` for one level, or `Tree`). It turns into a bottom navigation bar on mobile or narrow windows.
- **Order:** menus and `AvailablePages` list pages alphabetically by `SortKey` (the display name unless set), **including a parent's `Children`**. `PageRegistry.Sort` (or `options.PageSort`) takes any `Comparison<PageRegistration>`, `PageRegistry.SortDescending` reverses it, and `PageRegistry.DeclaredOrder` keeps the declared order (registration order for top-level pages, which is file-path order with the generator).
- **Manual registration** (no generator, or for DI factories): `PageRegistry.Register<TPage>(displayName, keepAlive, parents, children, topLevel, icon, shortcut, sortKey)` or `Register(() => new TPage(deps), …)`.
- **Tests:** `new AppShell(new NiddyAppOptions { StartingPage = HomePage.PageId, AppDataContext = … })` builds the app's page view, toast host and dialog host without a `NiddyApp`. Put it in a `Window` in a headless test.
- **Plugins:** pages in assemblies loaded later (`Assembly.LoadFrom`) register themselves if the plugin uses the generator. `PageRegistry.PagesChanged` may fire off the UI thread.

**Avalonia 12 name clash:** a class deriving from the *non-generic* `Page` in a file with `using Avalonia.Controls;` must add `using Page = Niddy.Avalonia.PageSystem.Page;`.

Details: [Page](Niddy.Avalonia/PageSystem/Page.md), [PageView](Niddy.Avalonia/PageSystem/PageView.md), [PageRegistrationAttribute](Niddy.Avalonia/PageSystem/PageRegistrationAttribute.md), [PageRegistry](Niddy.Avalonia/PageSystem/PageRegistry.md), [PageMenu](Niddy.Avalonia/PageSystem/PageMenu.md), [generator diagnostics NIDDY001–008](Niddy.Avalonia.Generators/Diagnostics.md), [examples/plugin-pages](examples/plugin-pages.md).

---

## Dialogs (`Niddy.Avalonia.Dialogs`)

Every dialog is `await Dialog.<Kind>.Open(parent, …)`, where `parent` is `this`. It shows as an overlay (inside a `DialogOverlayHost`) or a modal window, per `Dialog.DefaultMode` or the `displayMode:` argument. Escape, the window close button and platform back act as Cancel.

| Call | Returns |
|---|---|
| `Notification.Open(p, title, description)` | `Task` |
| `Confirmation.Open(p, title, description, defaultResult = false, yesButtonText = "Yes", …)` | `Task<bool>` |
| `Warning.Open(p, title, description, yesButtonText = "Proceed", noButtonText = "Cancel")` | `Task<bool>`; Enter doesn't confirm |
| `Input.Open(p, title, description, defaultValue = "", placeholderText = "")` | `Task<string?>`, null if cancelled |
| `MultiInput.Open(p, title, description, [("Name", ""), ("Email", "")])` | `Task<Dictionary<string,string>?>` keyed by label |
| `Selection.Open(p, title, description, items, x => x.Name)` | `Task<T?>` |
| `Progress.Open(p, title, description, async (progress, ct) => …)` | `Task<bool>`; runs on a **background thread**; progress **0–100** |
| `Exception.Open(p, title, description, exception, reportIssueLink)` | `Task` |
| `Color.Open(p, title, initialColor = null)` | `Task<Color?>` |
| `Markdown.Open(p, title, markdown, okButtonText = "OK", cancelButtonText = null)` | `Task<bool>` |
| `Table.Open(p, title, description, items, columns = null)` / `Table.Pick(…)` | `Task` / `Task<T?>`; columns are `new TableColumn<T>("Header", x => x.Value) { Format = …, AlignRight = true, Width = … }` |
| `Web.Open(p, title, uri, closeWhen: u => …, openInBrowserText = "Open in Browser")` | `Task<Uri?>`: the matched address, or the last address shown. Best with `displayMode: DialogDisplayMode.Window`. |
| `Web.OpenHtml(p, title, html, baseAddress = null, closeWhen = null)` | `Task<Uri?>` |
| `View.Open(p, view, title = null, description = null, okButtonText = "OK", cancelButtonText = "Cancel", canAccept = null, …, fit = ViewFit.Stretch, fill = false)` | `Task<bool>`, true for OK. `view` is any control not already shown, or a `Window` that isn't open (its content, title, `Width`, `Height` and `DataContext` are used; the content goes back afterwards). `View.Close(control, accepted)` closes it from inside. |
| `Show(p, new MyDialog(…), maxWidth = 500)` | `Task<TResult>` for a custom dialog |

All calls also accept `maxWidth`, `displayMode`, `windowWidth` and `windowHeight`. Markdown, Table, MultiInput, Exception, Selection and Web also take `contentHeight:`, a fixed height for their scrolling area (by default it fits up to 420/400/300/240/200, then scrolls; Web's is 560). `View.Open` takes it too, for the view; set it for a `DataGrid` or anything that scrolls inside itself, or pass `fill: true` instead.

**Fitting a view:** `View.Open`'s `fit:` is a `ViewFit`: `Stretch` (default: the view gets the dialog's width and the height it wants), `Center` (its own size, centered), `Scroll` (its own size, scroll bars both ways when bigger; the area fits up to 400), `Shrink` (scaled down to fit, never up) or `Scale` (scaled up or down to fit). `fill: true` makes the dialog as big as the host allows (the whole overlay, or a resizable window at 80% of its owner) and gives the view all the height between the description and the buttons; it can't be combined with `contentHeight`.

**Existing view or window in a dialog:** use `Dialog.View.Open` rather than writing a `DialogBase` class. Write a `DialogBase` when the dialog needs its own result type or buttons.

**Custom dialog:** create an AXAML file with `<dialogs:DialogLayout … Title="…" Description="…">` as the root. Put the body as its content and buttons in `<dialogs:DialogLayout.Buttons>`. In code-behind, derive from `DialogBase<TResult>`, implement `protected override TResult DismissResult`, and call `Close(result)` from buttons. You can also override `InitialFocus`, `CanDismiss` and `OnDismissed()`. Use a new instance per `Dialog.Show`.

Details: [Dialog](Niddy.Avalonia/Dialogs/Dialog.md), [DialogBase](Niddy.Avalonia/Dialogs/DialogBase.md), [DialogLayout](Niddy.Avalonia/Dialogs/DialogLayout.md), [examples/custom-dialog](examples/custom-dialog.md).

---

## Toasts (`Niddy.Avalonia.Toasts`)

```csharp
Toast.Show(this, "Saved", ToastType.Success);                                // Info | Success | Warning | Error
Toast.Show(this, "File deleted", actionText: "Undo", action: () => Restore(file));
Toast.Show(this, "Offline", ToastType.Error, duration: TimeSpan.Zero);       // stays until dismissed
Toast.Show(this, $"Synced {n}", key: "sync");                                 // same key updates in place

ToastHandle t = Toast.ShowProgress(this, "Uploading…", isIndeterminate: false, cancelText: "Cancel", cancel: cts.Cancel);
t.Report(0.5);                         // progress 0–1 (not 0–100)
t.Update("Almost done…");
t.Complete("Uploaded");                // or t.Complete("Failed", ToastType.Error); or t.Dismiss();
```

Call `Show`/`ShowProgress` on the UI thread. The host is the nearest `ToastHost` above `source` (logical, then visual), else the `NiddyApp` shell's. Without either they throw. `ToastHandle` methods are safe from any thread. Identical toasts merge with a "×N" count. Defaults: 3 s, or 6 s with an action.

Details: [Toast](Niddy.Avalonia/Toasts/Toast.md), [ToastHandle](Niddy.Avalonia/Toasts/ToastHandle.md), [ToastHost](Niddy.Avalonia/Toasts/ToastHost.md).

---

## Data contexts, converters, layout

- **`DataContextBase`** (ReactiveUI `ReactiveObject`):
  - `IsBusy` and `ErrorMessage`.
  - `protected RunAsync(Func<Task>, onError?)` / `RunAsync<T>(…)`. These set `IsBusy`, and on failure set `ErrorMessage` or call `onError`.
  - Use `this.RaiseAndSetIfChanged(ref field, value)` for properties.
- **Converters** (via `xmlns:niddy`):
  - `EnumToBoolConverter` for radio buttons bound to an enum.
  - `BoolToValueConverter` (TrueValue/FalseValue).
  - `CollectionConverters.IsEmpty` / `IsNotEmpty`.
  - `HumanizeConverters.FileSize`, `Duration`, `RelativeTime` and others (used with `{x:Static niddy:…}`).
  - `{niddy:EnumValues local:MyEnum}` for ComboBox items.
- **`Responsive`:** `niddy:Responsive.NarrowBelow="600"` (plus an optional `WideAbove`) adds the `narrow`/`medium`/`wide` classes based on **the control's own width**. Style against them.
- **`ThemeManager`:** `ThemeManager.Current` / `IsDarkMode` give the current light or dark theme. `ThemeChanged` and `WhenThemeChanged()` report changes.

Details: [DataContextBase](Niddy.Avalonia/DataContexts/DataContextBase.md), [Converters](README.md#niddyavalonia), [Responsive](Niddy.Avalonia/Layout/Responsive.md), [ThemeManager](Niddy.Avalonia/Theme/ThemeManager.md).

---

## Tables (`Niddy.Avalonia.Tables`)

`TableGrid` **is a `DataGrid`** (a subclass): everything DataGrid does works unchanged (`ItemsSource`, `Columns`, `AutoGenerateColumns`, `SelectedItem`, `CollectionView`, frozen columns, row details, styles). It adds: **read-only by default** (`IsReadOnly` defaults to true), optional **row locking** when editable, and **paging** with an automatic pager (first/previous/page box/next/last, rows per page, "61–90 of 352", refresh). **It isn't Avalonia 12.1's own `TableView`**, which is a different control in `Avalonia.Controls`.

```csharp
// Rows in memory: plain ItemsSource. Paged in memory; header sorting and CollectionView.Filter cover ALL rows, not just the page
TableGrid table = new() { PageSize = 50 };
table.Columns.Add(new TableGridColumn<Order>("Order", o => o.Number) { Format = o => $"#{o.Number}" });   // Format: text; Value still sorts
table.Columns.Add(new TableGridColumn<Order>("Customer", o => o.Customer) { Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 150 });
table.Columns.Add(new TableGridColumn<Order>("Total", o => o.Total) { FormatString = "C", CellClass = o => o.Total > 1000 ? "big" : null });   // numbers right-align
// TableGridColumn is a DataGridColumn: Width/MinWidth/CanUserSort/SortMemberPath are DataGrid's. Also: Alignment, NullText, Comparer, Foreground, CellTemplate = o => new MyBadge(o)
// Any DataGridColumn works too (DataGridTextColumn, DataGridCheckBoxColumn, DataGridTemplateColumn)
table.RowClass = item => ((Order)item).IsCancelled ? "muted" : null;   // style DataGridRow.muted
table.ItemsSource = orders;   // an ObservableCollection updates counts as it changes
table.CollectionView!.Filter = o => ((Order)o).Total > 0;   // counts and pages follow
table.ItemActivated += (_, e) => Open((Order)e.Item);   // double-tap or Enter (not on a row being edited)

// Editable with row locking: each row has a lock button (LockColumn, added first); locked rows don't enter edit
TableGrid lines = new() { IsReadOnly = false, LockRows = true, ItemsSource = orderLines };
lines.Columns.Add(new TableGridColumn<OrderLine>("Qty", l => l.Quantity) { Edit = (l, text) => l.Quantity = int.Parse(text) });   // throw = reject, cell stays in edit
lines.CanUnlockRow = item => !((OrderLine)item).IsShipped;   // refused rows get a disabled button
lines.UnlockRow(line); lines.LockRow(line); lines.LockAllRows(); lines.IsRowUnlocked(line);   // ShowLockButtons = false to do it only from code

// Pages loaded by the app (web API, database): set Source, not ItemsSource. The function gets the page, size, cursor and sort
table.Source = new PagedTableSource<GitHubIssueInfo>((request, ct) =>
    GitHubIssues.ListPageAsync("owner/repo", filter, request.Page, request.PageSize, read, cancellationToken: ct));
// canSort: true if the function sorts by request.SortKey (the column's SortMemberPath, else binding path, else Header); CanUserSort = false on columns it can't sort
// Cursor APIs: new PagedTableSource<GitHubProjectItem>((r, ct) => GitHubProjects.ListItemsPageAsync("org", 5, read, r.PageSize, r.Cursor, cancellationToken: ct), canJumpToPage: false)
// Your own: return new ItemPage<T> { Items = rows, HasMore = …, TotalCount = …, PageCount = … } (leave unknowns null)
source.Reset();     // back to page 1 (filter changed); source.Refresh() reloads the page. Any thread.
await table.GoToPageAsync(3); await table.RefreshAsync();   // never throw; failures go to table.Error and the Retry button
```

```xml
<niddy:TableGrid ItemsSource="{Binding Orders}" AutoGenerateColumns="True" PageSize="25" SelectedItem="{Binding Selected}" EmptyText="No orders." />
<!-- AutoGenerateColumns is off by default, as in DataGrid; generated headers split words ("CreatedAt" → "Created At") -->
<niddy:TableGrid ItemsSource="{Binding Lines}" IsReadOnly="False" LockRows="True">
    <niddy:TableGrid.Columns>
        <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}" />
    </niddy:TableGrid.Columns>
</niddy:TableGrid>
```

- Added properties: `Source`, `PageSize` (25), `PageSizes`, `ShowPager` (off = no paging for ItemsSource), `ShowPageSizes`, `EmptyText`, `LockRows`, `ShowLockButtons`; read-only `Page`, `PageCount`, `TotalCount`, `HasMore`, `IsLoading`, `Error`. Methods: `RefreshAsync`, `GoToPageAsync`, `RefreshRowClasses`, the lock methods.
- Setting `ItemsSource` clears `Source`; setting `Source` replaces `ItemsSource` with the page shown.
- The source's function runs **on the UI thread**: await slow work. Its `CancellationToken` is cancelled when the user moves on.
- The DataGrid theme is needed: `NiddyApp` adds it; otherwise add `avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml` to the app's styles.
- For a quick table in a dialog, `Dialog.Table` (with `Dialogs.TableColumn<T>`) is simpler; it isn't paged.

Details: [TableGrid](Niddy.Avalonia/Tables/TableGrid.md), [TableGridColumn](Niddy.Avalonia/Tables/TableGridColumn.md), [PagedTableSource](Niddy.Avalonia/Tables/PagedTableSource.md), [example](examples/github-table.md).

---

## UI state memory (`Niddy.Avalonia.State`)

- `NiddyApp` sets `UiStateStore.Default` and remembers the main window as `"Main"` automatically.
- **Other windows:** set `niddy:WindowMemory.Key="Settings"` on the `Window`, or call `WindowMemory.Attach(window, "Key")`.
- **Splitters:** set `niddy:LayoutMemory.Key="MainSplit"` on a `Grid` with `GridSplitter`s, or on a `ProportionalStackPanel`. Sizes are restored only if the column, row or pane count still matches.

Details: [UiStateStore](Niddy.Avalonia/State/UiStateStore.md), [WindowMemory](Niddy.Avalonia/State/WindowMemory.md), [LayoutMemory](Niddy.Avalonia/State/LayoutMemory.md).

---

## Files, links and attention

```csharp
using IStorageFile? file = await FilePicker.OpenFile(this, "Open", [FilePickerFileTypes.ImageAll]);   // works everywhere
await using Stream stream = await file!.OpenReadAsync();

string? folder = await FilePicker.OpenFolderPath(this);   // *Path variants: desktop only (null elsewhere)

await UriLauncher.TryLaunchAsync(this, "https://example.com");   // namespace Niddy.Avalonia; returns bool
WindowNotification.RequestAttention();                            // bounce the dock / flash the taskbar; desktop only
```

---

## Unhandled exceptions

`NiddyApp` installs `GlobalExceptionHandler`. It logs every unhandled exception to `options.LoggerFactory`. UI-thread exceptions show `Dialog.Exception`, and the app keeps running. To customize, use `options.OnUnhandledException = e => { e.Handled = true; }` or `e.ShowDialog = false`. For exceptions you catch yourself in fire-and-forget code, call `GlobalExceptionHandler.Report(ex)`. Niddy's own non-fatal failures (a URI that won't open, a failed UI-state save) are logged as warnings to the same factory, not thrown.

Details: [GlobalExceptionHandler](Niddy.Avalonia/Hosting/GlobalExceptionHandler.md).

---

## Niddy.Core

```csharp
// Paths: per-platform Data / Config / Cache / Logs folders
AppPaths paths = AppPaths.For("MyApp", organization: "Contoso").EnsureCreated();   // or AppPaths.Portable(dir)

// Settings: a typed JSON file with atomic saves and change events
public sealed class Settings { public int FontSize { get; set; } = 14; }
public sealed class SettingsStore(AppPaths p) : SettingsStore<Settings>(Path.Combine(p.Config, "settings.json"));
SettingsStore store = new(paths);
store.Load();                                 // defaults if missing or corrupt
store.Update(s => s.FontSize = 16);           // saves + raises Changed(e.Settings, e.Source)
store.Watch();                                // reload on external edits (Changed may fire on a background thread)
// Versioning: the store writes "$version" into the file and upgrades older files one step at a time, on the raw JSON
// (before deserializing), then backs up the old file and saves. A file without a version is version 0.
//   public override int Version => 2;
//   protected override void ConfigureMigrations(SettingsMigrations<Settings> migrations) => migrations   // one lambda per version
//       .Add(fromVersion: 0, json => json.RenameProperty("colour", "color"))
//       .Add(fromVersion: 1, json => json.Remove("legacy"))   // a step from >= Version throws on read: bump Version
//       .AfterUpgrade((s, fromVersion) => { … });   // optional typed fix-up after migrating (one per store)
// Steps run in order (file v1, store v5: the steps from 1, 2, 3, 4); a version without a step is skipped, unless
//   store.TreatMissingMigrationAsError = true, which makes it a failure (SettingsMigrationException.IsMissingStep).
// A step that throws (or is missing, as above): store.MigrationFailureAction = UseDefaults (default: CorruptFileDetected, *.bad, defaults)
//   | SkipStep (undo that step, carry on) | Throw (SettingsMigrationException out of Current/Load/Update; file untouched)
//   or override OnMigrationFailed(SettingsMigrationException e) to choose per step (e.FromVersion, e.ToVersion)
// store.FileVersion: the file's version before upgrading (higher than Version = written by a newer app; read as-is)

// Logging: rolling daily files, plus any other ILoggingBuilder providers
ILoggerFactory lf = NiddyLogging.CreateFactory(paths, LogLevel.Information, b => b.AddConsole());
services.AddLogging(b => b.AddFile(paths.Logs, o => o.RetainedFileCount = 14));
ILoggerFactory noFiles = NiddyLogging.CreateFactory(b => b.AddConsole());   // no file provider
// Your own provider (database, log server…): derive BatchingLoggerProvider<TOptions>, override WriteBatchAsync(batch, ct)
// (runs on one background task; LogEntry has Timestamp/Level/Category/EventId/Message/Exception/Scopes/Properties),
// then register it so the factory disposes it (= drains the queue):
// FlushInterval: a part-full batch waits up to this long for more entries (default zero = write at once; Dispose never waits).
// WriterThread: ThreadPool (default; no thread held while idle) | Dedicated (own thread; immune to pool starvation)
//   | Shared (one thread for all Shared providers; awaiting writers only). Browser/WASI fall back to ThreadPool. Nothing spins when idle.
// Providers are only ever added explicitly; nothing registers itself.
b.AddOwnedProvider(_ => new DbLoggerProvider(new DbLoggerOptions { ConnectionString = cs, MaxQueueLength = 10_000, FlushInterval = TimeSpan.FromSeconds(2) }));

// Secrets: Keychain (all Apple platforms) / Credential Manager / Secret Service / encrypted-file fallback. Throws in the browser
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, "MyApp");
secrets.SetToken("api", token); secrets.GetToken("api"); secrets.TokenExists("api"); secrets.SetToken("api", null);
// Plug in your own (before the first Create). Newest first; return null to pass to the next / the built-in default
IDisposable reg = SecureStorageFactory.Register(ctx => ctx.OS == OS.Android ? new MyKeystore(ctx.ServiceName) : null);   // ctx.OS = Platform.Info.OS
SecureStorageFactory.Register(ctx => new LoggingStorage(SecureStorageFactory.CreateDefault(ctx)));   // wrap the default

// Crash-safe writes
AtomicFile.WriteAllText(path, json);           // also WriteAllBytes, Write(path, stream => …), async versions

// Results instead of exceptions for expected failures
Result<User> Find(int id) => users.TryGetValue(id, out User? u) ? u : new Error("Not found", "user.missing");
Result<string> r = Result.Try(() => File.ReadAllText(path));        // exceptions become Error(Message, Code?, Exception?)
string text = r.Match(ok => ok, err => "");
Option<User> first = users.Values.FirstOrNone(u => u.IsAdmin);

// Threading
Debouncer debounce = new(TimeSpan.FromMilliseconds(300));
debounce.Invoke(async ct => await SearchAsync(query, ct));   // earlier calls are cancelled
Throttler throttle = new(TimeSpan.FromSeconds(1));
throttle.Invoke(() => Redraw());
Guard once = new(); if (once.TryClaim) Init();          // true only for the first caller

// Retry (Niddy.Core.Resilience)
await Retry.ExecuteAsync(() => CallApiAsync(ct), new RetryOptions   // null options: 3 attempts, 100 ms doubling to 3 s
{
    MaxAttempts = 5,                                  // total attempts, including the first
    BaseDelay = TimeSpan.FromMilliseconds(200),
    MaxJitter = TimeSpan.FromMilliseconds(100),       // random extra wait, 0–100 ms
    ShouldRetry = ex => ex is HttpRequestException,   // default: retries any exception; rejected ones pass through unwrapped
    OnRetry = a => { log(a.Exception, a.Number, a.Delay); return true; },   // false stops: RetryBlockedException
}, ct);                                               // all attempts failed: MaxRetriesException (InnerException = last)
// C# 14 static extensions on File/Directory/Path (Niddy.Core.IO): call them on the BCL type
Directory.DeleteWithRetry(dir);       // retries locked/read-only files; throwOnFailure: false returns false instead
Directory.CreateEmpty(outDir);        // exists and is empty afterwards
Directory.Copy(src, dest, excludedDirectories: ["bin", "obj"]);
string safe = Path.SanitizeFileName(title);
// Backups (Niddy.Core.IO.Backups)
File.Backup(path, keep: 5);  // report.backup-20260928T140312Z.txt next to it; Directory.Backup(dir, archive: ArchiveFormat.Zip) too
File.Backup(path, extension: ".bak");  // report.txt.backup-20260928T140312Z.bak
// Archives (Niddy.Core.IO.Archives)
Archive.Create(dir, "out.zip", new() { ExcludedDirectories = ["bin", "obj"] });  // zip/tar/tar.gz from the extension; path, stream and async forms
Archive.Extract(upload, dest, new() { MaxEntries = 10_000, MaxTotalBytes = 500L << 20 });  // rejects ../ and escaping links; set limits for untrusted archives
string cfg = Archive.ReadEntryText("plugin.zip", "config.json");  // also OpenEntry (stream), TryOpenEntry, ReadEntryBytes, ExtractEntry, ContainsEntry, GetEntries
// JSON (Niddy.Core.Serialization)
Project? p = JsonFile.Read<Project>(path);   // default if missing/empty/invalid; Write/WriteAsync are atomic
Project? q = JsonSerializer.DeserializeOrDefault<Project>(text);   // default instead of throwing
string json = JsonSerializer.Serialize(p, JsonSerializerOptions.Niddy);   // shared, read-only; copy it to change
// Text (Niddy.Core.Text), streams (Niddy.Core.IO), root (Niddy.Core)
Humanize.Bytes(1_500_000);            // "1.5 MB"
Humanize.RelativeTime(when);          // "3 minutes ago"
Humanize.Plural(3, "file");           // "3 files"
using DebouncedFileWatcher watcher = DebouncedFileWatcher.ForFile(path);  // one batched Changed per burst
string.Equals(a, b, Path.Comparison);   // compare paths: ignores case on Windows/macOS/Catalyst only
if (name.IsNullOrWhiteSpace()) return;   // [NotNullWhen(false)]: name is non-null after this
string all = await stream.ReadToStringAsync();
value.DisposeIfPossible();            // any T; disposes if IDisposable

// Web (namespace Niddy.Core.Web)
UrlLauncher.Open("https://example.com");   // opens the browser; no UI framework needed; not on Android
Result<FileInfo> saved = await WebContent.DownloadFileAsync(url, path, new DownloadOptions
{
    Overwrite = true,                     // false (default) + existing file → FileExists, nothing downloaded
    Progress = new Progress<DownloadProgress>(p => bar.Value = p.Fraction ?? 0),
    MaxBytes = 500_000_000,               // bigger (Content-Length or as it arrives) → TooLarge, nothing saved
    Sha256 = expectedHex,                 // mismatch → ChecksumMismatch, nothing saved
    Resume = true,                        // a failed/canceled download keeps its partial data; the next call asks for the rest (Range + If-Range)
});
// atomic (no partial file on failure), creates folders
// HttpClient.Timeout only covers the response starting: pass a CancellationToken for stalled big downloads
Result<FileInfo> intoFolder = await WebContent.DownloadToFolderAsync(url, folder);   // name from Content-Disposition, else the URL; can't escape folder
Result<string> page = await WebContent.ReadTextAsync(url, maxBytes: 1_000_000);   // charset-aware (BOM, then charset, then UTF-8); whole body in memory
Result<byte[]> bytes = await WebContent.ReadBytesAsync(url);
Result<MyDto> dto = await WebContent.ReadJsonAsync<MyDto>(url);   // or (url, MyJsonContext.Default.MyDto) for trimming/AOT; bad JSON → InvalidContent
await foreach (string line in WebContent.ReadLinesAsync(url, cancellationToken: ct)) { … }   // streams; break closes the connection
// Fail (no throw) with WebContent.ErrorCodes: NotFound, HttpError (StatusCode on the HttpRequestException), Network, TooLarge,
//   ChecksumMismatch, InvalidContent, FileExists, FileSystem
// ReadLinesAsync throws instead: HttpRequestException before the first line, IOException partway. Pass an HttpClient for headers/BaseAddress (relative URLs)
// Sync versions of everything (DownloadFile, DownloadToFolder, ReadText, ReadBytes, ReadJson, ReadLines): real sync sends, no deadlock,
//   block the thread. Not in the browser. A client you pass needs a handler with sync sends (SocketsHttpHandler; not the native Android/iOS ones)

// GitHub issues (Niddy.Core.GitHub): standalone, call from anywhere (crash handler, feedback button, CLI)
GitHubIssue issue = GitHubIssue.Bug("Crash when saving", details) with   // or FeatureRequest / Question / Documentation, or new() { Title = …, Labels = […] }
{
    Sections = [GitHubIssueSection.FromLatestLogFile(paths.Logs, lines: 200)],   // collapsed <details>; also Code(title, text), Text(title, md); never throws for missing logs
    Reporter = new GitHubIssueReporter(Name: "Jane", Email: "jane@example.com", GitHubUsername: "jane"),   // all optional
    Details = new Dictionary<string, string> { ["Document"] = "a.txt" },   // table under the body
    IncludeSystemInfo = true,                 // app version, OS, architecture, runtime; nothing identifying
};
GitHubIssue crash = GitHubIssue.FromException(ex, body: "What I was doing");   // title from the real cause, full ToString() in an "Exception" section, bug label, system info, Fingerprint
Result<GitHubIssueReference> created = await GitHubIssues.CreateAsync("owner/repo", token, issue);   // .Number, .Url
// Fingerprint (any text; FromException sets type + throwing method; null = always new): CreateAsync searches open issues for its hidden
//   marker and, if found, comments there instead → created.Value.AddedAsComment / .CommentUrl. Search or comment failing → new issue.
//   Search lags ~1 min, so near-simultaneous reports can still duplicate. Closed issues don't match.
// Too long for GitHub (65,536 chars) or a URL (~8,000): sections are cut first (shared evenly, "… (cut)"), then Body; reporter/details kept
// Fails (no throw) with Error.Code in GitHubIssues.ErrorCodes: Unauthorized, Forbidden, RateLimited, NotFound, IssuesDisabled, Invalid, Failed, Network
// Token: fine-grained PAT, one repo, Issues read/write only (a shipped token can be extracted). Labels/assignees/milestone are
//   silently dropped unless the token has push access. Pass an HttpClient with BaseAddress for GitHub Enterprise.
GitHubIssues.OpenInBrowser("owner/repo", issue);   // no token: prefilled new-issue form; GetNewIssueUrl for Avalonia's UriLauncher
// API, else the form (no token, or Unauthorized/Forbidden/NotFound/RateLimited/Invalid); other failures returned. Avalonia: pass openUrl
Result<GitHubIssueSubmission> sent = await GitHubIssues.CreateOrOpenAsync("owner/repo", tokenOrNull, issue, openUrl: url => _ = UriLauncher.TryLaunchAsync(control, url));
// sent.Value.OpenedInBrowser / .Issue / .ApiError; can't open the browser: ErrorCodes.BrowserUnavailable
// Sync: GitHubIssues.Create(…) / CreateOrOpen(…), same parameters and limits as WebContent's sync versions
// Set up once instead (keep it for the app's lifetime): same methods minus repository/token/httpClient/openUrl/baseAddress
GitHubIssueClient issues = new("owner/repo", tokenOrNull, httpClient) { Reporter = reporter, OpenUrl = url => _ = UriLauncher.TryLaunchAsync(control, url) };
await issues.CreateOrOpenAsync(crash);   // also CreateAsync (throws InvalidOperationException without a token), Create, CreateOrOpen, GetNewIssueUrl, OpenInBrowser
// Reporter fills in issues whose Reporter is null; HasToken says whether CreateAsync can be used

// Reading GitHub (Niddy.Core.GitHub), read-only: issues and PRs via REST, projects (v2 only) via GraphQL
GitHubReadOptions read = new() { Token = tokenOrNull, CacheDirectory = Path.Combine(paths.Cache, "github") };   // CacheDuration defaults to Zero: always asks, 304s are free, offline → cached copy
Result<IReadOnlyList<GitHubIssueInfo>> open = await GitHubIssues.ListAsync("owner/repo",
    new GitHubIssueFilter { Labels = ["bug"], Sort = GitHubIssueSort.Updated }, maxCount: 50, options: read);   // PRs skipped unless IncludePullRequests
// GitHubIssueInfo (not GitHubIssue, which is for opening one): Number, Title, Body, State (GitHubItemState), IsPullRequest, Labels, CommentCount, …
Result<GitHubIssueInfo> one = await GitHubIssues.GetAsync("owner/repo", 42, read);
Result<IReadOnlyList<GitHubIssueInfo>> hits = await GitHubIssues.SearchAsync("repo:owner/repo is:pr is:open author:me", options: read);   // max 1,000; 30 searches/min
Result<IReadOnlyList<GitHubComment>> comments = await GitHubIssues.ListCommentsAsync("owner/repo", 42, options: read);   // PR conversation too
Result<IReadOnlyList<GitHubPullRequest>> pulls = await GitHubPullRequests.ListAsync("owner/repo",
    new GitHubPullRequestFilter { Base = "main", Head = "owner:branch" }, options: read);   // Head needs owner:branch
// GitHubPullRequest: HeadBranch, BaseBranch, IsDraft, State (Merged too), RequestedReviewers; counts/IsMergeable only from GetAsync (null in lists)
// Projects: options with a Token is required (ArgumentNullException/ArgumentException otherwise); read:project scope or Projects: Read
Result<IReadOnlyList<GitHubProject>> projects = await GitHubProjects.ListAsync("org", read);   // or ListForRepositoryAsync("owner/repo", read); GetAsync("org", number, read)
Result<IReadOnlyList<GitHubProjectItem>> items = await GitHubProjects.ListItemsAsync("org", 5, read, maxCount: 500);
// item.Type (Issue/PullRequest/DraftIssue/Redacted), .Status, .GetField("Priority") (case-insensitive, numbers invariant), .IsArchived (archived items may be included)
Result<IReadOnlyList<GitHubProjectField>> fields = await GitHubProjects.ListFieldsAsync("org", 5, read);   // .Options: single-select options / iteration titles
// ErrorCodes: GitHubPullRequests/GitHubProjects.ErrorCodes have GitHubIssues.ErrorCodes' values. Every read has a sync twin (List, Get, Search, ListItems, …)
// Paging yourself (e.g. a TableGrid): one page per call, as an ItemPage<T> (Niddy.Core.Collections): Items, HasMore, NextCursor, TotalCount?, PageCount?
Result<ItemPage<GitHubIssueInfo>> page2 = await GitHubIssues.ListPageAsync("owner/repo", filter: null, page: 2, pageSize: 50, options: read);
// PageCount from the Link header, TotalCount null; PRs are filtered out after reading, so a page can be short (or empty) with HasMore true
// Also SearchPageAsync(query, page, pageSize, …) (TotalCount = GitHub's; pages must start within the first 1,000), ListCommentsPageAsync(repo, number, page, pageSize, …),
// GitHubPullRequests.ListPageAsync(repo, filter, page, pageSize, …). page ≥ 1, pageSize 1–100, else ArgumentOutOfRangeException
Result<ItemPage<GitHubProjectItem>> first = await GitHubProjects.ListItemsPageAsync("org", 5, read, pageSize: 50);   // cursor-based: no page numbers
Result<ItemPage<GitHubProjectItem>> next = await GitHubProjects.ListItemsPageAsync("org", 5, read, 50, first.Value!.NextCursor);   // NextCursor null on the last page
// Also ListPageAsync(owner, read, pageSize, cursor) and ListForRepositoryPageAsync(repo, read, pageSize, cursor); TotalCount and PageCount are given. Sync twins: ListPage, SearchPage, ListItemsPage, …

// GitHub releases (Niddy.Core.GitHub): update checks and downloads
GitHubReleaseOptions releaseOptions = new()
{
    CacheDirectory = Path.Combine(paths.Cache, "releases"),   // fresh → no request; stale → ETag (304 is free); offline/rate-limited/5xx → stale copy
    // Token = …, IncludePrereleases = true, CacheDuration = TimeSpan.FromHours(6)
};
Result<GitHubUpdateCheck> check = await GitHubReleases.CheckForUpdateAsync("owner/app", currentVersion, releaseOptions);
// currentVersion: a string ("v1.2.0", AssemblyInformationalVersion with +build is fine) or a Version. Semver: 1.2.0-beta.10 > -beta.2, release > prerelease
// No digits in the string → ArgumentException; a latest tag with no version → InvalidVersion failure
if (check.TryGetValue(out GitHubUpdateCheck update) && update.IsUpdateAvailable)
{
    GitHubRelease latest = update.Latest;   // .TagName, .Name, .Notes, .Url, .PublishedAt, .Version, .Assets
    GitHubReleaseAsset? asset = latest.FindAssetForPlatform();   // this OS/arch from names like app-linux-x64.tar.gz; filter: a => a.Name.EndsWith(".zip"); or GetAsset(name)
    if (asset is not null)
    {
        Result<FileInfo> file = await GitHubReleases.DownloadAssetAsync(latest, asset, path, new DownloadOptions { Overwrite = true });
        // SHA-256 checked against: DownloadOptions.Sha256, else GitHub's digest, else {asset}.sha256(sum), else SHA256SUMS/*checksums.txt. Failures use WebContent.ErrorCodes
    }
}
// Also GetLatestAsync, GetByTagAsync, ListAsync(maxCount) (drafts need a token with push access; prereleases skipped by GetLatest/CheckForUpdate unless IncludePrereleases)
// Lookups fail with GitHubReleases.ErrorCodes: NotFound, Unauthorized, Forbidden, RateLimited, Failed, Network, InvalidVersion
// Without a token: 60 API requests/hour per IP, shared. Set CacheDirectory. Every method has a sync version (GetLatest, CheckForUpdate, DownloadAsset, …)
string direct = GitHubReleases.GetAssetUrl("owner/app", "app-linux-x64.tar.gz");   // …/releases/latest/download/…: no API call, public repos, pass to WebContent

// Machine info (namespace Niddy.Core.SystemInfo): detected once and cached
Platform info = Platform.Info;
info.OS; info.OSVersion;              // OS.MacOS, 15.0. OS.Browser / OS.Wasi on WebAssembly, OS.MacCatalyst for Catalyst. On Linux/Android OSVersion is the kernel version
info.Triple;                          // Rust/LLVM style: "aarch64-apple-darwin", "x86_64-unknown-linux-musl", "armv7-unknown-linux-gnueabihf"; also TotalMemoryBytes, TotalGpus (estimate), Runtime
Platform.DeclareSupportedOperatingSystems(OS.Windows, OS.MacOS);   // then Info throws UnsupportedOperatingSystemException elsewhere
```

Details: [Niddy.Core pages](README.md#niddycore), [examples/settings-logging-paths](examples/settings-logging-paths.md).

---

## Gotchas

| Mistake | Correct |
|---|---|
| `Page<App, HomePage>` | `Page<MainDataContext, HomePageDataContext>`: app data context, then page data context |
| Relying on `Children = [...]` or file order for menu order | Menus are alphabetical. Use `SortKey`, `options.PageSort`, or `PageRegistry.DeclaredOrder` |
| `SortKey = "~"` to sort last | Culture comparison doesn't put punctuation after letters; use `"zzz"` |
| Rebuilding `DialogOverlayHost` → `ToastHost` → `PageView` by hand in tests | `new AppShell(options)` |
| `[PageRegistration]` without the generator package | Add `Niddy.Avalonia.Generators`, or call `PageRegistry.Register<T>()` |
| Using `NiddyApp.Paths` / `Current.Paths` inside `Configure` | Build `AppPaths.For(...)` yourself there |
| `Dialog.Progress` reporting 0–1 | 0–100 (while `ToastHandle.Report` is 0–1) |
| Touching controls inside `Dialog.Progress` work | It runs on a background thread; only report progress |
| `FilePicker.OpenFilePath` in cross-platform code | `FilePicker.OpenFile` + `OpenReadAsync` |
| `Directory.Delete(dir, true)` on folders that may be locked or read-only (git clones, just-closed files) | `Directory.DeleteWithRetry(dir)` / `File.DeleteWithRetry(path)` |
| `catch (HttpRequestException)` around `Retry.Execute`/`ExecuteAsync` for the final failure | `catch (MaxRetriesException ex)` (or `RetryException` for blocked retries too); the failure is `ex.InnerException`. Same for `File.DeleteWithRetry`/`Directory.DeleteWithRetry` |
| `ZipFile.ExtractToDirectory` on an upload or download | `Archive.Extract` with `MaxEntries`/`MaxTotalBytes` (or `maxBytes` on single-entry methods), so a zip bomb can't fill the disk |
| Reading many single files from a big `.tar.gz` with `OpenEntry` | Each call rescans from the start; use zip for random access, or `Extract` with a `Filter` |
| `Archive.Create(source, stream)` without a format | `new ArchiveCreateOptions { Format = ArchiveFormat.Zip }`; a stream has no extension to go by |
| `Process.Start(url)` | `UriLauncher.TryLaunchAsync(this, url)` in Avalonia; `UrlLauncher.Open(url)` (`Niddy.Core.Web`) without a UI (not on Android) |
| `using Niddy.Core;` for `UrlLauncher` | It's in `Niddy.Core.Web` |
| `File.WriteAllBytes(path, await http.GetByteArrayAsync(url))` | `WebContent.DownloadFileAsync(url, path)`: streams to disk, atomic, progress, `Result` failures |
| `WebContent.DownloadFileAsync(url, path, overwrite: true, progress: …)` | `new DownloadOptions { Overwrite = true, Progress = … }` as the third argument |
| `Version.Parse(release.TagName.TrimStart('v'))` and `>` for update checks | `GitHubReleases.CheckForUpdateAsync`: semver order, prereleases, tag prefixes, `+build` metadata |
| Calling the GitHub releases API on every app start without a token | Set `GitHubReleaseOptions.CacheDirectory`; 60 requests/hour per IP are shared by everyone on it |
| `GitHubIssue` for issues read from GitHub | `GitHubIssueInfo`; `GitHubIssue` is what `CreateAsync` opens |
| `GitHubProjects.ListAsync("org", new GitHubReadOptions())` | Projects need a token (GraphQL); it throws `ArgumentException` without one |
| `GitHubPullRequestFilter { Head = "my-branch" }` | `Head = "owner:my-branch"`; a bare branch matches nothing |
| `WebContent.ReadText` / `DownloadFile` (sync) in browser (WASM) code | The async versions; the sync ones are `[UnsupportedOSPlatform("browser")]` |
| `return ok ? text : new Error("…");` in a method returning `Result<string>` | The `?:` is typed `Error` (string converts to `Error` implicitly), so the text becomes a failure. Use `if`/`return`, or `Result<string>.Success(text)` |
| `new TableView()` / `TableViewColumn` for a Niddy table | `TableGrid` / `TableGridColumn<T>`; `TableView` is Avalonia 12.1's own control |
| A `TableGrid` with no rows in an app not built on `NiddyApp` | Add the DataGrid theme: `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>` |
| Blocking (`.Result`, synchronous I/O) in a `PagedTableSource` function | `await`; it runs on the UI thread |
| `PagedTableSource` for a GraphQL/cursor API with page numbers | `canJumpToPage: false` and pass `request.Cursor`; the table keeps the cursors |
| Columns not sortable with a `PagedTableSource` | `canSort: true` if the function sorts by `request.SortKey`; `CanUserSort = false` on the columns it can't sort |
| A `TableGrid` that won't edit | `IsReadOnly` defaults to true: set it false. With `LockRows`, unlock the row first. A `TableGridColumn<T>` also needs `Edit` |
| A `TableGrid` with `ItemsSource` and no columns showing nothing | `AutoGenerateColumns="True"`, or add `Columns`; it's off by default, as in DataGrid |
| `GitHubIssues.ListAsync(…, maxCount: 1000)` to fill a paged UI | `ListPageAsync(…, page, pageSize)` per page shown |
| Tokens in `SettingsStore` | `ISecureStorage` |
| A `Version` property on the settings class, checked in `OnLoaded` | Override the store's `Version` and add a step per version in `ConfigureMigrations`; the store writes the version and runs each step on the raw JSON, so renamed or retyped properties aren't lost |
| A new `SettingsStore.Version` for every added property | Only bump it when old JSON needs changing; new properties just get their defaults |
| `SecureStorageFactory.Create()` | `SecureStorageFactory.Create(paths.Data, "MyApp")`; `baseDirectory` is required |
| Trusting the default secure storage on Android, FreeBSD or WASI | It's `EncryptedFileSecureStorage` there, with a guessable key; register a Keystore (or similar) provider for real secrets |
| Calling `SecureStorageFactory.Create` in a browser app with no provider | It throws `PlatformNotSupportedException`; keep secrets server-side, or register a provider deliberately |
| Running unsigned on iOS/iPadOS/Catalyst and expecting the Keychain to work | Keychain calls throw `InvalidOperationException` (OSStatus -34018) without a provisioning profile; macOS needs none |
| Calling `SecureStorageFactory.Create` inside a registered provider | `SecureStorageFactory.CreateDefault(context)`; `Create` would run the provider again, forever |
| Throwing from a provider when it doesn't apply | Return null so the next provider (or the default) is tried; exceptions aren't caught |
| `b.AddProvider(new MyBatchingProvider(...))` | `b.AddOwnedProvider(_ => new MyBatchingProvider(...))`; `AddProvider` instances are never disposed, so queued entries are lost at exit |
| `.Result` / `.Wait()` inside a batching provider's hooks with `WriterThread = Dedicated` or `Shared` | `await` it; blocking can deadlock the writer thread, like a UI thread |
| A blocking (synchronous I/O) writer on `LogWriterThread.Shared` | `Dedicated`; shared-thread writers take turns, so a blocking one holds up every other provider |
| Logging from inside `WriteBatchAsync` / `OnWriteFailed` | Don't; it feeds back into the provider's own queue. Count or report failures another way |
| `using Niddy.Core.Helpers;` | There's no Helpers namespace; see the namespace list at the top (e.g. `Retry` is in `Niddy.Core.Resilience`, `Humanize` in `Niddy.Core.Text`) |
| `IOHelpers.DeleteDirectory(dir)` / `PathHelpers.PathComparison` | `Directory.DeleteWithRetry(dir)` / `Path.Comparison`, C# 14 static extensions (`using Niddy.Core.IO;`) |
| Mutating `JsonSerializerOptions.Niddy` | It's read-only and shared; copy it: `new JsonSerializerOptions(JsonSerializerOptions.Niddy) { … }` |
| Reusing one custom dialog instance | New instance per `Dialog.Show` |
| `window.ShowDialog(owner)` for a form, or a `DialogBase` class just to wrap an existing view | `Dialog.View.Open(this, viewOrWindow, "Title")`; works as an overlay on mobile and browser too |
| Passing `Dialog.View.Open` a control that's already on screen, or an open window | A new or detached control, or a window that hasn't been shown |
| `windowHeight: 800` to show more of a `Dialog.Markdown`/`Table` | `contentHeight: 600`; `windowHeight` sizes the window, not the capped scrolling area inside it |
| Handling `SettingsStore.Changed` / `PageRegistry.PagesChanged` and touching UI | `Dispatcher.UIThread.Post(...)` |
| `Platform.Info.OSVersion` as the Linux distro or Android release | It's the kernel version on Linux and Android |
| Expecting `Triple` to always end in `linux-gnu` on Linux | It's `linux-musl` on Alpine and other musl distros, with `eabihf` added on 32-bit ARM. 32-bit `Arm` is `armv7`, `Armv6` is `arm` |
| Checking `OS.iOS` to mean "any Apple mobile" | iPads report `OS.iPadOS` and Catalyst apps `OS.MacCatalyst`; check each one you mean |
| `DeclareSupportedOperatingSystems` and expecting only the first `Info` to throw | Every `Platform.Info` access throws on an undeclared OS; catch `UnsupportedOperatingSystemException` once at startup and exit |

## Changing Niddy itself

If you're editing this library rather than using it, follow the [Development section of the root README](../README.md#development). In particular: **every new public type (class, record, struct, interface, enum, delegate, attribute, anything) is documented in the same change.** It gets a `docs/<Project>/<Folder>/<Type>.md` page (small supporting types go on their owning class's page), an entry in the [docs index](README.md), and an update to this guide if it changes how the library is used. New public members go on their type's existing page. Undocumented public types mean the change isn't finished.

## Where to look next

- [docs/README.md](README.md): every page.
- [docs/examples/](examples): app-setup, settings-logging-paths, long-running-work, custom-dialog, mobile-and-browser, plugin-pages, oauth-sign-in.
