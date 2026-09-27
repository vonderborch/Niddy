# NiddyAppOptions

`Niddy.Avalonia.Hosting` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Hosting/NiddyAppOptions.cs)

Everything [`NiddyApp`](NiddyApp.md) sets up, filled in by `NiddyApp.Configure`. Only `StartingPage` is required.

## API

### App

| Property | Default | Description |
|---|---|---|
| `AppName` | `Title`, else the entry assembly name | Used for the app's folders ([`NiddyApp.Paths`](NiddyApp.md)). |
| `Organization` | null | The parent folder on Windows, e.g. `"Contoso"`. |
| `Mode` | `AppMode.Auto` | Desktop or mobile behavior. See [AppMode](NiddyApp.md#appmode). |

### Pages

| Property | Default | Description |
|---|---|---|
| `StartingPage` | **required** | e.g. `HomePage.PageId`. |
| `AppDataContext` | null | Every page's `AppDataContext`. Pages deriving from `Page<TApp, TPage>` need it to be a `TApp`. |
| `PageTransition` | null (instant) | e.g. `new PageSlide(TimeSpan.FromMilliseconds(200))`. |
| `HandleBackRequests` | true | Android back, iOS back gesture and browser back go back a page. An open overlay dialog is always dismissed first. |
| `Layout` | null | `Func<PageView, Control>` that wraps the page view in your chrome, e.g. a [`PageMenu`](../PageSystem/PageMenu.md). Dialogs and toasts still cover it. |

### Theme

| Property | Default | Description |
|---|---|---|
| `UseFluentTheme` | true | Adds Fluent with the DataGrid and ColorPicker styles. Set false if your App.axaml adds a theme. |
| `Theme` | null (follow the OS) | `AppTheme.Light` or `AppTheme.Dark`. |

### Dialogs and toasts

| Property | Default | Description |
|---|---|---|
| `DialogMode` | `Auto` | Sets [`Dialog.DefaultMode`](../Dialogs/Dialog.md). Auto: windows on desktop, overlays on mobile. |
| `ToastPlacement` | `BottomRight` | Where toasts go on wide screens. |
| `ToastNarrowPlacement` | `BottomCenter` | Mobile mode and narrow windows. Null always uses `ToastPlacement`. |
| `ToastDuration` | 3 s | Sets `Toast.DefaultDuration`. |

### Main window (desktop)

| Property | Default | Description |
|---|---|---|
| `Title` | `""` | |
| `Width`, `Height` | 1024 × 768 | Initial size. |
| `MinWidth`, `MinHeight` | 0 | |
| `MobilePreviewWidth`, `MobilePreviewHeight` | 390 × 844 | Window size with `Mode = Mobile` on desktop. |
| `RememberWindowState` | true | Save and restore size, position and maximized state in `UiStateStore.Default`. |
| `ConfigureWindow` | null | `Action<Window>` run after the options above, e.g. to set the icon. |

### Logging and errors

| Property | Default | Description |
|---|---|---|
| `LoggerFactory` | null | Where the app, including unhandled exceptions, logs. e.g. `NiddyLogging.CreateFactory(AppPaths.For("MyApp"))`. |
| `HandleUnhandledExceptions` | true | Install [`GlobalExceptionHandler`](GlobalExceptionHandler.md). |
| `ShowUnhandledExceptionDialog` | true | Show `Dialog.Exception` for UI-thread exceptions. |
| `ReportIssueLink` | `""` | The exception dialog's Report Issue URL. The button is hidden when empty. |
| `OnUnhandledException` | null | `Action<UnhandledExceptionOccurredEventArgs>`, called first; set `Handled` or `ShowDialog` to change the handling. |

## Example

```csharp
protected override void Configure(NiddyAppOptions options)
{
    options.StartingPage = HomePage.PageId;
    options.Title = "Notes";
    options.MinWidth = 400;
    options.Theme = AppTheme.Dark;
    options.DialogMode = DialogDisplayMode.Overlay;
    options.ToastPlacement = ScreenPlacement.TopRight;
    options.ConfigureWindow = w => w.CanResize = true;
    options.OnUnhandledException = e =>
    {
        if (e.Exception is OperationCanceledException)
            e.Handled = true;
    };
#if DEBUG
    options.Mode = AppMode.Mobile;   // preview the phone layout
#endif
}
```

## See also

- [NiddyApp](NiddyApp.md)
