# UrlHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/UrlHelpers.cs)

Opens a URL in the default browser on desktop (Windows, macOS, Linux) through the shell.

## API

| Member | Description |
|---|---|
| `static void OpenUrl(string url, string errorMessage = "Failed to open URL.")` | Starts the platform's URL handler with `Process.Start`; writes `errorMessage` to `Console.Error` instead of throwing if it can't. |

## Example

```csharp
using Niddy.Helpers;

UrlHelpers.OpenUrl("https://github.com/vonderborch/Niddy");
```

In an Avalonia app use [`UriLauncher.TryLaunchAsync`](../../Niddy.Avalonia/Utilities/UriLauncher.md) instead: it also works on mobile and in the browser.
