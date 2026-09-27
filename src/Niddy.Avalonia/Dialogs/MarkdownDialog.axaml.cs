using Avalonia.Interactivity;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A dialog showing Markdown, e.g. release notes or a licence. The result is true for OK, false if cancelled.</summary>
internal sealed partial class MarkdownDialog : DialogBase<bool>
{
    public MarkdownDialog()
    {
        InitializeComponent();
    }

    internal MarkdownDialog(string title, string markdown, string okButtonText, string? cancelButtonText) : this()
    {
        Title = title;
        MarkdownViewer.Markdown = markdown;
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = cancelButtonText is not null;
    }

    /// <summary>The Markdown shown.</summary>
    internal string? Markdown => MarkdownViewer.Markdown;

    protected override bool DismissResult => !ButtonCancel.IsVisible;

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e) => Close(true);
}
