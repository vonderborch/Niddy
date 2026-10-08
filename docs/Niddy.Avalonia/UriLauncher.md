# UriLauncher

`Niddy.Avalonia` · Niddy.Avalonia · [source](../../src/Niddy.Avalonia/UriLauncher.cs)

Opens a URI with the platform's default handler, usually the browser. It goes through Avalonia's `TopLevel.Launcher`, so it works on desktop, mobile and browser, unlike `Process.Start`, which is desktop-only.

## API

| Member | Description |
|---|---|
| `static Task<bool> TryLaunchAsync(Control source, string uri)` | False if the URI isn't absolute, `source` isn't attached to a visual tree, or the platform couldn't open it. Never throws; a platform error is logged as a warning to `NiddyAppOptions.LoggerFactory` (or `Trace`). |

## Example

```csharp
using Niddy.Avalonia;
using Niddy.Avalonia.Toasts;

if (!await UriLauncher.TryLaunchAsync(this, "https://github.com/vonderborch/Niddy"))
    Toast.Show(this, "Couldn't open the browser", ToastType.Warning);

await UriLauncher.TryLaunchAsync(this, "mailto:support@example.com?subject=Help");
```

## See also

- [Dialog.Web](Dialogs/Dialog.md), for showing a page inside the app
