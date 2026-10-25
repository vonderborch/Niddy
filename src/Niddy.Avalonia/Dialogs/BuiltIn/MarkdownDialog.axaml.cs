using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Niddy.Avalonia.Dialogs.BuiltIn;

/// <summary>A dialog showing Markdown, e.g. release notes or a licence. The result is true for OK, false if cancelled.</summary>
internal sealed partial class MarkdownDialog : DialogBase<bool>
{
    public MarkdownDialog()
    {
        InitializeComponent();
    }

    internal MarkdownDialog(string title, string markdown, string okButtonText, string? cancelButtonText, double? contentHeight = null) : this()
    {
        Title = title;
        MarkdownViewer.Markdown = markdown;
        SetContentHeight(MarkdownViewer, contentHeight);
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = cancelButtonText is not null;
    }

    /// <summary>The Markdown view.</summary>
    internal Control Viewer => MarkdownViewer;

    /// <summary>The Markdown shown.</summary>
    internal string? Markdown => MarkdownViewer.Markdown;

    protected override bool DismissResult => !ButtonCancel.IsVisible;

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e) => Close(true);
}
