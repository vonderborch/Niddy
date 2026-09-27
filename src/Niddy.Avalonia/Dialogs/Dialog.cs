using Avalonia.Controls;

namespace Niddy.Avalonia.Dialogs;

/// <summary>How a dialog should be presented to the user.</summary>
public enum DialogDisplayMode
{
    /// <summary>
    ///     Use an overlay if a <see cref="DialogOverlayHost"/> is found in the visual tree; otherwise
    ///     open a new Window on desktop. On platforms without windows (mobile, browser), throws
    ///     <see cref="InvalidOperationException"/> if there is no <see cref="DialogOverlayHost"/>.
    /// </summary>
    Auto,

    /// <summary>
    ///     Open the dialog in a new Window. On platforms without windows (mobile, browser), falls back
    ///     to an overlay, and throws <see cref="InvalidOperationException"/> if there is no
    ///     <see cref="DialogOverlayHost"/>.
    /// </summary>
    Window,

    /// <summary>
    ///     Always use an overlay. Throws <see cref="InvalidOperationException"/> if no
    ///     <see cref="DialogOverlayHost"/> is found in the visual tree.
    /// </summary>
    Overlay,
}

/// <summary>
///     Shows dialogs. Each dialog is a single control that is shown either in an overlay card over the
///     nearest <see cref="DialogOverlayHost"/> or in a modal window, chosen by <see cref="DefaultMode"/>
///     (or a per-call <c>displayMode</c> override).
///     <para>
///         Dismissing a dialog without a button (Escape, the window's close button, or the platform
///         back request) acts like its cancel button, and is ignored if the cancel button is hidden.
///     </para>
/// </summary>
public static class Dialog
{
    /// <summary>
    ///     The default display mode applied to all calls that do not specify a
    ///     <c>displayMode</c> override. Defaults to <see cref="DialogDisplayMode.Auto"/>.
    /// </summary>
    public static DialogDisplayMode DefaultMode { get; set; } = DialogDisplayMode.Auto;

    /// <summary>
    ///     Shows a custom dialog and returns its result. Derive the dialog from <see cref="DialogBase{TResult}"/>.
    /// </summary>
    /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
    /// <param name="dialog">The dialog to show. Create a new instance for each call.</param>
    /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
    /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
    /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
    /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
    public static Task<TResult> Show<TResult>(
        Control parent,
        DialogBase<TResult> dialog,
        double maxWidth = 500,
        DialogDisplayMode? displayMode = null,
        double? windowWidth = null,
        double? windowHeight = null
    ) => ShowAsync(parent, dialog, maxWidth, ResolveMode(displayMode, parent), windowWidth, windowHeight);

    private static async Task<TResult> ShowAsync<TResult>(
        Control parent,
        DialogBase<TResult> dialog,
        double maxWidth,
        DialogDisplayMode resolvedMode,
        double? windowWidth,
        double? windowHeight
    )
    {
        if (resolvedMode == DialogDisplayMode.Overlay)
            await DialogOverlayHost.FindInVisualTree(parent)!.ShowAsync(dialog, dialog, maxWidth);
        else
            await DialogWindow.ShowAsync((Window)TopLevel.GetTopLevel(parent)!, dialog, dialog, windowWidth ?? maxWidth, windowHeight);

        return await dialog.Result;
    }

    /// <summary>Resolves the requested mode to Overlay or Window, or throws if neither is possible.</summary>
    private static DialogDisplayMode ResolveMode(DialogDisplayMode? requested, Control parent)
    {
        var mode = requested ?? DefaultMode;
        var hasOverlayHost = DialogOverlayHost.FindInVisualTree(parent) is not null;

        // Mobile and browser apps have a single view and no Window to own a dialog window.
        var canOpenWindows = TopLevel.GetTopLevel(parent) is global::Avalonia.Controls.Window;

        return mode switch
        {
            DialogDisplayMode.Overlay when hasOverlayHost => DialogDisplayMode.Overlay,
            DialogDisplayMode.Overlay => throw MissingOverlayHost(),
            _ when mode == DialogDisplayMode.Auto && hasOverlayHost => DialogDisplayMode.Overlay,
            _ when canOpenWindows => DialogDisplayMode.Window,
            _ when hasOverlayHost => DialogDisplayMode.Overlay,
            _ => throw MissingOverlayHost(
                " This platform can't open dialog windows (mobile and browser apps have a single view), so overlay dialogs are required."
            ),
        };
    }

