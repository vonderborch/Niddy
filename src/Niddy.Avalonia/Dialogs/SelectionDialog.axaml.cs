using Avalonia.Interactivity;

namespace Niddy.Avalonia.Dialogs;

/// <summary>A list selection dialog. The result is the selected index, or -1 if cancelled or nothing was selected.</summary>
internal sealed partial class SelectionDialog : DialogBase<int>
{
    public SelectionDialog()
    {
        InitializeComponent();
    }

    internal SelectionDialog(
        string title,
        string description,
        IReadOnlyList<string> items,
        string okButtonText,
        string cancelButtonText,
        bool showCancelButton
    ) : this()
    {
        Title = title;
        Description = description;
        ListBoxItems.ItemsSource = items;
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = showCancelButton;
    }

    protected override bool CanDismiss => ButtonCancel.IsVisible;

    protected override int DismissResult => -1;

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e) => Close(-1);

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e) => Close(ListBoxItems.SelectedIndex);
}
