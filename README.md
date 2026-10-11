<p align="center">
  <img src="https://raw.githubusercontent.com/vonderborch/Niddy/main/assets/branding/niddy-package-icon.png" alt="Niddy logo" width="160" height="160" />
</p>

# Niddy

> **This is my personal utility library.** It's public in case it's useful to someone, but it's built for my own projects first: APIs may change between versions, and issues or PRs may not get a response. Use it, fork it, or borrow from it freely (MIT).

A collection of general-purpose .NET utilities and Avalonia UI helpers, split into focused NuGet packages so you only pull in what you need.

## Packages

| Package | What's in it | NuGet |
|---|---|---|
| **Niddy.Core** | Files, archives, JSON, retry, threading primitives, and platform-native secure storage; no UI dependencies | [![NuGet](https://img.shields.io/nuget/v/Niddy.Core.svg?style=flat-square)](https://www.nuget.org/packages/Niddy.Core/) |
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

- **File system** (`Niddy.Core.IO`): C# 14 extensions on `File`, `Directory` and `Path` (`Directory.DeleteWithRetry` for locked or read-only files, `Directory.CreateEmpty`, `Directory.Copy` with exclusions, `Path.Comparison`, `Path.IsInList`, `Path.SanitizeFileName`), and `stream.ReadToStringAsync()`
- **Backups** (`Niddy.Core.IO.Backups`): `File.Backup` / `Directory.Backup` make uniquely named copies or archives and prune old ones
- **Archives** (`Niddy.Core.IO.Archives`): zip, tar and tar.gz with `Archive` (create, safely extract, read or stream single files)
- **JSON** (`Niddy.Core.Serialization`): `JsonFile` reads and atomically writes JSON files; `JsonSerializerOptions.Niddy` and `JsonSerializer.DeserializeOrDefault`
- **Retry** (`Niddy.Core.Resilience`): retry with exponential backoff and optional jitter (sync and async), throwing `MaxRetriesException` or `RetryBlockedException` when it stops
- **Small extensions**: `string.IsNullOrWhiteSpace()` and `Join` (`Niddy.Core.Text`), `List<T>` extensions (`Niddy.Core.Collections`), `value.DisposeIfPossible()` (`Niddy.Core`)
- **Threading** (`Niddy.Core.Threading`): lock-free `Guard` (atomic boolean flag) and `AtomicOperations`, plus `Debouncer` and `Throttler` for bursty events (sync or async actions, `TimeProvider`-driven, posting back to the captured synchronization context)
- **Results** (`Niddy.Core.Results`): `Result`, `Result<T>` and `Option<T>` value types with `Match`/`Map`/`Bind`, `Result.Try`/`TryAsync` and an `Error` record
- **Humanize** (`Niddy.Core.Text`): file sizes (`1.5 MB`), counts (`12.3K`), durations (`5 min 3 s`), relative times (`3 minutes ago`), plurals and ordinals
- **Files** (`Niddy.Core.IO`): `AtomicFile` writes (temp file + replace, so a crash never leaves half a file), `DebouncedFileWatcher` (one batched `Changed` event per burst), and `AppPaths` for per-platform data, config, cache and log folders
- **Logging** (`Niddy.Core.Logging`): a rolling file logger for `Microsoft.Extensions.Logging` (`AddFile`, `NiddyLogging.CreateFactory`; the provider is in `.Files`), plus `BatchingLoggerProvider` (`.Batching`) for writing your own non-blocking providers (a database, a log server) and `AddOwnedProvider` to register them
- **Settings** (`Niddy.Core.Settings`): `SettingsStore<T>`, a typed JSON settings file with atomic saves, `Update`/`Reset`, optional reload on external changes, and a built-in file version with step-by-step upgrades and a choice of what happens when one fails or is missing (`Version`, `ConfigureMigrations`, `MigrationFailureAction`, `TreatMissingMigrationAsError`)
- **Secure storage** (`Niddy.Core.Security`): platform-native secure storage via `ISecureStorage` — Keychain on every Apple platform, Windows Credential Manager, Linux Secret Service, with an encrypted-file fallback (and an error in the browser, which can't keep secrets); `SecureStorageFactory` auto-selects the right implementation, and apps or other packages can register their own providers to replace, wrap or extend it
- **GitHub issues** (`Niddy.Core.GitHub`): open a bug report, feature request or any other issue from anywhere in an app with `GitHubIssues.CreateAsync` (REST API and a token, returning a `Result`), `OpenInBrowser` (GitHub's new-issue form, filled in, no token), or `CreateOrOpenAsync` (the API with a browser fallback); `GitHubIssue.FromException` crash reports, collapsible log sections (`GitHubIssueSection.FromLatestLogFile`), and an optional reporter, details table and system info. `GitHubReleases` reads releases, checks for updates (`CheckForUpdateAsync`, semver-aware), picks the asset for the current platform (`FindAssetForPlatform`) and downloads it verified against GitHub's digest or a checksum file, with an optional disk cache that keeps you under the rate limit
- **Web** (`Niddy.Core.Web`): `WebContent.DownloadFileAsync` and `DownloadToFolderAsync` (atomic downloads with progress, a size limit, SHA-256 checking and resuming), `ReadTextAsync`, `ReadBytesAsync`, `ReadJsonAsync` and `ReadLinesAsync` (read a URL whole or stream it line by line), returning `Result`s, each with a synchronous version; `UrlLauncher.Open` opens a URL in the browser without a UI framework
- **Platform info** (`Niddy.Core.SystemInfo`): `Platform.Info` with the OS and its version, architecture, Rust/LLVM-style target triple (`aarch64-apple-darwin`, `x86_64-unknown-linux-musl`), runtime, memory and estimated GPUs (Mac Catalyst, iPadOS, browser and WASI included, on every architecture .NET supports), plus `DeclareSupportedOperatingSystems` to fail fast on unsupported OSes

### Niddy.Avalonia

- **App setup** (`Niddy.Avalonia.Hosting`): derive your `App` from `NiddyApp` and configure pages, dialogs, toasts, theme and the main window in one `Configure` method. It decides between desktop and mobile mode itself (or you set `Mode`) and builds the right shell, so you only write pages, never windows
- **Dialog system** (`Niddy.Avalonia.Dialogs`): `Dialog` static facade for Notification, Confirmation, Warning, Input, MultiInput, Progress, Selection, Exception, Color, Markdown, Table and Web dialogs. Each dialog is one control, shown either as an overlay card or in a modal window (`DialogDisplayMode.Auto` picks); derive from `DialogBase<TResult>` and call `Dialog.Show` for your own dialogs. Every dialog inherits the same title, description and button-row layout from `DialogLayout`. Window dialogs take an optional fixed `windowWidth` and `windowHeight`. Escape, the window close button, and the platform back request act as the cancel button
- **Page system** (`Niddy.Avalonia.PageSystem`): derive pages from `Page` or `Page<TAppDataContext, TPageDataContext>`, register them explicitly or automatically with the generator (including pages in referenced projects and plugin assemblies loaded at runtime), and show them in a `PageView` with a back stack, parameters, lifecycle hooks, navigation guards, kept-alive pages, nested child views and page transitions. Pages can declare keyboard shortcuts (`Shortcut = "Primary+1"`). Menus list pages alphabetically, or in any order you define with a sort key per page or a sort function
- **Data contexts** (`Niddy.Avalonia.DataContexts`): `DataContextBase`, a ReactiveUI base class for a view's data context with `IsBusy`/`ErrorMessage` and a `RunAsync` helper
- **Toasts** (`Niddy.Avalonia.Toasts`): `Toast.Show` over a `ToastHost`, shown at any of eight screen positions (`ScreenPlacement`), with a separate placement for mobile and narrow windows. Toasts can carry an action button (Undo, Retry…), repeated toasts merge into one with a count, and `Toast.ShowProgress` shows a progress toast you update and complete
- **File pickers** (`Niddy.Avalonia`): `FilePicker` returns `IStorageFile`/`IStorageFolder` handles, with desktop-only `*Path` variants
- **Window and layout memory** (`Niddy.Avalonia.State`): windows reopen at their last size, position and state (kept on screen if a monitor went away), and `Grid`/`ProportionalStackPanel` splitters keep their sizes, via `WindowMemory.Key`/`LayoutMemory.Key` attached properties
- **Global exception handler** (`Niddy.Avalonia.Hosting`): logs UI-thread, unobserved-task and background-thread exceptions and shows `Dialog.Exception` instead of crashing
- **Converters and markup** (`Niddy.Avalonia.Converters`): enum-to-bool (radio buttons), bool-to-value, humanize and collection-emptiness converters, `{niddy:EnumValues}`, and one `niddy` XAML namespace for everything
- **Responsive layout** (`Niddy.Avalonia.Layout`): `Responsive.NarrowBelow`/`WideAbove` add `narrow`/`medium`/`wide` classes by width for styles to target

### Desktop, mobile, and browser

Everything works on desktop. For mobile and browser apps (a single view, no windows):

- With `NiddyApp` this is already set up. Otherwise, use an `AppShell` as your main view (`new AppShell(options)`), or wrap your content in a `DialogOverlayHost` (and a `ToastHost` for toasts). Dialogs then open as overlays, including `Dialog.Exception`; without a host, dialogs throw a clear `InvalidOperationException` because there's no window to open.
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
using Niddy.Avalonia.Toasts;

if (await Dialog.Warning.Open(this, "Delete project?", "This can't be undone.", yesButtonText: "Delete"))
{
    DeleteProject();
    Toast.Show(this, "Project deleted", actionText: "Undo", action: RestoreProject);
}

NavigateTo<SettingsPage>();
```

And Niddy.Core, in any app:

```csharp
using Niddy.Core.IO;
using Niddy.Core.Logging;
using Niddy.Core.Security;

AppPaths paths = AppPaths.For("MyApp").EnsureCreated();                  // per-platform data, config, cache, log folders
using ILoggerFactory loggerFactory = NiddyLogging.CreateFactory(paths);  // rolling log files in paths.Logs
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, "MyApp"); // keychain / credential manager
secrets.SetToken("api-key", key);
```

See the [docs](https://github.com/vonderborch/Niddy/blob/main/docs/README.md) for everything else.

## Development

Requires the .NET 10 SDK (pinned in [global.json](https://github.com/vonderborch/Niddy/blob/main/global.json)). C# 14, Avalonia 12, xUnit v3.

### Layout

```
Niddy.slnx                             solution: /src/ and /tests/ solution folders
src/Niddy.Core/<Folder>/*.cs           namespace Niddy.Core.<Folder>       (e.g. Niddy.Core.Settings, Niddy.Core.IO.Archives)
src/Niddy.Avalonia/<Folder>/*.cs       namespace Niddy.Avalonia.<Folder>   (e.g. Niddy.Avalonia.PageSystem)
src/Niddy.Avalonia.Generators/         netstandard2.0, packed with its .nuspec
tests/<Project>.Tests/                 one test project per package
tests/Fixtures/                        small page libraries the tests reference or load at runtime
docs/                                  documentation
```

A new Avalonia folder whose types are used from XAML also needs an `XmlnsDefinition` line in `src/Niddy.Avalonia/XmlnsDefinitions.cs` (every public namespace is mapped to `https://github.com/vonderborch/Niddy`; `Dialogs.BuiltIn` holds only internal dialogs, so it isn't).

### Build and test

```bash
dotnet build
dotnet test
```

- Builds must be warning-free: `TreatWarningsAsErrors` is on in `Directory.Build.props`, and that includes missing or broken XML docs. The only suppression is AVLN3001 on `ToastItem`, in `Niddy.Avalonia.csproj`.
- Avalonia tests run headless (`[AvaloniaFact]`); call `Dispatcher.UIThread.RunJobs()` to flush posted work.
- Generator tests run the generator on in-memory compilations.
- Add tests with every behavior change.

### Code conventions

- File-scoped namespaces; XML doc comments on every public member (they feed IntelliSense and the docs).
- Folders, namespaces and docs folders match: `src/Niddy.Core/IO/Archives/Archive.cs` is `Niddy.Core.IO.Archives`, documented in `docs/Niddy.Core/IO/Archives/`.
  - A feature made of several public types gets its own folder (or subfolder).
  - Don't split a feature into sub-namespaces that its users would import together. Everything for pages is in `PageSystem`, so one `using` covers it.
  - A single type that doesn't belong to a domain goes in the project root namespace (`Niddy.Core.DisposableExtensions`).
  - Never name a namespace after a type in it (`Toasts` holds `Toast`), or after a BCL or Avalonia type its users will also import (`Logging.Files`, not `File`).
  - No catch-all `Helpers` or `Utilities` folders: put a type in the domain it serves.
- Extensions use C# 14 `extension` blocks in a static class named `<Target>Extensions` (`FileSystemExtensions`, `StreamExtensions`). Add static members to BCL types where that reads naturally (`Directory.DeleteWithRetry`, `Path.Comparison`). Don't reuse a name the target already has: the BCL's own member always wins.
- Explicit types, no `var` unless it's required (anonymous types). When the type is on the left, use target-typed `new`: `List<string> names = new();`, `Button button = new() { Content = "OK" };`. Always use braces on control blocks (`if`, `else`, etc.). Code examples in `docs/` follow the same style. These rules are in `.editorconfig` and enforced by the build (`EnforceCodeStyleInBuild`).
- `System.Threading.Lock` (`private readonly Lock _gate = new();`) for locking.
- Inside `Niddy.Avalonia`, refer to Avalonia types as `global::Avalonia.*` where the `Niddy.Avalonia` namespace would shadow them.
- Match the surrounding code's style and comment density.
- Things that must work off desktop (dialogs, toasts, file pickers, navigation) need a mobile/browser path; don't assume windows exist.
- Don't remove package references from the `.csproj` files; some are there for upcoming work.

### Documentation

Any change to the public API or to documented behavior updates the docs in the same change.

**Every new public type is documented when it's added: classes, records, structs, interfaces, enums, delegates, attributes, everything.** There are no exceptions for "small", "internal-feeling" or "helper" types. A new top-level type gets its own page (or a section on its owning class's page, if it's a small supporting type per the rules below), an entry in `docs/README.md`, and, if it changes how the library is used, an update to `docs/AI-GUIDE.md`. New members on existing types are added to that type's page. A change that adds an undocumented public type is not finished.

- `docs/<Project>/<Folder>/<Class>.md`: one page per public class, mirroring the source layout under `src/`. Small supporting types (enums, event args, options records, nested types) go on the page of the class that uses them. Internal types are not documented.
- Page shape: `# Name`, a line with `` `Namespace` · Package · [source](../../../src/<Project>/<Folder>/<Class>.cs) ``, a short summary, `## API` tables, `## Example` / `## Examples`, `## See also`.
- `docs/examples/*.md`: walkthroughs that span several classes. Add one when a feature only makes sense combined with others.
- `docs/README.md`: the index. Add, rename or remove pages there too.
- `docs/AI-GUIDE.md`: a single-file guide to the whole library for AI assistants. Update it when a change affects how the library should be used: new features, changed signatures, new gotchas.
- This `README.md`: packages, features, a short quick start and links into `docs/`. It is packed into every NuGet package, so its links into the repo are absolute GitHub URLs. Keep usage detail in `docs/`.

Write docs from the source, not from memory: check each member's signature and XML docs, make sure examples compile against the real API, and make sure relative links resolve.

### Contributing

1. Clone or fork the repo
2. Create a new branch
3. Code!
4. Push your changes and open a PR
5. Once approved, they'll be merged in
6. Profit!

### Releasing

All packages share one [SemVer 2](https://semver.org) version, taken from the GitHub release tag.

1. Create a GitHub release with a tag like `v1.2.0` (or `v1.3.0-beta.1` for a pre-release; the leading `v` is optional)
2. Publishing the release runs the [publish workflow](https://github.com/vonderborch/Niddy/blob/main/.github/workflows/nuget-publish.yml), which runs the tests, then packs every project at that version and pushes it to NuGet

Publishing uses nuget.org [trusted publishing](https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing), so no API key is stored in the repo. It needs, once:

- a trusted publishing policy on nuget.org (your username → **Trusted Publishing**) for owner `vonderborch`, repository `Niddy` and workflow file `nuget-publish.yml`
- a `NUGET_USER` repository secret holding the nuget.org profile name that owns the packages

Local builds are versioned `0.0.0-dev`.

## Future Plans

See list of issues under the Milestones: https://github.com/vonderborch/Niddy/milestones
