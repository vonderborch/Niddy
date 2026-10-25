# Dialog

`Niddy.Avalonia.Dialogs` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Dialogs/Dialog.cs) · [TableColumn](../../../src/Niddy.Avalonia/Dialogs/TableColumn.cs) · [ViewFit](../../../src/Niddy.Avalonia/Dialogs/ViewFit.cs)

Shows dialogs: twelve built-in kinds, your own control or window through `Dialog.View`, and your own dialog class through `Dialog.Show`. Each dialog is a single control. It's shown either as an overlay card over the nearest [`DialogOverlayHost`](DialogOverlayHost.md) or in a modal window, chosen by `DefaultMode` or a per-call `displayMode`. Every method is `async` and takes `parent`, a control in the visual tree the dialog belongs to. From a page or control, that's `this`.

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
| `maxWidth` | The overlay card's maximum width, and the window's width unless `windowWidth` is set. Defaults to 500 (600 for Exception, 520 for Color, 640 for Markdown, 800 for Table, 900 for Web; for `View.Open` with a window, the window's `Width` plus the dialog's 40 of padding). |
| `displayMode` | Overrides `DefaultMode` for this call. |
| `windowWidth`, `windowHeight` | A fixed window size. By default the window fits its content. Ignored in overlays. |

### Content height

The dialogs with a scrolling area also take `double? contentHeight`, the last parameter. It gives that area a fixed height, in overlays and windows alike. By default the area fits its content up to a cap, then scrolls:

| Dialog | Scrolling area | Default cap |
|---|---|---|
| `Markdown.Open` | The Markdown view | 420 |
| `Table.Open`, `Table.Pick` | The table | 400 |
| `MultiInput.Open` | The fields | 300 |
| `Exception.Open` | The exception details | 240 |
| `Selection.Open` | The list | 200 |
| `Web.Open`, `Web.OpenHtml` | The web view | Always 560 (a web page has no natural height) |
| `View.Open` | Your view | Depends on `fit`: no cap with the default `Stretch` and with `Center` (the view takes the height it wants, and the dialog scrolls); 400 with `Scroll`, `Shrink` and `Scale`. A window's `Height` is the default. Set it for a `DataGrid` or anything else that scrolls inside itself, or use `fill`. |

- **A fixed height**, not a new cap: short content leaves the rest of the area empty.
- **It isn't `windowHeight`.** That sizes the window, not the area inside it, so a taller window alone doesn't show more Markdown. Set `contentHeight` instead and let the window fit it.
- **When the dialog doesn't fit** (a small screen, or a window smaller than the content), the whole dialog scrolls, as it already does.
- **It must be positive and finite**, or the call throws `ArgumentOutOfRangeException`.
- The other built-in dialogs have nothing that scrolls, so they don't take it. In a [custom dialog](DialogBase.md), set `Height` or `MaxHeight` on your own content.

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
| `Web.Open(parent, title, Uri address, Func<Uri,bool>? closeWhen = null, closeButtonText = "Close", openInBrowserText = "Open in Browser")` | `Task<Uri?>` | The platform web view (WebView2, WKWebView, WebKitGTK, Android WebView). `closeWhen` cancels the matching navigation, closes the dialog and returns that address (e.g. an OAuth redirect). Otherwise it returns the address shown at close. `openInBrowserText: null` hides that button. |
| `Web.OpenHtml(parent, title, string html, Uri? baseAddress = null, closeWhen = null, …)` | `Task<Uri?>` | The followed link that matched `closeWhen`, or null. |
| `View.Open(parent, Control view, title = null, description = null, okButtonText = "OK", cancelButtonText = "Cancel", Func<bool>? canAccept = null, double? maxWidth = null, …, ViewFit fit = ViewFit.Stretch, bool fill = false)` | `Task<bool>` | Your own control, or a `Window`'s content, as a dialog. True for OK. See [Your own view](#your-own-view). |
| `Show<TResult>(parent, DialogBase<TResult> dialog, …)` | `Task<TResult>` | A custom dialog. See [DialogBase](DialogBase.md). Use a new instance for each call. |

