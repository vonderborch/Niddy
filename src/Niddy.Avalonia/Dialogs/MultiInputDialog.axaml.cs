using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A multi-field text input dialog. The result maps each label to its value, or is null if cancelled.</summary>
internal sealed partial class MultiInputDialog : DialogBase<Dictionary<string, string>?>
{
    private readonly List<(string Label, TextBox TextBox)> _fields = new List<(string, TextBox)>();

    protected override bool CanDismiss => ButtonCancel.IsVisible;

    protected override Dictionary<string, string>? DismissResult => null;

    protected override Control? InitialFocus => (_fields.Count > 0) ? _fields[0].TextBox : null;

    public MultiInputDialog()
    {
        InitializeComponent();
    }

    internal MultiInputDialog(string title, string description, IEnumerable<(string Label, string DefaultValue)> fields, string okButtonText, string cancelButtonText, bool showCancelButton)
        : this()
    {
        Title = title;
        Description = description;
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = showCancelButton;
        foreach (var field in fields)
        {
            string item = field.Label;
            string item2 = field.DefaultValue;
            TextBox textBox = new TextBox
            {
                Text = item2,
                FontSize = 14.0,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            PanelFields.Children.Add(new TextBlock
            {
                Text = item,
                FontSize = 13.0
            });
            PanelFields.Children.Add(textBox);
            _fields.Add((item, textBox));
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e)
    {
        Dictionary<string, string> dictionary = new Dictionary<string, string>();
        foreach (var field in _fields)
        {
            string item = field.Label;
            TextBox item2 = field.TextBox;
            dictionary[item] = item2.Text ?? string.Empty;
        }
        Close(dictionary);
    }
}
