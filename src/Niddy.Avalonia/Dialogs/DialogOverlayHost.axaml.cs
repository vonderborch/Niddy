using Avalonia;
using Avalonia.Controls;
using Niddy.Avalonia.Utilities;

namespace Niddy.Avalonia.Dialogs;

/// <summary>
///     Hosts content and shows overlay dialogs on top of it. Wrap your window's or main view's content
///     in one of these to use overlay dialogs, which work on every platform (desktop, mobile, browser).
///     <para>
///         Dialogs are kept clear of safe-area insets and the on-screen keyboard, and the platform back
///         request dismisses the open dialog as its cancel button would. A dialog opened while another
///         is showing stacks on top of it.
///     </para>
/// </summary>
public partial class DialogOverlayHost : UserControl
{
    private const double DialogMargin = 16;

    private sealed record OpenDialog(Control Content, IDialog Dialog, double MaxWidth);

    private readonly List<OpenDialog> _openDialogs = new();
    private IDisposable? _backRegistration;

    public DialogOverlayHost()
    {
        InitializeComponent();
        _ = new ObstructionTracker(this, obstruction =>
            DialogCard.Margin = new Thickness(DialogMargin) + obstruction);
    }

    /// <summary>
    ///     Gets or sets the main content displayed beneath the overlay.
    /// </summary>
    public new object? Content
    {
        get => HostContent.Content;
        set => HostContent.Content = value;
    }

    internal async Task ShowAsync(Control content, IDialog dialog, double maxWidth)
    {
        var entry = new OpenDialog(content, dialog, maxWidth);
        _openDialogs.Add(entry);
        ShowTopDialog();
        try
        {
            await dialog.Completion;
        }
        finally
        {
            _openDialogs.Remove(entry);
            ShowTopDialog();
        }
    }

    private void ShowTopDialog()
    {
        if (_openDialogs.Count == 0)
        {
            OverlayPanel.IsVisible = false;
            DialogPresenter.Content = null;
            return;
        }

        var top = _openDialogs[^1];
        DialogCard.MaxWidth = top.MaxWidth;
        DialogPresenter.Content = top.Content;
        OverlayPanel.IsVisible = true;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is { } topLevel)
            _backRegistration = BackNavigation.Register(topLevel, BackNavigation.DialogPriority, OnBackRequested);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _backRegistration?.Dispose();
        _backRegistration = null;
    }

    private bool OnBackRequested()
    {
        if (_openDialogs.Count == 0)
            return false;

        // Always consume the request while a dialog is open so the page underneath doesn't navigate.
        _openDialogs[^1].Dialog.TryDismiss();
        return true;
    }

    /// <summary>
    ///     Walks the logical parent chain from <paramref name="control"/> upward to find
    ///     the nearest <see cref="DialogOverlayHost"/>. Returns null if none is found.
    /// </summary>
    public static DialogOverlayHost? FindInVisualTree(Control control)
    {
        var current = control.Parent;
        while (current is not null)
        {
            if (current is DialogOverlayHost host)
                return host;
            current = current.Parent;
        }
        return null;
    }
}
