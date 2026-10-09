# Niddy: a guide for AI assistants

This one file tells you how to use the whole library in an app. It's written for AI coding assistants, but people can read it too. Every API here matches the source. For the complete API of a class, open its page from the [docs index](README.md): `docs/<Package>/<Folder>/<Class>.md`.

**Stack:** .NET 10, C# 14, Avalonia 12. Packages:

| Package | Use it for | Namespaces |
|---|---|---|
| `Niddy.Core` | Anything (no UI): results, settings, logging, paths, files, archives, JSON, retry, secure storage, threading, platform info | Namespaces follow the folders: `Niddy.Core` (`UrlLauncher`, `DisposeIfPossible`), `.Collections`, `.IO`, `.IO.Archives`, `.IO.Backups`, `.Logging`, `.Logging.Files`, `.Logging.Batching`, `.Resilience`, `.Results`, `.Security`, `.Serialization`, `.Settings`, `.SystemInfo`, `.Text`, `.Threading` |
| `Niddy.Avalonia` | Avalonia apps: app hosting, pages, dialogs, toasts, state memory, converters | Namespaces follow the folders: `Niddy.Avalonia` (`FilePicker`, `UriLauncher`, `WindowNotification`, `ScreenPlacement`), `.Hosting`, `.PageSystem` (everything for pages: `Page`, `PageView`, `[PageRegistration]`, `PageRegistry`, `[PageIcon]`, `PageMenu`), `.Dialogs`, `.Toasts`, `.State`, `.Converters`, `.DataContexts`, `.Layout`, `.Theme` |
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
| `Show(p, new MyDialog(…), maxWidth = 500)` | `Task<TResult>` for a custom dialog |

All calls also accept `maxWidth`, `displayMode`, `windowWidth` and `windowHeight`.

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
//   protected override void ConfigureMigrations(SettingsMigrations migrations) => migrations   // one lambda per version
//       .Add(fromVersion: 0, json => json.RenameProperty("colour", "color"))
//       .Add(fromVersion: 1, json => json.Remove("legacy"));   // a step from >= Version throws on read: bump Version
//   protected override void OnUpgraded(Settings s, int fromVersion) { … }   // optional typed fix-up after migrating
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
UrlLauncher.Open("https://example.com");   // no UI framework needed; not on Android

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
| `Process.Start(url)` | `UriLauncher.TryLaunchAsync(this, url)` in Avalonia; `UrlLauncher.Open(url)` without a UI (not on Android) |
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
