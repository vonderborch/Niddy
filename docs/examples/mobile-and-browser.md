# Mobile and browser

One `NiddyApp` runs on desktop, Android, iOS and the browser. This page covers what changes between them and how to write pages that work everywhere. It uses [NiddyApp / AppMode](../Niddy.Avalonia/Hosting/NiddyApp.md#appmode), [AppShell](../Niddy.Avalonia/Hosting/AppShell.md), [PageMenu](../Niddy.Avalonia/PageSystem/PageMenu.md), [PageView](../Niddy.Avalonia/PageSystem/PageView.md), [Dialog](../Niddy.Avalonia/Dialogs/Dialog.md), [ToastHost](../Niddy.Avalonia/Toast/ToastHost.md), [FilePicker](../Niddy.Avalonia/FilePicker/FilePicker.md), [Responsive](../Niddy.Avalonia/Layout/Responsive.md) and [UriLauncher](../Niddy.Avalonia/Utilities/UriLauncher.md).

## What Niddy does for you

| | Desktop | Mobile (Android, iOS, browser) |
|---|---|---|
| Root | A main window holding the [`AppShell`](../Niddy.Avalonia/Hosting/AppShell.md), which remembers its size and position | The `AppShell` as the main view |
| Dialogs (`DialogMode = Auto`) | Their own windows | Overlays in the shell's `DialogOverlayHost` |
| Toasts | `ToastPlacement` (bottom right) | `ToastNarrowPlacement` (bottom center) |
| `PageMenu` | `Style` / `Placement` | `NarrowStyle` (a bottom navigation bar) |
| Back | Your own buttons | Android back, the iOS back gesture and browser back go back a page. An open overlay dialog is dismissed first. |

`AppMode.Auto` picks mobile for single-view lifetimes and for Android, iOS and the browser. Everything in the Mobile column also applies to a desktop app started with `options.Mode = AppMode.Mobile`.

## Preview the phone layout on desktop

```csharp
protected override void Configure(NiddyAppOptions options)
{
    options.StartingPage = HomePage.PageId;
    options.Layout = pages => new PageMenu { Content = pages };
#if DEBUG
    if (Environment.GetEnvironmentVariable("MYAPP_MOBILE") == "1")
        options.Mode = AppMode.Mobile;   // a 390 × 844 window (MobilePreviewWidth/Height) with mobile behavior
#endif
}
```

## Pages that work everywhere

**Files: use handles, not paths.** On mobile and in the browser, picked files usually have no local path, so the `*Path` methods return null there.

```csharp
using var file = await FilePicker.OpenFile(this, "Open", [FilePickerFileTypes.ImageAll]);
if (file is null) return;
await using var stream = await file.OpenReadAsync();
Photo = new Bitmap(stream);
```

**Links: `UriLauncher`, not `Process.Start`.** `Process.Start` is desktop-only.

```csharp
if (!await UriLauncher.TryLaunchAsync(this, "https://example.com/help"))
    Toast.Show(this, "Couldn't open the browser", ToastType.Error);
```

**Layout: react to the control's width.** A phone, a narrow desktop window and a split-screen tablet all look the same to [`Responsive`](../Niddy.Avalonia/Layout/Responsive.md).

```xml
<DockPanel niddy:Responsive.NarrowBelow="700">
    <DockPanel.Styles>
        <Style Selector="DockPanel.narrow > Border#Details">
            <Setter Property="IsVisible" Value="False" />
        </Style>
    </DockPanel.Styles>
    <Border x:Name="Details" DockPanel.Dock="Right" Width="280"> … </Border>
    <ListBox ItemsSource="{Binding Items}" />
</DockPanel>
```

**Mode-specific behavior: `Page.AppMode`.**

```csharp
private void Items_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
{
    if (e.AddedItems is not [Item item, ..])
        return;

    // On a phone, show details as a page of their own (back returns here);
    // on desktop they're in the side panel, bound to the selection
    if (AppMode == AppMode.Mobile)
        Push(new ItemDetailsPage(item));
}
```

**Dialogs: always give them a control in the shell.** On mobile, a dialog needs a `DialogOverlayHost` above its parent control; there are no windows. `NiddyApp` provides one. Pass the page (or any control on it) as `parent`. If you host pages yourself, see [DialogOverlayHost](../Niddy.Avalonia/Dialogs/DialogOverlayHost.md).

## Things that don't exist off desktop

| API | Off desktop |
|---|---|
| `FilePicker.*Path` | Returns null or skips items. Use the handle methods. |
| `WindowMemory`, `NiddyAppOptions.Width/Height/ConfigureWindow` | Ignored: there's no window. |
| `WindowNotification.RequestAttention` | Does nothing. |
| `DialogDisplayMode.Window` | Falls back to an overlay (there's nothing to own a window). |
| `Dialog.Web` | Shows a message where the platform has no web view. |
| `SecureStorageFactory.Create` | Falls back to [`EncryptedFileSecureStorage`](../Niddy.Core/Security/EncryptedFileSecureStorage.md) on iOS, Android and the browser. |

## See also

- [app-setup](app-setup.md), [custom-dialog](custom-dialog.md)
