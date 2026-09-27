using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Niddy.Avalonia.Dialogs;

/// <summary>An informational dialog with a single OK button.</summary>
internal sealed partial class NotificationDialog : DialogBase<bool>
{
    protected override bool DismissResult => false;

    public NotificationDialog()
    {
        InitializeComponent();
    }

    internal NotificationDialog(string title, string description, string okButtonText)
        : this()
    {
        Title = title;
        Description = description;
        ButtonOk.Content = okButtonText;
    }

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(result: true);
    }
}