    private static InvalidOperationException MissingOverlayHost(string detail = "") =>
        new($"No DialogOverlayHost found above the parent control.{detail} Wrap your main view's content in a DialogOverlayHost.");

    // ──────────────────────────────────────────────────────────────────────────────
    //  Notification
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Informational dialogs with a single OK button.</summary>
    public static class Notification
    {
        /// <summary>Shows an informational dialog with a single OK button.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        public static Task Open(
            Control parent,
            string title,
            string description,
            string okButtonText = "OK",
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(parent, new NotificationDialog(title, description, okButtonText), maxWidth, displayMode, windowWidth, windowHeight);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Confirmation
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Yes/No confirmation dialogs.</summary>
    public static class Confirmation
    {
        /// <summary>Shows a Yes/No confirmation dialog.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="defaultResult">The result if the dialog is dismissed without clicking a button.</param>
        /// <param name="yesButtonText">The text for the Yes button.</param>
        /// <param name="showYesButton">Whether to show the Yes button.</param>
        /// <param name="noButtonText">The text for the No button.</param>
        /// <param name="showNoButton">Whether to show the No button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>True if Yes was clicked, false if No was clicked.</returns>
        public static Task<bool> Open(
            Control parent,
            string title,
            string description,
            bool defaultResult = false,
            string yesButtonText = "Yes",
            bool showYesButton = true,
            string noButtonText = "No",
            bool showNoButton = true,
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            new ConfirmationDialog(title, description, defaultResult, yesButtonText, showYesButton, noButtonText, showNoButton, isWarning: false),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Warning
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Warning dialogs for potentially destructive or irreversible actions.
    ///     The title renders in orange to draw attention.
    /// </summary>
    public static class Warning
    {
        /// <summary>
        ///     Shows a confirmation dialog for a potentially destructive or irreversible action. The title
        ///     renders in orange, and Enter doesn't confirm.
        /// </summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="defaultResult">The result if the dialog is dismissed without clicking a button.</param>
        /// <param name="yesButtonText">The text for the Proceed button.</param>
        /// <param name="showYesButton">Whether to show the Proceed button.</param>
        /// <param name="noButtonText">The text for the Cancel button.</param>
        /// <param name="showNoButton">Whether to show the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>True if Proceed was clicked, false if Cancel was clicked.</returns>
        public static Task<bool> Open(
            Control parent,
            string title,
            string description,
            bool defaultResult = false,
            string yesButtonText = "Proceed",
            bool showYesButton = true,
            string noButtonText = "Cancel",
            bool showNoButton = true,
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            new ConfirmationDialog(title, description, defaultResult, yesButtonText, showYesButton, noButtonText, showNoButton, isWarning: true),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Input
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Single-field text input dialogs.</summary>
    public static class Input
    {
        /// <summary>Shows a single-field text input dialog.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="defaultValue">The initial value in the text box.</param>
        /// <param name="placeholderText">The placeholder text shown when the text box is empty.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <param name="showCancelButton">Whether to show the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>The entered text, or null if the dialog was cancelled.</returns>
        public static Task<string?> Open(
            Control parent,
            string title,
            string description,
            string defaultValue = "",
            string placeholderText = "",
            string okButtonText = "OK",
            string cancelButtonText = "Cancel",
            bool showCancelButton = true,
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            new InputDialog(title, description, defaultValue, placeholderText, okButtonText, cancelButtonText, showCancelButton),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Progress
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Progress dialogs for long-running background work.</summary>
    public static class Progress
    {
        /// <summary>
        ///     Shows a progress dialog and runs <paramref name="work"/> on a background thread; do not
        ///     access UI elements from it. The dialog closes when the work completes or is cancelled, and
        ///     this returns once the work has finished.
        /// </summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="work">
        ///     The work to perform. Receives an <see cref="IProgress{T}"/> (values 0–100) and a
        ///     <see cref="CancellationToken"/>. Should throw <see cref="OperationCanceledException"/>
        ///     when the token is cancelled.
        /// </param>
        /// <param name="isIndeterminate">True to show an indeterminate progress bar.</param>
        /// <param name="showCancelButton">Whether to show the Cancel button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>True if the work completed successfully, false if it was cancelled or faulted.</returns>
        public static async Task<bool> Open(
            Control parent,
            string title,
            string description,
            Func<IProgress<double>, CancellationToken, Task> work,
            bool isIndeterminate = false,
            bool showCancelButton = true,
            string cancelButtonText = "Cancel",
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        )
        {
            // Resolve first so a missing host throws before any work starts.
            var mode = ResolveMode(displayMode, parent);
            var dialog = new ProgressDialog(title, description, isIndeterminate, showCancelButton, cancelButtonText);

            var finished = dialog.Start(work);
            var result = await ShowAsync(parent, dialog, maxWidth, mode, windowWidth, windowHeight);
            dialog.Cancel();
            await finished;
            return result;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Selection
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>List-based selection dialogs.</summary>
    public static class Selection
    {
        /// <summary>Shows a list of items to choose from.</summary>
        /// <typeparam name="T">The type of items to select from.</typeparam>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="items">The items to display in the list.</param>
        /// <param name="displaySelector">Converts an item to its display text.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <param name="showCancelButton">Whether to show the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>The selected item, or default if the dialog was cancelled or nothing was selected.</returns>
        public static async Task<T?> Open<T>(
            Control parent,
            string title,
            string description,
            IEnumerable<T> items,
            Func<T, string> displaySelector,
            string okButtonText = "OK",
            string cancelButtonText = "Cancel",
            bool showCancelButton = true,
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        )
        {
            var itemList = items.ToList();
            var dialog = new SelectionDialog(
                title, description, itemList.Select(displaySelector).ToList(),
                okButtonText, cancelButtonText, showCancelButton
            );

            var selectedIndex = await Show(parent, dialog, maxWidth, displayMode, windowWidth, windowHeight);
            return selectedIndex >= 0 && selectedIndex < itemList.Count ? itemList[selectedIndex] : default;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  MultiInput
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Multi-field form dialogs.</summary>
    public static class MultiInput
    {
        /// <summary>Shows a form with one text box per field.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="fields">
        ///     The fields to display, as (Label, DefaultValue) tuples. Labels are used as keys in the
        ///     returned dictionary. If duplicate labels are provided, the last value wins.
        /// </param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <param name="showCancelButton">Whether to show the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>Each label mapped to its entered value, or null if the dialog was cancelled.</returns>
        public static Task<Dictionary<string, string>?> Open(
            Control parent,
            string title,
            string description,
            IEnumerable<(string Label, string DefaultValue)> fields,
            string okButtonText = "OK",
            string cancelButtonText = "Cancel",
            bool showCancelButton = true,
            double maxWidth = 500,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            new MultiInputDialog(title, description, fields, okButtonText, cancelButtonText, showCancelButton),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Exception
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Exception detail dialogs with Report Issue, Ignore and Continue, and Exit Program buttons.</summary>
    public static class Exception
    {
        /// <summary>Shows the details of an exception.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text of the dialog.</param>
        /// <param name="exception">The exception to show details for.</param>
        /// <param name="reportIssueLink">The URL opened by the Report Issue button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="reportIssueText">The text for the Report Issue button.</param>
        /// <param name="ignoreAndContinueText">The text for the Ignore and Continue button.</param>
        /// <param name="exitProgramText">The text for the Exit Program button.</param>
        /// <param name="showReportIssueButton">Whether to show the Report Issue button.</param>
        /// <param name="showIgnoreAndContinueButton">Whether to show the Ignore and Continue button.</param>
        /// <param name="showExitProgramButton">
        ///     Whether to show the Exit Program button. If null, it is shown on desktop only. It is never
        ///     shown on platforms where the app can't shut itself down (mobile, browser).
        /// </param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <param name="copyDetailsText">The text for the button that copies the full exception to the clipboard, or null to hide it.</param>
        public static Task Open(
            Control parent,
            string title,
            string description,
            System.Exception exception,
            string reportIssueLink,
            double maxWidth = 600,
            string reportIssueText = "Report Issue",
            string ignoreAndContinueText = "Ignore and Continue",
            string exitProgramText = "Exit Program",
            bool showReportIssueButton = true,
            bool showIgnoreAndContinueButton = true,
            bool? showExitProgramButton = false,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null,
            string? copyDetailsText = "Copy Details"
        ) => Show(
            parent,
            new ExceptionDialog(
                title, description, exception, reportIssueLink,
                reportIssueText, ignoreAndContinueText, exitProgramText,
                showReportIssueButton, showIgnoreAndContinueButton, showExitProgramButton, copyDetailsText
            ),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Color
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Color picker dialogs.</summary>
    public static class Color
    {
        /// <summary>Shows a color picker with a spectrum, palette and RGB/HSV/hex inputs.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="initialColor">The color selected when the dialog opens; defaults to white.</param>
        /// <param name="description">Optional body text above the picker.</param>
        /// <param name="showAlpha">Whether the color's transparency can be changed.</param>
        /// <param name="palette">The palette's colors; defaults to the Fluent palette.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>The chosen color, or null if the dialog was cancelled.</returns>
        public static Task<global::Avalonia.Media.Color?> Open(
            Control parent,
            string title,
            global::Avalonia.Media.Color? initialColor = null,
            string description = "",
            bool showAlpha = true,
            IEnumerable<global::Avalonia.Media.Color>? palette = null,
            string okButtonText = "OK",
            string cancelButtonText = "Cancel",
            double maxWidth = 520,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            new ColorDialog(title, description, initialColor ?? global::Avalonia.Media.Colors.White, showAlpha, palette, okButtonText, cancelButtonText),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Markdown
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Dialogs that show Markdown, such as release notes, help or a licence to accept.</summary>
    public static class Markdown
    {
        /// <summary>Shows Markdown text in a scrollable, selectable view.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="markdown">The Markdown to show.</param>
        /// <param name="okButtonText">The text for the OK button.</param>
        /// <param name="cancelButtonText">
        /// The text for a Cancel button, e.g. "Decline" for a licence. Null (the default) shows only OK.
        /// </param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>True if OK was clicked (or, with no Cancel button, the dialog was dismissed); false if cancelled.</returns>
        public static Task<bool> Open(
            Control parent,
            string title,
            string markdown,
            string okButtonText = "OK",
            string? cancelButtonText = null,
            double maxWidth = 640,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(parent, new MarkdownDialog(title, markdown, okButtonText, cancelButtonText), maxWidth, displayMode, windowWidth, windowHeight);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Table
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Dialogs that show items in a sortable table, to view or to pick one.</summary>
    public static class Table
    {
        /// <summary>Shows items in a read-only, sortable table.</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text above the table.</param>
        /// <param name="items">The rows.</param>
        /// <param name="columns">The columns; by default, one per public property of <typeparamref name="T"/>.</param>
        /// <param name="okButtonText">The text for the button that closes the dialog.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        public static Task Open<T>(
            Control parent,
            string title,
            string description,
            IEnumerable<T> items,
            IEnumerable<TableColumn<T>>? columns = null,
            string okButtonText = "Close",
            double maxWidth = 800,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            TableDialog.Create(title, description, items.ToList(), columns, isPicker: false, okButtonText, cancelButtonText: null),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );

        /// <summary>Shows items in a sortable table and lets the user pick one (double-clicking a row picks it).</summary>
        /// <typeparam name="T">The row type.</typeparam>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="description">The body text above the table.</param>
        /// <param name="items">The rows.</param>
        /// <param name="columns">The columns; by default, one per public property of <typeparamref name="T"/>.</param>
        /// <param name="okButtonText">The text for the OK button, enabled once a row is selected.</param>
        /// <param name="cancelButtonText">The text for the Cancel button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>The picked item, or default if the dialog was cancelled.</returns>
        public static async Task<T?> Pick<T>(
            Control parent,
            string title,
            string description,
            IEnumerable<T> items,
            IEnumerable<TableColumn<T>>? columns = null,
            string okButtonText = "OK",
            string cancelButtonText = "Cancel",
            double maxWidth = 800,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        )
        {
            var itemList = items.ToList();
            var dialog = TableDialog.Create(title, description, itemList, columns, isPicker: true, okButtonText, cancelButtonText);
            var index = await Show(parent, dialog, maxWidth, displayMode, windowWidth, windowHeight);
            return index >= 0 && index < itemList.Count ? itemList[index] : default;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Web
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Dialogs that show a web page or HTML using the platform's web view (WebView2 on Windows, WKWebView on macOS and
    /// iOS, WebKitGTK on Linux, Android's WebView). Works best as a window: an overlay can't draw over a native web view.
    /// </summary>
    public static class Web
    {
        /// <summary>Shows a web page.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="address">The page to show.</param>
        /// <param name="closeWhen">
        /// Closes the dialog when the page navigates to an address this returns true for, e.g. a sign-in redirect. The
        /// navigation is cancelled and the address returned.
        /// </param>
        /// <param name="height">The height of the web view.</param>
        /// <param name="closeButtonText">The text for the Close button.</param>
        /// <param name="openInBrowserText">The text for a button that opens the page in the browser; null hides it.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>The address shown when the dialog closed, or the one that matched <paramref name="closeWhen"/>.</returns>
        public static Task<Uri?> Open(
            Control parent,
            string title,
            Uri address,
            Func<Uri, bool>? closeWhen = null,
            double height = 560,
            string closeButtonText = "Close",
            string? openInBrowserText = "Open in Browser",
            double maxWidth = 900,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        )
        {
            ArgumentNullException.ThrowIfNull(address);
            return Show(
                parent,
                new WebDialog(title, address, html: null, height, closeWhen, closeButtonText, openInBrowserText),
                maxWidth,
                displayMode,
                windowWidth,
                windowHeight
            );
        }

        /// <summary>Shows HTML.</summary>
        /// <param name="parent">A control in the visual tree the dialog belongs to.</param>
        /// <param name="title">The title of the dialog.</param>
        /// <param name="html">The HTML document to show.</param>
        /// <param name="baseAddress">The address relative links in <paramref name="html"/> resolve against.</param>
        /// <param name="closeWhen">Closes the dialog when a link to an address this returns true for is followed.</param>
        /// <param name="height">The height of the web view.</param>
        /// <param name="closeButtonText">The text for the Close button.</param>
        /// <param name="maxWidth">The maximum width of the overlay card, and the window's width unless <paramref name="windowWidth"/> is set.</param>
        /// <param name="displayMode">Overrides <see cref="DefaultMode"/> for this call.</param>
        /// <param name="windowWidth">A fixed width for the window. Ignored in overlays.</param>
        /// <param name="windowHeight">A fixed height for the window; by default it fits the content. Ignored in overlays.</param>
        /// <returns>The address shown when the dialog closed, or null if no link was followed.</returns>
        public static Task<Uri?> OpenHtml(
            Control parent,
            string title,
            string html,
            Uri? baseAddress = null,
            Func<Uri, bool>? closeWhen = null,
            double height = 560,
            string closeButtonText = "Close",
            double maxWidth = 900,
            DialogDisplayMode? displayMode = null,
            double? windowWidth = null,
            double? windowHeight = null
        ) => Show(
            parent,
            new WebDialog(title, baseAddress, html, height, closeWhen, closeButtonText, openInBrowserText: null),
            maxWidth,
            displayMode,
            windowWidth,
            windowHeight
        );
    }
}
