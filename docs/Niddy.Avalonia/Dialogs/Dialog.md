# Dialog

`Niddy.Avalonia.Dialogs` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Dialogs/Dialog.cs) · [TableColumn](../../../src/Niddy.Avalonia/Dialogs/TableDialog.axaml.cs)

Shows dialogs: eleven built-in kinds, and your own through `Dialog.Show`. Each dialog is a single control. It's shown either as an overlay card over the nearest [`DialogOverlayHost`](DialogOverlayHost.md) or in a modal window, chosen by `DefaultMode` or a per-call `displayMode`. Every method is `async` and takes `parent`, a control in the visual tree the dialog belongs to. From a page or control, that's `this`.

**Dismissing** a dialog without a button (Escape, the window's close button, or the platform back request) acts like its cancel button. It's ignored if the cancel button is hidden.

## Display mode

| `DialogDisplayMode` | Behavior |
|---|---|
| `Auto` | Overlay if a `DialogOverlayHost` is found, else a window on desktop. On mobile and browser, throws `InvalidOperationException` without a host. |
| `Window` | A modal window. On mobile and browser, falls back to an overlay (and throws without a host). |
| `Overlay` | Always an overlay. Throws without a host. |

`Dialog.DefaultMode` defaults to `Auto`. [`NiddyApp`](../Hosting/NiddyApp.md) sets it from `NiddyAppOptions.DialogMode`: windows on desktop and overlays on mobile.

Every method also takes these optional parameters:

| Parameter | Description |
|---|---|
| `maxWidth` | The overlay card's maximum width, and the window's width unless `windowWidth` is set. Defaults to 500 (600 for Exception, 520 for Color, 640 for Markdown, 800 for Table, 900 for Web). |
| `displayMode` | Overrides `DefaultMode` for this call. |
| `windowWidth`, `windowHeight` | A fixed window size. By default the window fits its content. Ignored in overlays. |

## Built-in dialogs

| Method | Returns | Notes |
|---|---|---|
| `Notification.Open(parent, title, description, okButtonText = "OK")` | `Task` | A single OK button. |
| `Confirmation.Open(parent, title, description, defaultResult = false, yesButtonText = "Yes", showYesButton = true, noButtonText = "No", showNoButton = true)` | `Task<bool>` | True for Yes. Dismissing returns `defaultResult`. |
| `Warning.Open(…same…, yesButtonText = "Proceed", noButtonText = "Cancel")` | `Task<bool>` | For destructive actions. The title is orange and **Enter doesn't confirm**. |
| `Input.Open(parent, title, description, defaultValue = "", placeholderText = "", okButtonText = "OK", cancelButtonText = "Cancel", showCancelButton = true)` | `Task<string?>` | Null if cancelled. |
| `MultiInput.Open(parent, title, description, IEnumerable<(string Label, string DefaultValue)> fields, …)` | `Task<Dictionary<string,string>?>` | Keyed by label (a duplicate label: the last one wins). Null if cancelled. |
| `Selection.Open<T>(parent, title, description, items, Func<T,string> displaySelector, …)` | `Task<T?>` | Default if cancelled or nothing selected. |
| `Progress.Open(parent, title, description, Func<IProgress<double>, CancellationToken, Task> work, isIndeterminate = false, showCancelButton = true, cancelButtonText = "Cancel")` | `Task<bool>` | Runs `work` **on a background thread**; don't touch UI from it. Progress is **0–100**. True if the work completed, false if it was cancelled or faulted. Returns once the work has finished. |
| `Exception.Open(parent, title, description, exception, reportIssueLink, …, showReportIssueButton = true, showIgnoreAndContinueButton = true, bool? showExitProgramButton = false, copyDetailsText = "Copy Details")` | `Task` | Details with Copy Details, Report Issue, Ignore and Continue, and Exit Program. `showExitProgramButton: null` shows it on desktop only; it's never shown on mobile or browser. An empty `reportIssueLink` hides Report Issue. |
| `Color.Open(parent, title, initialColor = null, description = "", showAlpha = true, palette = null, okButtonText, cancelButtonText)` | `Task<Color?>` | Spectrum, palette and RGB/HSV/hex inputs. `initialColor` defaults to white, the palette to Fluent. Null if cancelled. |
| `Markdown.Open(parent, title, markdown, okButtonText = "OK", cancelButtonText = null)` | `Task<bool>` | Scrollable, selectable Markdown (Markdown.Avalonia). With a cancel text (e.g. "Decline"), returns false on cancel. |
| `Table.Open<T>(parent, title, description, items, columns = null, okButtonText = "Close")` | `Task` | A read-only, sortable table (DataGrid). By default there's one column per public property. |
| `Table.Pick<T>(…, okButtonText = "OK", cancelButtonText = "Cancel")` | `Task<T?>` | Pick a row. Double-clicking a row picks it. |
| `Web.Open(parent, title, Uri address, Func<Uri,bool>? closeWhen = null, height = 560, closeButtonText = "Close", openInBrowserText = "Open in Browser")` | `Task<Uri?>` | The platform web view (WebView2, WKWebView, WebKitGTK, Android WebView). `closeWhen` cancels the matching navigation, closes the dialog and returns that address (e.g. an OAuth redirect). Otherwise it returns the address shown at close. `openInBrowserText: null` hides that button. |
| `Web.OpenHtml(parent, title, string html, Uri? baseAddress = null, closeWhen = null, …)` | `Task<Uri?>` | The followed link that matched `closeWhen`, or null. |
| `Show<TResult>(parent, DialogBase<TResult> dialog, …)` | `Task<TResult>` | A custom dialog. See [DialogBase](DialogBase.md). Use a new instance for each call. |

`Dialog.Web` works best as a window, because an overlay can't draw over a native web view. Where there's no web view, the dialog says so instead of failing.

### `TableColumn<T>`

`record TableColumn<T>(string Header, Func<T, object?> Value)` with `Func<object?, string>? Format { init; }`. The value is used for sorting, and `Format` (default `ToString`) for display.

## Examples

```csharp
using Niddy.Avalonia.Dialogs;

await Dialog.Notification.Open(this, "Done", "Export finished.");

if (!await Dialog.Warning.Open(this, "Delete project?", "This can't be undone.", yesButtonText: "Delete"))
    return;

string? name = await Dialog.Input.Open(this, "Rename", "New name:", defaultValue: project.Name);

var values = await Dialog.MultiInput.Open(this, "New user", "", [("Name", ""), ("Email", "")]);
if (values is not null) CreateUser(values["Name"], values["Email"]);

Theme? theme = await Dialog.Selection.Open(this, "Theme", "Pick one:", themes, t => t.Name);

bool finished = await Dialog.Progress.Open(this, "Importing…", "This can take a minute.",
    async (progress, ct) =>
    {
        for (var i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await ImportAsync(files[i], ct);
            progress.Report(100.0 * (i + 1) / files.Count);   // 0–100
        }
    });

Color? accent = await Dialog.Color.Open(this, "Accent color", initialColor: Colors.SteelBlue, showAlpha: false);

bool accepted = await Dialog.Markdown.Open(this, "License", licenseText, "Accept", cancelButtonText: "Decline");

Download? pick = await Dialog.Table.Pick(this, "Pick a file", "", downloads,
    [new("Name", d => d.Name), new("Size", d => d.Size) { Format = v => Humanize.Bytes((long)v!) }]);

Uri? redirect = await Dialog.Web.Open(this, "Sign in", authorizeUri,
    closeWhen: uri => uri.AbsoluteUri.StartsWith("myapp://callback"),
    displayMode: DialogDisplayMode.Window);

try { Risky(); }
catch (Exception ex)
{
    await Dialog.Exception.Open(this, "Import failed", "The file couldn't be read.", ex,
        reportIssueLink: "https://github.com/me/app/issues/new", showExitProgramButton: null);
}
```

A fixed-size window, whatever the default mode:

```csharp
await Dialog.Notification.Open(this, "Licence", longText,
    displayMode: DialogDisplayMode.Window, windowWidth: 600, windowHeight: 400);
```

## See also

- [DialogBase](DialogBase.md), [DialogLayout](DialogLayout.md), [DialogOverlayHost](DialogOverlayHost.md)
- [examples/custom-dialog](../../examples/custom-dialog.md), [examples/oauth-sign-in](../../examples/oauth-sign-in.md), [examples/long-running-work](../../examples/long-running-work.md)
