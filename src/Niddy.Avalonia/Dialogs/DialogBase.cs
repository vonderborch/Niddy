using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Niddy.Avalonia.Dialogs;

/// <summary>What a dialog host (overlay or window) needs from a dialog, independent of its result type.</summary>
internal interface IDialog
{
    /// <summary>Completes when the dialog has a result.</summary>
    Task Completion { get; }

    /// <summary>The title to use when the dialog is shown in a window.</summary>
    string? WindowTitle { get; }

    /// <summary>
    ///     Dismisses the dialog as its cancel button would. Returns false if the dialog can't be
    ///     dismissed that way (see <see cref="DialogBase{TResult}.CanDismiss"/>) unless <paramref name="force"/> is set.
    /// </summary>
    bool TryDismiss(bool force = false);
}

/// <summary>
///     Base class for dialogs shown with <see cref="Dialog.Show{TResult}"/>. A dialog is just its content:
///     the same control is shown in an overlay card or in a window, and the host provides the chrome.
///     It inherits the standard title, description and button row from <see cref="DialogLayout"/>, so use
///     <c>DialogLayout</c> as the root element of the dialog's AXAML file.
///     <para>
///         Call <see cref="Close"/> with the result (typically from a button). The dialog can also be
///         dismissed without a button: the window's close button, Escape, or the platform back request.
///         Override <see cref="CanDismiss"/> and <see cref="DismissResult"/> to control that.
///     </para>
/// </summary>
/// <typeparam name="TResult">The type of the dialog's result.</typeparam>
public abstract class DialogBase<TResult> : DialogLayout, IDialog
{
    private readonly TaskCompletionSource<TResult> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes with the dialog's result when it closes.</summary>
    public Task<TResult> Result => _result.Task;

    /// <summary>
    ///     The title of the window when the dialog is shown in one. Defaults to <see cref="DialogLayout.Title"/>.
    ///     Ignored in overlays.
    /// </summary>
    public string? WindowTitle { get; set; }

    Task IDialog.Completion => _result.Task;

    string? IDialog.WindowTitle => WindowTitle ?? Title;

    /// <summary>
    ///     Whether the dialog can be dismissed without a button (window close button, Escape, platform
    ///     back). Defaults to true. Typically false when the dialog has no visible cancel button.
    /// </summary>
    protected virtual bool CanDismiss => true;

    /// <summary>The result when the dialog is dismissed without a button.</summary>
    protected abstract TResult DismissResult { get; }

    /// <summary>The control to focus when the dialog is shown. Defaults to the dialog itself.</summary>
    protected virtual Control? InitialFocus => null;

    /// <summary>Closes the dialog with <paramref name="result"/>. Safe to call from any thread; later calls are ignored.</summary>
    protected void Close(TResult result) => _result.TrySetResult(result);

    /// <summary>Called when the dialog is dismissed without a button, just before it closes with <see cref="DismissResult"/>.</summary>
    protected virtual void OnDismissed()
    {
    }

    bool IDialog.TryDismiss(bool force)
    {
        if (_result.Task.IsCompleted)
            return true;
        if (!force && !CanDismiss)
            return false;

        OnDismissed();
        Close(DismissResult);
        return true;
    }

    /// <inheritdoc />
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Focusable = true;
        (InitialFocus ?? this).Focus();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key == Key.Escape)
            e.Handled = ((IDialog)this).TryDismiss();
    }
}
