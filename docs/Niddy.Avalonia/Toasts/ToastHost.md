# ToastHost

`Niddy.Avalonia.Toasts` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Toasts/ToastHost.axaml.cs)

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
| `static ToastHost? FindInVisualTree(Control control)` | The nearest host: `control` itself, then its logical parents, then its visual ancestors. Null if none. (`Toast.Show` also falls back to the `NiddyApp` shell's host.) |

The newest toast is nearest the edge it appears at.

### ToastItem

[`ToastItem`](../../../src/Niddy.Avalonia/Toasts/ToastItem.axaml.cs) is the `UserControl` for one toast inside the host. It's public only because Avalonia's XAML compiler needs it to be. Its constructor and members are internal, so you can't create or change one directly. Use [Toast](Toast.md) and [ToastHandle](ToastHandle.md) instead. It closes itself when its duration runs out, which pauses while the pointer is over it. Clicking it also closes it, unless it's showing progress.

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
