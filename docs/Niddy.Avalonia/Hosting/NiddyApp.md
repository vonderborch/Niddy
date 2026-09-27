# NiddyApp

`Niddy.Avalonia.Hosting` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Hosting/NiddyApp.cs)

An Avalonia `Application` that sets up the whole app from one `Configure` method: the page system, dialogs, toasts, theme, window memory, unhandled-exception handling, and the main window (desktop) or main view (mobile and browser). You write pages; `NiddyApp` builds the shell. No App.axaml is needed.

During `Initialize` it:

- calls `Configure`, and throws if `StartingPage` isn't set;
- adds the Fluent theme plus the DataGrid and ColorPicker styles (unless `UseFluentTheme` is false) and applies `Theme`;
- sets `Toast.DefaultDuration`, and, when `RememberWindowState` is on and no store is set yet, `UiStateStore.Default = UiStateStore.For(Paths)`;
- installs [`GlobalExceptionHandler`](GlobalExceptionHandler.md) with `LoggerFactory`, `ShowUnhandledExceptionDialog` and `ReportIssueLink`, and hooks `OnUnhandledException` to it (unless `HandleUnhandledExceptions` is false).

Once the framework has initialized, it resolves the [mode](#appmode), sets `Dialog.DefaultMode` (for `DialogMode = Auto`: windows on desktop, overlays on mobile) and creates the [`AppShell`](AppShell.md). On desktop it puts the shell in a centered main window, which remembers its placement under the [`WindowMemory`](../State/WindowMemory.md) key `"Main"` (not in the mobile preview). On single-view platforms the shell is the main view.

## API

| Member | Description |
|---|---|
| `protected abstract void Configure(NiddyAppOptions options)` | Set up the app. See [NiddyAppOptions](NiddyAppOptions.md). |
| `protected virtual Window CreateMainWindow(AppShell shell)` | Override to use your own `Window` subclass. For smaller tweaks use `options.ConfigureWindow`. |
| `static NiddyApp? Current` | The running app, or null if the app isn't a `NiddyApp`. |
| `static AppMode CurrentMode` | The running app's mode, or the mode decided from the platform if the app isn't a `NiddyApp`. |
| `NiddyAppOptions Options` | |
| `AppShell Shell` | The root view: `Shell.Pages`, `Shell.Toasts`, `Shell.Dialogs`. |
| `AppPaths Paths` | [`AppPaths`](../../Niddy.Core/IO/AppPaths.md) for the app name. Folders aren't created until you call `EnsureCreated()`. |
| `AppMode Mode` | Resolved once the framework has initialized. |
| `Initialize()`, `OnFrameworkInitializationCompleted()` | If you override `Initialize` (e.g. to load an App.axaml), call `base.Initialize()` after loading; skipping it throws at startup. |

The app name for `Paths` is `AppName`, else `Title`, else the entry assembly's name, else `"NiddyApp"`.

### AppMode

| Value | Meaning |
|---|---|
| `Auto` (default) | `Mobile` for a single-view lifetime or on Android, iOS and the browser; `Desktop` for a desktop lifetime. |
| `Desktop` | A resizable main window, dialogs in windows, toasts bottom-right (bottom-center when narrow). |
| `Mobile` | Full-screen view, overlay dialogs, toasts bottom-center. On desktop it gives a phone-sized window (`MobilePreviewWidth` × `MobilePreviewHeight`) to preview the mobile layout. |

Pages read it from `Page.AppMode`; anything else can use `NiddyApp.CurrentMode`.

## Examples

```csharp
using Avalonia;
using Avalonia.Animation;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;
using Niddy.Logging;

public class App : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        options.AppName = "MyApp";
        options.Title = "My App";
        options.StartingPage = HomePage.PageId;
        options.AppDataContext = new MainDataContext();
        options.PageTransition = new CrossFade(TimeSpan.FromMilliseconds(150));
        options.Layout = pages => new PageMenu { Content = pages };
        options.LoggerFactory = NiddyLogging.CreateFactory(AppPaths.For("MyApp"));
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

Navigating from outside a page:

```csharp
NiddyApp.Current!.Shell.Pages.NavigateTo<SettingsPage>();
```

A custom main window:

```csharp
protected override Window CreateMainWindow(AppShell shell)
{
    var window = base.CreateMainWindow(shell);
    window.Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://MyApp/Assets/icon.ico")));
    return window;
}
```

Keeping an App.axaml:

```csharp
public override void Initialize()
{
    AvaloniaXamlLoader.Load(this);
    base.Initialize();
}
```

## See also

- [NiddyAppOptions](NiddyAppOptions.md), [AppShell](AppShell.md), [GlobalExceptionHandler](GlobalExceptionHandler.md)
- [examples/app-setup](../../examples/app-setup.md), [examples/mobile-and-browser](../../examples/mobile-and-browser.md)
