# UrlLauncher

`Niddy.Core` · Niddy.Core · [source](../../src/Niddy.Core/UrlLauncher.cs)

Opens a URL with the platform's default handler, usually the web browser, without needing a UI framework.

## API

| Member | Description |
|---|---|
| `static void Open(string url)` | Opens an absolute URL (`https:`, `mailto:`, `file:`, a custom scheme…). Returns once the request is made. |

| Platform | How |
|---|---|
| Windows, macOS, Linux, FreeBSD | The shell: `ShellExecute`, `open`, `xdg-open`. |
| iOS, iPadOS, tvOS, Mac Catalyst | `UIApplication.openURL`, called on the main thread for you. Custom schemes must be listed under `LSApplicationQueriesSchemes` in Info.plist. |
| Browser | `window.open` in a new tab (`noopener`). Browsers block it as a pop-up unless it runs while handling a click or key press. |
| Android, watchOS, WASI | Throws `PlatformNotSupportedException`: opening a URL there needs the app's UI framework. |

It throws `ArgumentException` if `url` isn't an absolute URL. Plain file paths (`/usr/bin/x`, `C:\app.exe`) are rejected too; write files as `file:` URLs. The platform comes from [`Platform.Info`](SystemInfo/Platform.md), so on an OS the app didn't declare it throws `UnsupportedOperatingSystemException`.

Whatever handler is registered for the URL's scheme runs. Check the scheme of URLs you didn't write yourself, e.g. allow only `http` and `https` for links from a document.

## Example

```csharp
using Niddy.Core;

UrlLauncher.Open("https://github.com/vonderborch/Niddy");
```

In an Avalonia app, prefer [`UriLauncher.TryLaunchAsync`](../Niddy.Avalonia/UriLauncher.md): it goes through Avalonia's launcher, so it also works on Android, and it returns false instead of throwing.