`Dialog.Web` works best as a window, because an overlay can't draw over a native web view. Where there's no web view, the dialog says so instead of failing.

### Your own view

`View.Open` shows any control as a dialog, under the standard title and description, with OK and Cancel buttons. It's the quick way to put an existing view or form in a dialog without writing a [`DialogBase`](DialogBase.md) class.

- **A control** mustn't already be in the visual tree. It's taken out of the dialog when it closes, so the same instance can be shown again. Its `DataContext` is inherited from where the dialog shows unless you set one.
- **A `Window`** isn't opened. Its `Content` moves into the dialog, with its `DataContext` and `ContentTemplate`, and goes back to the window when the dialog closes. Its `Title` is the default `title`, its `Width` sets the default `maxWidth`, and its `Height` is the default `contentHeight`. The window mustn't be open (`InvalidOperationException`). Its own `Styles` and `Resources` stay on the window, so put anything the content needs on the content's root instead.
- **Buttons:** `okButtonText: null` or `cancelButtonText: null` hides that button. Without Cancel, Escape, back and the window's close button do nothing, as with every dialog.
- **`canAccept`** runs when OK is clicked; returning false keeps the dialog open, e.g. while a field is invalid.
- **`fit`** says how the view fits its area, the space between the description and the buttons. See [`ViewFit`](#viewfit).
- **`fill: true`** makes the dialog as big as it can be, and gives the view all the height between the description and the buttons, with the buttons at the bottom:
  - in an overlay, the card fills the [`DialogOverlayHost`](DialogOverlayHost.md), less its margin, and doesn't scroll. `maxWidth` defaults to no limit, so set it to keep the card narrower;
  - in a window, the window is resizable and the view resizes with it. It starts at 80% of its owner's size, unless `windowWidth` (or `maxWidth`) and `windowHeight` say otherwise;
  - a `Window` view's `Width` and `Height` aren't used;
  - `contentHeight` can't be combined with it (`ArgumentException`).

  Use it for a `DataGrid` or `TableGrid`, an editor or a map: anything that should use the room it's given rather than its natural size.
- **`View.Close(control, accepted = true)`** closes the dialog from inside the view (e.g. a double-click, or the view's own buttons) and makes `Open` return `accepted`. It skips `canAccept`, and returns false if the control isn't in a `View.Open` dialog.
- The result is just OK or not. Read anything else from the view or its view model after `Open` returns.

### `ViewFit`

`enum ViewFit`: how `View.Open` fits a view into its area. The area is as wide as the dialog. Its height is `contentHeight` when given, everything left over with `fill`, and otherwise as below.

| Value | The view | Height without `contentHeight` or `fill` |
|---|---|---|
| `Stretch` (default) | Is laid out like any control: it gets the area's width, and its own `HorizontalAlignment` and `VerticalAlignment` decide whether it stretches across it (by default it does). | As tall as the view wants; the whole dialog scrolls if it's too tall. |
| `Center` | Keeps the size it wants, centered in the area. A view bigger than the area is limited to it, as with `Stretch`. | As tall as the view wants. |
| `Scroll` | Keeps the size it wants, and scrolls inside the area, both ways, when it's bigger. The title, description and buttons stay in place. | Fits the view, up to 400. |
| `Shrink` | Keeps the size it wants, but is scaled down, keeping its proportions, when it's bigger than the area. A smaller view isn't scaled up. | At most 400. |
| `Scale` | Is scaled up or down, keeping its proportions, to fit the area, e.g. a chart or drawing with a fixed size. | At most 400. |

`Shrink` and `Scale` scale the view as a picture: it's laid out at the size it wants, then drawn smaller or bigger, text included.

### `TableColumn<T>`

`record TableColumn<T>(string Header, Func<T, object?> Value)`: a column of `Dialog.Table`. `Value` gets a row's value, which is used for sorting.

| Property | Default | |
|---|---|---|
| `Func<object?, string>? Format { init; }` | `ToString` | Formats the value for display. |
| `bool AlignRight { init; }` | false | Right-aligns the column, e.g. for numbers. |
| `DataGridLength? Width { init; }` | fits the content | The column width, e.g. `new DataGridLength(1, DataGridLengthUnitType.Star)` (`using Avalonia.Controls;`). |

## Examples

```csharp
using Niddy.Avalonia.Dialogs;
using Niddy.Core.Text;   // Humanize

await Dialog.Notification.Open(this, "Done", "Export finished.");

if (!await Dialog.Warning.Open(this, "Delete project?", "This can't be undone.", yesButtonText: "Delete"))
    return;

string? name = await Dialog.Input.Open(this, "Rename", "New name:", defaultValue: project.Name);

Dictionary<string, string>? values = await Dialog.MultiInput.Open(this, "New user", "", [("Name", ""), ("Email", "")]);
if (values is not null) CreateUser(values["Name"], values["Email"]);

Theme? theme = await Dialog.Selection.Open(this, "Theme", "Pick one:", themes, t => t.Name);

bool finished = await Dialog.Progress.Open(this, "Importing…", "This can take a minute.",
    async (progress, ct) =>
    {
        for (int i = 0; i < files.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            await ImportAsync(files[i], ct);
            progress.Report(100.0 * (i + 1) / files.Count);   // 0–100
        }
    });

Color? accent = await Dialog.Color.Open(this, "Accent color", initialColor: Colors.SteelBlue, showAlpha: false);

bool accepted = await Dialog.Markdown.Open(this, "License", licenseText, "Accept", cancelButtonText: "Decline");

await Dialog.Markdown.Open(this, "What's new", releaseNotes, contentHeight: 600);   // a taller view than the default 420

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

Your own view, or a window you already have:

```csharp
SettingsView view = new() { DataContext = settings };
if (await Dialog.View.Open(this, view, "Settings", okButtonText: "Save", canAccept: () => settings.IsValid))
    await settings.SaveAsync();

// The window isn't opened: its content shows in the dialog, with its title, size and DataContext.
bool accepted = await Dialog.View.Open(this, new ConnectionWindow(), description: "Where's the server?");

// A DataGrid scrolls inside itself, so give it a height...
await Dialog.View.Open(this, new DataGrid { ItemsSource = rows, AutoGenerateColumns = true },
    "Rows", cancelButtonText: null, maxWidth: 900, contentHeight: 400);

// ...or let it fill the overlay, or a resizable window.
await Dialog.View.Open(this, new TableGrid { ItemsSource = rows, AutoGenerateColumns = true },
    "Rows", cancelButtonText: null, fill: true);

// A big image scrolls both ways; a fixed-size diagram is scaled down to fit.
await Dialog.View.Open(this, new Image { Source = scan, Stretch = Stretch.None }, "Scan", fit: ViewFit.Scroll, contentHeight: 600);
await Dialog.View.Open(this, new DiagramView { Width = 1600, Height = 900 }, "Diagram", fit: ViewFit.Shrink, fill: true);

// A small view keeps its size, centered.
await Dialog.View.Open(this, new ColorSwatch { Width = 120, Height = 120 }, "Accent", fit: ViewFit.Center);
```

From inside the view, e.g. picking a row by double-clicking it:

```csharp
private void Grid_OnDoubleTapped(object? sender, TappedEventArgs e) => Dialog.View.Close(this);
```

A fixed-size window, whatever the default mode:

```csharp
await Dialog.Notification.Open(this, "Licence", longText,
    displayMode: DialogDisplayMode.Window, windowWidth: 600, windowHeight: 400);
```

## See also

- [DialogBase](DialogBase.md), [DialogLayout](DialogLayout.md), [DialogOverlayHost](DialogOverlayHost.md)
- [examples/custom-dialog](../../examples/custom-dialog.md), [examples/oauth-sign-in](../../examples/oauth-sign-in.md), [examples/long-running-work](../../examples/long-running-work.md)
