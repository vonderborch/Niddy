using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A yes/no dialog. As a warning, the title is orange and Enter doesn't confirm.</summary>
internal sealed partial class ConfirmationDialog : DialogBase<bool>
{
    private readonly bool _defaultResult;

    protected override bool DismissResult => _defaultResult;

    public ConfirmationDialog()
    {
        InitializeComponent();
    }

    internal ConfirmationDialog(string title, string description, bool defaultResult, string yesButtonText, bool showYesButton, string noButtonText, bool showNoButton, bool isWarning)
        : this()
    {
        _defaultResult = defaultResult;
        Title = title;
        Description = description;
        ButtonYes.Content = yesButtonText;
        ButtonYes.IsVisible = showYesButton;
        ButtonNo.Content = noButtonText;
        ButtonNo.IsVisible = showNoButton;
        if (isWarning)
        {
            TitleForeground = Brushes.Orange;
            ButtonYes.IsDefault = false;
        }
    }

    private void ButtonNo_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(result: false);
    }

    private void ButtonYes_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(result: true);
    }
}
