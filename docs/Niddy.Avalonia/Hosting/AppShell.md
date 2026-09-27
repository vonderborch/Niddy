# AppShell

`Niddy.Avalonia.Hosting` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Hosting/AppShell.cs)

The root view that [`NiddyApp`](NiddyApp.md) creates. It's a [`PageView`](../PageSystem/PageView.md), optionally wrapped by `NiddyAppOptions.Layout`, under a [`ToastHost`](../Toast/ToastHost.md), all under a [`DialogOverlayHost`](../Dialogs/DialogOverlayHost.md). On desktop it's the main window's content; on mobile and browser it's the main view. Get it from `NiddyApp.Current!.Shell`.

```
DialogOverlayHost   (Dialogs)
└── ToastHost       (Toasts)
    └── Layout(PageView) or PageView   (Pages)
```

## API

| Member | Description |
|---|---|
| `AppMode Mode` | Desktop or mobile. |
| `PageView Pages` | The app's page view. Use it to navigate from outside a page. |
| `ToastHost Toasts` | |
| `DialogOverlayHost Dialogs` | Covers toasts too. |

## Example

```csharp
var shell = NiddyApp.Current!.Shell;
shell.Pages.NavigateTo(HomePage.PageId, addToBackStack: false);
Toast.Show(shell, "Saved");                         // any control inside the shell works as the source
await Dialog.Notification.Open(shell, "Hello", "From outside a page.");
```

## See also

- [NiddyApp](NiddyApp.md)
