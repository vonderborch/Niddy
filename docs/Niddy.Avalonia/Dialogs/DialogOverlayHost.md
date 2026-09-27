# DialogOverlayHost

`Niddy.Avalonia.Dialogs` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Dialogs/DialogOverlayHost.axaml.cs)

Hosts content and shows overlay dialogs on top of it. Overlay dialogs work on every platform, and they're the only kind on mobile and browser. [`NiddyApp`](../Hosting/NiddyApp.md) already puts one at the root of the [`AppShell`](../Hosting/AppShell.md). Without `NiddyApp`, wrap your window's or main view's content in one.

- Dialogs stay clear of safe-area insets (notch, status and navigation bars) and the on-screen keyboard.
- The platform back request dismisses the open dialog, as its cancel button would.
- A dialog opened while another is showing stacks on top of it.

## API

| Member | Description |
|---|---|
| `object? Content` | The main content under the overlay. |
| `static DialogOverlayHost? FindInVisualTree(Control control)` | Walks up the logical parents to the nearest host. |

## Example

Put the content between the host's XAML tags, as in [PageView's example](../PageSystem/PageView.md#examples), or set `Content` in code. For a single-view (mobile/browser) app without `NiddyApp`:

```csharp
public override void OnFrameworkInitializationCompleted()
{
    if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
    {
        singleView.MainView = new DialogOverlayHost
        {
            Content = new ToastHost
            {
                Content = new PageView { StartingPage = typeof(HomePage) },
            },
        };
    }
    base.OnFrameworkInitializationCompleted();
}
```

```csharp
bool canOverlay = DialogOverlayHost.FindInVisualTree(this) is not null;
```

## See also

- [Dialog](Dialog.md), [ToastHost](../Toast/ToastHost.md), [examples/mobile-and-browser](../../examples/mobile-and-browser.md)
