# Niddy: a guide for AI assistants

This one file tells you how to use the whole library in an app. It's written for AI coding assistants, but people can read it too. Every API here matches the source. For the complete API of a class, open its page from the [docs index](README.md): `docs/<Package>/<Folder>/<Class>.md`.

**Stack:** .NET 10, C# 14, Avalonia 12. Packages:

| Package | Use it for | Namespaces |
|---|---|---|
| `Niddy.Core` | Anything (no UI): results, settings, logging, paths, secure storage, threading, helpers | `Niddy.Helpers`, `Niddy.IO`, `Niddy.Logging`, `Niddy.Results`, `Niddy.Security`, `Niddy.Settings`, `Niddy.Threading` |
| `Niddy.Avalonia` | Avalonia apps: app hosting, pages, dialogs, toasts, state memory, converters | `Niddy.Avalonia.<Folder>`: `Hosting`, `PageSystem`, `Dialogs`, `Toast`, `State`, `Converters`, `DataContexts`, `FilePicker`, `Layout`, `Theme`, `Utilities`; `ScreenPlacement` is in `Niddy.Avalonia` |
| `Niddy.Avalonia.Generators` | Compile-time registration of `[PageRegistration]` pages. **Without it, `[PageRegistration]` does nothing.** | generates `Niddy.Generated.Pages_<Assembly>` |

XAML: `xmlns:niddy="https://github.com/vonderborch/Niddy"` covers every `Niddy.Avalonia` folder **except `Utilities`**. For the `Page` root element, examples use `xmlns:pageSystem="clr-namespace:Niddy.Avalonia.PageSystem;assembly=Niddy.Avalonia"`.

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
using Niddy.IO;
using Niddy.Logging;

public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        var paths = AppPaths.For("MyApp").EnsureCreated();     // NiddyApp.Paths isn't usable inside Configure
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

Other `NiddyAppOptions` settings: `Organization`, `Mode` (`AppMode.Auto|Desktop|Mobile`), `PageTransition`, `HandleBackRequests`, `UseFluentTheme`, `Theme` (`AppTheme?`), `DialogMode`, `ToastPlacement`, `ToastNarrowPlacement`, `ToastDuration`, `Width/Height/MinWidth/MinHeight`, `MobilePreviewWidth/Height`, `RememberWindowState`, `ConfigureWindow`, `HandleUnhandledExceptions`, `ShowUnhandledExceptionDialog` and `OnUnhandledException`. `options.Mode = AppMode.Mobile` on desktop previews the phone layout in a 390×844 window.

At runtime: `NiddyApp.Current!.Shell.Pages` (the root `PageView`), `.Shell.Toasts`, `.Shell.Dialogs`, `NiddyApp.Current.Paths`, `NiddyApp.CurrentMode`.

Details: [NiddyApp](Niddy.Avalonia/Hosting/NiddyApp.md), [NiddyAppOptions](Niddy.Avalonia/Hosting/NiddyAppOptions.md), [examples/app-setup](examples/app-setup.md).

---

## Pages

