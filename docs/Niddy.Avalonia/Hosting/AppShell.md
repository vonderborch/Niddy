# AppShell

`Niddy.Avalonia.Hosting` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Hosting/AppShell.cs)

The root view that [`NiddyApp`](NiddyApp.md) creates. It's a [`PageView`](../PageSystem/PageView.md), optionally wrapped by `NiddyAppOptions.Layout`, under a [`ToastHost`](../Toasts/ToastHost.md), all under a [`DialogOverlayHost`](../Dialogs/DialogOverlayHost.md). On desktop it's the main window's content; on mobile and browser it's the main view. Get it from `NiddyApp.Current!.Shell`.

You can also create one yourself, e.g. to render pages in a test or to host them in an app that isn't a `NiddyApp`. The shell only builds the views. The main window, `Dialog.DefaultMode`, the page sort and other app-wide settings are still set up by `NiddyApp`.

```
DialogOverlayHost   (Dialogs)
└── ToastHost       (Toasts)
    └── Layout(PageView) or PageView   (Pages)
```

## API

| Member | Description |
|---|---|
| `AppShell(NiddyAppOptions? options = null, AppMode mode = AppMode.Auto)` | Builds the page view, toast host and dialog host from `options`: the starting page, app data context, layout, page transition, back handling and toast placement. Null options show no page until you navigate. `mode: Auto` uses `options.Mode`, and if that is `Auto` too, decides from the application lifetime, then the platform. |
| `AppMode Mode` | Desktop or mobile. |
| `PageView Pages` | The app's page view. Use it to navigate from outside a page. |
| `ToastHost Toasts` | |
| `DialogOverlayHost Dialogs` | Covers toasts too. |

## Examples

```csharp
AppShell shell = NiddyApp.Current!.Shell;
shell.Pages.NavigateTo(HomePage.PageId, addToBackStack: false);
Toast.Show(shell, "Saved");                         // any control inside the shell works as the source
await Dialog.Notification.Open(shell, "Hello", "From outside a page.");
```

A headless render test (Avalonia.Headless with xUnit) with the same host stack as the app, so `Toast.Show` and overlay dialogs work inside the page:

```csharp
[AvaloniaFact]
public void HomePageRenders()
{
    AppShell shell = new(new NiddyAppOptions
    {
        StartingPage = HomePage.PageId,
        AppDataContext = new MainDataContext(AppPaths.For("NotesTests")),
    });
    new Window { Content = shell }.Show();
    Dispatcher.UIThread.RunJobs();

    Assert.IsType<HomePage>(shell.Pages.PageControlDataContext.ActivePage);
}
```

## See also

- [NiddyApp](NiddyApp.md)
