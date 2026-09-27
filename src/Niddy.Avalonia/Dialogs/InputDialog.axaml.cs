using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A single-field text input dialog. The result is null if cancelled.</summary>
internal sealed partial class InputDialog : DialogBase<string?>
{
    protected override bool CanDismiss => ButtonCancel.IsVisible;

    protected override string? DismissResult => null;

    protected override Control InitialFocus => TextBoxInput;

    public InputDialog()
    {
        InitializeComponent();
    }

    internal InputDialog(string title, string description, string defaultValue, string placeholderText, string okButtonText, string cancelButtonText, bool showCancelButton)
        : this()
    {
        Title = title;
        Description = description;
        TextBoxInput.Text = defaultValue;
        TextBoxInput.PlaceholderText = placeholderText;
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = showCancelButton;
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(TextBoxInput.Text);
    }
}