```csharp
using Niddy.Avalonia.PageSystem;

[PageRegistration(DisplayName = "Home", Shortcut = "Primary+1")]           // Primary = Cmd on macOS, Ctrl elsewhere
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
- **Navigation, from inside a page:** `NavigateTo<T>(addToBackStack = true, parameter = null)`, `NavigateTo(PageId, …)`, `NavigateToChild<T>()`, `NavigateToSibling<T>()`, `NavigateToParent()`, `Push(Page)`, `GoBack()`, `CanGoBack`. Each returns `bool`.
- **Navigation, from outside a page:** use `NiddyApp.Current!.Shell.Pages` (a `PageView`) with the same methods.
- **Guards:** override `OnNavigatingFrom(PageNavigatingFromEventArgs e)`, set `e.Cancel = true`, ask the user, then call `e.Continue()`.
- **Clean-up:** override `OnClosed()` (never called for kept-alive pages).
- **Nested pages:** put a `<pageSystem:PageView StartingPage="{x:Type local:ChildPage}" />` inside a page. It shows that page's children.
- **Menus:** `PageMenu` lists registered pages (`Mode`: `Flat` for one level, or `Tree`). It turns into a bottom navigation bar on mobile or narrow windows.
- **Manual registration** (no generator, or for DI factories): `PageRegistry.Register<TPage>(displayName, keepAlive, parents, children, topLevel, icon, shortcut)` or `Register(() => new TPage(deps), …)`.
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
| `Table.Open(p, title, description, items, columns = null)` / `Table.Pick(…)` | `Task` / `Task<T?>`; columns are `new TableColumn<T>("Header", x => x.Value) { Format = … }` |
| `Web.Open(p, title, uri, closeWhen: u => …, openInBrowserText = "Open in Browser")` | `Task<Uri?>`: the matched address, or the last address shown. Best with `displayMode: DialogDisplayMode.Window`. |
| `Web.OpenHtml(p, title, html, baseAddress = null, closeWhen = null)` | `Task<Uri?>` |
| `Show(p, new MyDialog(…), maxWidth = 500)` | `Task<TResult>` for a custom dialog |

All calls also accept `maxWidth`, `displayMode`, `windowWidth` and `windowHeight`.

**Custom dialog:** create an AXAML file with `<dialogs:DialogLayout … Title="…" Description="…">` as the root. Put the body as its content and buttons in `<dialogs:DialogLayout.Buttons>`. In code-behind, derive from `DialogBase<TResult>`, implement `protected override TResult DismissResult`, and call `Close(result)` from buttons. You can also override `InitialFocus`, `CanDismiss` and `OnDismissed()`. Use a new instance per `Dialog.Show`.

Details: [Dialog](Niddy.Avalonia/Dialogs/Dialog.md), [DialogBase](Niddy.Avalonia/Dialogs/DialogBase.md), [DialogLayout](Niddy.Avalonia/Dialogs/DialogLayout.md), [examples/custom-dialog](examples/custom-dialog.md).

---

## Toasts (`Niddy.Avalonia.Toast`)

```csharp
Toast.Show(this, "Saved", ToastType.Success);                                // Info | Success | Warning | Error
Toast.Show(this, "File deleted", actionText: "Undo", action: () => Restore(file));
Toast.Show(this, "Offline", ToastType.Error, duration: TimeSpan.Zero);       // stays until dismissed
Toast.Show(this, $"Synced {n}", key: "sync");                                 // same key updates in place

var t = Toast.ShowProgress(this, "Uploading…", isIndeterminate: false, cancelText: "Cancel", cancel: cts.Cancel);
t.Report(0.5);                         // progress 0–1 (not 0–100)
t.Update("Almost done…");
t.Complete("Uploaded");                // or t.Complete("Failed", ToastType.Error); or t.Dismiss();
```

Call `Show`/`ShowProgress` on the UI thread. `ToastHandle` methods are safe from any thread. Identical toasts merge with a "×N" count. Defaults: 3 s, or 6 s with an action.

Details: [Toast](Niddy.Avalonia/Toast/Toast.md), [ToastHandle](Niddy.Avalonia/Toast/ToastHandle.md), [ToastHost](Niddy.Avalonia/Toast/ToastHost.md).

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

## UI state memory (`Niddy.Avalonia.State`)

- `NiddyApp` sets `UiStateStore.Default` and remembers the main window as `"Main"` automatically.
- **Other windows:** set `niddy:WindowMemory.Key="Settings"` on the `Window`, or call `WindowMemory.Attach(window, "Key")`.
- **Splitters:** set `niddy:LayoutMemory.Key="MainSplit"` on a `Grid` with `GridSplitter`s, or on a `ProportionalStackPanel`. Sizes are restored only if the column, row or pane count still matches.

Details: [UiStateStore](Niddy.Avalonia/State/UiStateStore.md), [WindowMemory](Niddy.Avalonia/State/WindowMemory.md), [LayoutMemory](Niddy.Avalonia/State/LayoutMemory.md).

---

## Files, links and attention

```csharp
using var file = await FilePicker.OpenFile(this, "Open", [FilePickerFileTypes.ImageAll]);   // works everywhere
await using var stream = await file!.OpenReadAsync();

string? folder = await FilePicker.OpenFolderPath(this);   // *Path variants: desktop only (null elsewhere)

await UriLauncher.TryLaunchAsync(this, "https://example.com");   // Niddy.Avalonia.Utilities; returns bool
WindowNotification.RequestAttention();                            // bounce the dock / flash the taskbar; desktop only
```

---

## Unhandled exceptions

`NiddyApp` installs `GlobalExceptionHandler`. It logs every unhandled exception to `options.LoggerFactory`. UI-thread exceptions show `Dialog.Exception`, and the app keeps running. To customize, use `options.OnUnhandledException = e => { e.Handled = true; }` or `e.ShowDialog = false`. For exceptions you catch yourself in fire-and-forget code, call `GlobalExceptionHandler.Report(ex)`.

Details: [GlobalExceptionHandler](Niddy.Avalonia/Hosting/GlobalExceptionHandler.md).

---

## Niddy.Core

```csharp
// Paths: per-platform Data / Config / Cache / Logs folders
var paths = AppPaths.For("MyApp", organization: "Contoso").EnsureCreated();   // or AppPaths.Portable(dir)

