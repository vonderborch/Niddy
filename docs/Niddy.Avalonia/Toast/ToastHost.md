# ToastHost

`Niddy.Avalonia.Toast` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Toast/ToastHost.axaml.cs)

Hosts content and shows toasts on top of it, at any of the eight [`ScreenPlacement`](../ScreenPlacement.md)s. Toasts stay clear of safe-area insets and the on-screen keyboard. On narrow screens and in mobile mode they move to `NarrowPlacement`. [`NiddyApp`](../Hosting/NiddyApp.md)'s shell includes one. Without `NiddyApp`, wrap your content in one.

## API

| Member | Description |
|---|---|
| `object? Content` | The content under the toasts. |
| `ScreenPlacement Placement` | Where toasts go on wide screens. Default `BottomRight`. |
| `ScreenPlacement? NarrowPlacement` | Where they go in mobile mode or when the host is narrower than `NarrowWidth`. Default `BottomCenter`. Null always uses `Placement`. |
| `double NarrowWidth` | Default 600. |
| `ScreenPlacement ActualPlacement` | Read-only: the placement in use right now. |
| `bool MergeDuplicates` | Default true. See [Toast](Toast.md). |
| `static ToastHost? FindInVisualTree(Control control)` | The nearest host above `control`, walking logical parents. |

The newest toast is nearest the edge it appears at.

## Example

With `NiddyApp`, set `ToastPlacement` and `ToastNarrowPlacement` in `NiddyAppOptions`. The shell's host is also `AppShell.Toasts`.

Without `NiddyApp`, wrap your content in XAML (the child becomes the host's `Content`):

```xml
<Window xmlns:niddy="https://github.com/vonderborch/Niddy" ...>
    <niddy:DialogOverlayHost>
        <niddy:ToastHost Placement="TopRight" NarrowPlacement="TopCenter" MergeDuplicates="False">
            <local:MainView />
        </niddy:ToastHost>
    </niddy:DialogOverlayHost>
</Window>
```

Or in code:

```csharp
public MainWindow()
{
    InitializeComponent();
    Content = new DialogOverlayHost
    {
        Content = new ToastHost
        {
            Placement = ScreenPlacement.TopRight,
            NarrowPlacement = ScreenPlacement.TopCenter,
            MergeDuplicates = false,
            Content = new MainView(),
        },
    };
}
```

## See also

- [Toast](Toast.md), [DialogOverlayHost](../Dialogs/DialogOverlayHost.md)
