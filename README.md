# Niddy

> **This is my personal utility library.** It's public in case it's useful to someone, but it's built for my own projects first: APIs may change between versions, and issues or PRs may not get a response. Use it, fork it, or borrow from it freely (MIT).

A collection of general-purpose .NET utilities and Avalonia UI helpers, split into focused NuGet packages so you only pull in what you need.

## Packages

| Package | What's in it | NuGet |
|---|---|---|
| **Niddy.Core** | Helpers, threading primitives, and platform-native secure storage; no UI dependencies | [![NuGet](https://img.shields.io/nuget/v/Niddy.Core.svg?style=flat-square)](https://www.nuget.org/packages/Niddy.Core/) |
| **Niddy.Avalonia** | Avalonia UI dialogs, pages, toasts, theming, file pickers | [![NuGet](https://img.shields.io/nuget/v/Niddy.Avalonia.svg?style=flat-square)](https://www.nuget.org/packages/Niddy.Avalonia/) |
| **Niddy.Avalonia.Generators** | Optional source generator that registers `[PageRegistration]` pages automatically; brings in Niddy.Avalonia | [![NuGet](https://img.shields.io/nuget/v/Niddy.Avalonia.Generators.svg?style=flat-square)](https://www.nuget.org/packages/Niddy.Avalonia.Generators/) |

```bash
dotnet add package Niddy.Core
dotnet add package Niddy.Avalonia
dotnet add package Niddy.Avalonia.Generators   # optional: automatic page registration
```

Alternatively, clone this repo and reference the relevant project(s) directly.

## Features

### Niddy.Core

- **Helpers** (`Niddy.Helpers`): byte manipulation, disposing utilities, IO/path/stream/URL helpers, JSON helpers, list extensions, string extensions, and retry-with-exponential-backoff (sync and async)
- **Threading** (`Niddy.Threading`): lock-free `Guard` (atomic boolean flag) and `AtomicOperations`, plus `Debouncer` and `Throttler` for bursty events (sync or async actions, `TimeProvider`-driven, posting back to the captured synchronization context)
- **Results** (`Niddy.Results`): `Result`, `Result<T>` and `Option<T>` value types with `Match`/`Map`/`Bind`, `Result.Try`/`TryAsync` and an `Error` record
- **Humanize** (`Niddy.Helpers`): file sizes (`1.5 MB`), counts (`12.3K`), durations (`5 min 3 s`), relative times (`3 minutes ago`), plurals and ordinals
- **Files** (`Niddy.IO`): `AtomicFile` writes (temp file + replace, so a crash never leaves half a file), `DebouncedFileWatcher` (one batched `Changed` event per burst), and `AppPaths` for per-platform data, config, cache and log folders
- **Logging** (`Niddy.Logging`): a rolling file logger for `Microsoft.Extensions.Logging` (`AddFile`, `NiddyLogging.CreateFactory`)
- **Settings** (`Niddy.Settings`): `SettingsStore<T>`, a typed JSON settings file with atomic saves, `Update`/`Reset`, and optional reload on external changes
- **Secure storage** (`Niddy.Security`): platform-native secure storage via `ISecureStorage` — macOS Keychain, Windows Credential Manager, Linux Secret Service, with an encrypted-file fallback; `SecureStorageFactory` auto-selects the right implementation

### Niddy.Avalonia

- **App setup** (`Niddy.Avalonia.Hosting`): derive your `App` from `NiddyApp` and configure pages, dialogs, toasts, theme and the main window in one `Configure` method. It decides between desktop and mobile mode itself (or you set `Mode`) and builds the right shell, so you only write pages, never windows
- **Dialog system** (`Niddy.Avalonia.Dialogs`): `Dialog` static facade for Notification, Confirmation, Warning, Input, MultiInput, Progress, Selection, Exception, Color, Markdown, Table and Web dialogs. Each dialog is one control, shown either as an overlay card or in a modal window (`DialogDisplayMode.Auto` picks); derive from `DialogBase<TResult>` and call `Dialog.Show` for your own dialogs. Every dialog inherits the same title, description and button-row layout from `DialogLayout`. Window dialogs take an optional fixed `windowWidth` and `windowHeight`. Escape, the window close button, and the platform back request act as the cancel button
- **Page system** (`Niddy.Avalonia.PageSystem`): derive pages from `Page` or `Page<TAppDataContext, TPageDataContext>`, register them explicitly or automatically with the generator (including pages in referenced projects and plugin assemblies loaded at runtime), and show them in a `PageView` with a back stack, parameters, lifecycle hooks, navigation guards, kept-alive pages, nested child views and page transitions. Pages can declare keyboard shortcuts (`Shortcut = "Primary+1"`)
- **Data contexts** (`Niddy.Avalonia.DataContexts`): `DataContextBase`, a ReactiveUI base class for a view's data context with `IsBusy`/`ErrorMessage` and a `RunAsync` helper
- **Toasts**: `Toast.Show` over a `ToastHost`, shown at any of eight screen positions (`ScreenPlacement`), with a separate placement for mobile and narrow windows. Toasts can carry an action button (Undo, Retry…), repeated toasts merge into one with a count, and `Toast.ShowProgress` shows a progress toast you update and complete
- **File pickers**: `FilePicker` returns `IStorageFile`/`IStorageFolder` handles, with desktop-only `*Path` variants
- **Window and layout memory** (`Niddy.Avalonia.State`): windows reopen at their last size, position and state (kept on screen if a monitor went away), and `Grid`/`ProportionalStackPanel` splitters keep their sizes, via `WindowMemory.Key`/`LayoutMemory.Key` attached properties
- **Global exception handler** (`Niddy.Avalonia.Hosting`): logs UI-thread, unobserved-task and background-thread exceptions and shows `Dialog.Exception` instead of crashing
- **Converters and markup** (`Niddy.Avalonia.Converters`): enum-to-bool (radio buttons), bool-to-value, humanize and collection-emptiness converters, `{niddy:EnumValues}`, and one `niddy` XAML namespace for everything
- **Responsive layout** (`Niddy.Avalonia.Layout`): `Responsive.NarrowBelow`/`WideAbove` add `narrow`/`medium`/`wide` classes by width for styles to target

### Desktop, mobile, and browser

Everything works on desktop. For mobile and browser apps (a single view, no windows):

- With `NiddyApp` this is already set up. Otherwise, wrap your main view's content in a `DialogOverlayHost` (and a `ToastHost` for toasts). Dialogs then open as overlays, including `Dialog.Exception`; without a host, dialogs throw a clear `InvalidOperationException` because there's no window to open.
- Use the `FilePicker` methods that return storage handles; the `*Path` variants return null there.
- The platform back request (Android back button, browser back) dismisses an open overlay dialog first, then goes back a page in the most recently attached `PageView`. Set `HandleBackRequests="False"` on a `PageView` to opt out.
- Overlay dialogs and toasts stay clear of safe-area insets (notch, status and navigation bars) and the on-screen keyboard.

## Documentation

Full docs live in [`docs/`](https://github.com/vonderborch/Niddy/blob/main/docs/README.md): a page per class, with examples, organized by package and folder.

- **[Docs index](https://github.com/vonderborch/Niddy/blob/main/docs/README.md)**: every class in Niddy.Core, Niddy.Avalonia and the generator.
- **Walkthroughs** that span several classes:
  - [App setup, end to end](https://github.com/vonderborch/Niddy/blob/main/docs/examples/app-setup.md)
  - [Settings, logging, paths and secrets](https://github.com/vonderborch/Niddy/blob/main/docs/examples/settings-logging-paths.md)
  - [Long-running work](https://github.com/vonderborch/Niddy/blob/main/docs/examples/long-running-work.md)
  - [A custom dialog](https://github.com/vonderborch/Niddy/blob/main/docs/examples/custom-dialog.md)
  - [Mobile and browser](https://github.com/vonderborch/Niddy/blob/main/docs/examples/mobile-and-browser.md)
  - [Plugin pages](https://github.com/vonderborch/Niddy/blob/main/docs/examples/plugin-pages.md)
  - [OAuth sign-in](https://github.com/vonderborch/Niddy/blob/main/docs/examples/oauth-sign-in.md)
- **[AI-GUIDE.md](https://github.com/vonderborch/Niddy/blob/main/docs/AI-GUIDE.md)**: one file that explains how to use the whole library, written for AI coding assistants (and handy for people too). Point your assistant at it.

## Quick start

An Avalonia app is an `App` class deriving from `NiddyApp`, plus pages:

```csharp
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;

public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        options.Title = "My App";
        options.StartingPage = HomePage.PageId;
        options.AppDataContext = new MainDataContext();
        options.Layout = pages => new PageMenu { Content = pages };   // optional navigation menu
    }
}

// Registered at compile time by Niddy.Avalonia.Generators
[PageRegistration(DisplayName = "Home", Shortcut = "Primary+1")]
public partial class HomePage : Page<MainDataContext, HomePageDataContext>
{
    public HomePage() => InitializeComponent();
}

// Program.cs (desktop)
AppBuilder.Configure<App>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
```

Then, from any page:

```csharp
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Toast;

if (await Dialog.Warning.Open(this, "Delete project?", "This can't be undone.", yesButtonText: "Delete"))
{
    DeleteProject();
    Toast.Show(this, "Project deleted", actionText: "Undo", action: RestoreProject);
}

NavigateTo<SettingsPage>();
```

And Niddy.Core, in any app:

```csharp
using Niddy.IO;
using Niddy.Logging;
using Niddy.Security;

var paths = AppPaths.For("MyApp").EnsureCreated();                 // per-platform data, config, cache, log folders
using var loggerFactory = NiddyLogging.CreateFactory(paths);         // rolling log files in paths.Logs
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, "MyApp");   // keychain / credential manager
secrets.SetToken("api-key", key);
```

See the [docs](https://github.com/vonderborch/Niddy/blob/main/docs/README.md) for everything else.

## Development

Requires the .NET 10 SDK (pinned in [global.json](global.json)).

The packages live under `src/`, and each has a test project under `tests/` (xUnit v3; the Avalonia tests run headless, and the generator tests run the generator on in-memory compilations). `tests/Fixtures/` holds small page libraries the tests reference or load at runtime. Run them all with `dotnet test`.

1. Clone or fork the repo
2. Create a new branch
3. Code!
4. Push your changes and open a PR
5. Once approved, they'll be merged in
6. Profit!

### Releasing

All packages share one [SemVer 2](https://semver.org) version, taken from the GitHub release tag.

1. Create a GitHub release with a tag like `v1.2.0` (or `v1.3.0-beta.1` for a pre-release; the leading `v` is optional)
2. Publishing the release runs the [publish workflow](.github/workflows/nuget-publish.yml), which runs the tests, then packs every project at that version and pushes it to NuGet

Publishing uses nuget.org [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), so no API key is stored in the repo. It needs, once:

- a trusted publishing policy on nuget.org (your username → **Trusted Publishing**) for owner `vonderborch`, repository `Niddy` and workflow file `nuget-publish.yml`
- a `NUGET_USER` repository secret holding the nuget.org profile name that owns the packages

Local builds are versioned `0.0.0-dev`.

## Future Plans

See list of issues under the Milestones: https://github.com/vonderborch/Niddy/milestones