// Settings: a typed JSON file with atomic saves and change events
public sealed class Settings { public int FontSize { get; set; } = 14; }
public sealed class SettingsStore(AppPaths p) : SettingsStore<Settings>(Path.Combine(p.Config, "settings.json"));
var store = new SettingsStore(paths);
store.Load();                                 // defaults if missing or corrupt
store.Update(s => s.FontSize = 16);           // saves + raises Changed(e.Settings, e.Source)
store.Watch();                                // reload on external edits (Changed may fire on a background thread)

// Logging: rolling daily files
ILoggerFactory lf = NiddyLogging.CreateFactory(paths, LogLevel.Information);
services.AddLogging(b => b.AddFile(paths.Logs, o => o.RetainedFileCount = 14));

// Secrets: Keychain / Credential Manager / Secret Service / encrypted-file fallback
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, "MyApp");
secrets.SetToken("api", token); secrets.GetToken("api"); secrets.TokenExists("api"); secrets.SetToken("api", null);

// Crash-safe writes
AtomicFile.WriteAllText(path, json);           // also WriteAllBytes, Write(path, stream => …), async versions

// Results instead of exceptions for expected failures
Result<User> Find(int id) => users.TryGetValue(id, out var u) ? u : new Error("Not found", "user.missing");
var r = Result.Try(() => File.ReadAllText(path));        // exceptions become Error(Message, Code?, Exception?)
string text = r.Match(ok => ok, err => "");
Option<User> first = users.Values.FirstOrNone(u => u.IsAdmin);

// Threading
var debounce = new Debouncer(TimeSpan.FromMilliseconds(300));
debounce.Invoke(async ct => await SearchAsync(query, ct));   // earlier calls are cancelled
var throttle = new Throttler(TimeSpan.FromSeconds(1));
throttle.Invoke(() => Redraw());
var once = new Guard(); if (once.CheckSet()) Init();          // true only for the first caller

// Helpers
await Retry.ExecuteAsync(() => CallApiAsync(), maxRetries: 5, baseDelayMs: 200);   // retries on any exception
Humanize.Bytes(1_500_000);            // "1.5 MB"
Humanize.RelativeTime(when);          // "3 minutes ago"
Humanize.Plural(3, "file");           // "3 files"
using var watcher = DebouncedFileWatcher.ForFile(path);  // one batched Changed per burst
```

Details: [Niddy.Core pages](README.md#niddycore), [examples/settings-logging-paths](examples/settings-logging-paths.md).

---

## Gotchas

| Mistake | Correct |
|---|---|
| `Page<App, HomePage>` | `Page<MainDataContext, HomePageDataContext>`: app data context, then page data context |
| `[PageRegistration]` without the generator package | Add `Niddy.Avalonia.Generators`, or call `PageRegistry.Register<T>()` |
| Using `NiddyApp.Paths` / `Current.Paths` inside `Configure` | Build `AppPaths.For(...)` yourself there |
| `Dialog.Progress` reporting 0–1 | 0–100 (while `ToastHandle.Report` is 0–1) |
| Touching controls inside `Dialog.Progress` work | It runs on a background thread; only report progress |
| `FilePicker.OpenFilePath` in cross-platform code | `FilePicker.OpenFile` + `OpenReadAsync` |
| `Process.Start(url)` | `UriLauncher.TryLaunchAsync(this, url)` |
| Tokens in `SettingsStore` | `ISecureStorage` |
| `SecureStorageFactory.Create()` | `SecureStorageFactory.Create(paths.Data, "MyApp")`; `baseDirectory` is required |
| `xmlns:niddy` for `UriLauncher`/`WindowNotification` | They're in `Niddy.Avalonia.Utilities`, which isn't in the XAML namespace (use them from C#) |
| Reusing one custom dialog instance | New instance per `Dialog.Show` |
| Handling `SettingsStore.Changed` / `PageRegistry.PagesChanged` and touching UI | `Dispatcher.UIThread.Post(...)` |

## Where to look next

- [docs/README.md](README.md): every page.
- [docs/examples/](examples/): app-setup, settings-logging-paths, long-running-work, custom-dialog, mobile-and-browser, plugin-pages, oauth-sign-in.
