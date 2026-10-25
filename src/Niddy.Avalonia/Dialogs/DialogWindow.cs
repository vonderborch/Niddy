using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Niddy.Avalonia.Dialogs;

/// <summary>
///     Shows a dialog in a modal window (desktop only). The window sizes its height to the dialog unless a
///     fixed height is given, and its close button dismisses the dialog as the dialog's cancel button would.
///     A filling dialog gets a resizable window instead, and fills it rather than scrolling.
/// </summary>
internal sealed class DialogWindow : Window
{
    /// <summary>How much of its owner a filling dialog's window takes, when no size is given.</summary>
    private const double FillShare = 0.8;

    private readonly IDialog _dialog;
    private bool _isClosed;

    private DialogWindow(Control content, IDialog dialog, double width, double? height)
    {
        _dialog = dialog;
        Title = dialog.WindowTitle ?? string.Empty;
        Width = width;
        if (height is { } fixedHeight)
        {
            Height = fixedHeight;
        }
        else
        {
            SizeToContent = SizeToContent.Height;
        }

        CanResize = dialog.Fill;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Border padded = new() { Padding = new Thickness(20), Child = content };
        if (dialog.Fill)
        {
            MinWidth = 300;
            MinHeight = 200;
            Content = padded;
        }
        else
        {
            Content = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = padded };
        }

        Closing += OnClosing;
        Closed += (_, _) => _isClosed = true;
    }

    internal static async Task ShowAsync(Window owner, Control content, IDialog dialog, double width, double? height = null)
    {
        if (dialog.Fill)
        {
            // Without a size of its own, a filling dialog starts at most of its owner.
            width = double.IsFinite(width) ? width : owner.Bounds.Width * FillShare;
            height ??= owner.Bounds.Height * FillShare;
        }

        DialogWindow window = new(content, dialog, width, height);

        // Long content scrolls rather than growing the window past its owner.
        if (height is null)
        {
            window.MaxHeight = Math.Max(200, owner.Bounds.Height);
        }

        Task closed = window.ShowDialog(owner);
        await Task.WhenAny(dialog.Completion, closed);
        if (!window._isClosed)
        {
            window.Close();
        }

        await closed;

        // The window can be closed without the dialog completing, e.g. when the owner closes.
        dialog.TryDismiss(force: true);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        // The user closed the window: treat it like Escape or back, which a dialog without a cancel button refuses.
        if (!e.IsProgrammatic && !_dialog.TryDismiss())
        {
            e.Cancel = true;
        }
    }
}
