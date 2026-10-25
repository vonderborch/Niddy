using System.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Niddy.Avalonia.Dialogs.BuiltIn;

/// <summary>
/// A table of items. As a viewer the result is -1; as a picker it's the index (in the original list) of the chosen row,
/// or -1 if cancelled.
/// </summary>
internal sealed partial class TableDialog : DialogBase<int>
{
    private IList _items = Array.Empty<object>();
    private bool _isPicker;

    public TableDialog()
    {
        InitializeComponent();
    }

    private TableDialog(string title, string description, IList items, bool isPicker, string okButtonText, string? cancelButtonText, double? contentHeight) : this()
    {
        Title = title;
        Description = description;
        SetContentHeight(DataGridItems, contentHeight);
        _items = items;
        _isPicker = isPicker;
        DataGridItems.ItemsSource = items;
        ButtonOk.Content = okButtonText;
        ButtonCancel.Content = cancelButtonText;
        ButtonCancel.IsVisible = isPicker && cancelButtonText is not null;
        if (isPicker)
        {
            ButtonOk.IsEnabled = false;
            DataGridItems.SelectionChanged += (_, _) => ButtonOk.IsEnabled = DataGridItems.SelectedItem is not null;
        }
    }

    internal static TableDialog Create<T>(
        string title,
        string description,
        IReadOnlyList<T> items,
        IEnumerable<TableColumn<T>>? columns,
        bool isPicker,
        string okButtonText,
        string? cancelButtonText,
        double? contentHeight = null)
    {
        IList list = items as IList ?? items.ToList();
        TableDialog dialog = new(title, description, list, isPicker, okButtonText, cancelButtonText, contentHeight);
        if (columns is null)
        {
            dialog.DataGridItems.AutoGenerateColumns = true;
        }
        else
        {
            dialog.DataGridItems.AutoGenerateColumns = false;
            foreach (TableColumn<T> column in columns)
            {
                dialog.DataGridItems.Columns.Add(CreateColumn(column));
            }
        }
        return dialog;
    }

    /// <summary>The table.</summary>
    internal DataGrid Grid => DataGridItems;

    /// <summary>Selects a row by its index in the original list, as if the user had clicked it.</summary>
    internal void Select(int index) => DataGridItems.SelectedItem = _items[index];

    protected override Control? InitialFocus => DataGridItems;

    protected override int DismissResult => -1;

    private static DataGridColumn CreateColumn<T>(TableColumn<T> column)
    {
        string Text(object? row) => row is T item
            ? column.Format is { } format ? format(column.Value(item)) : column.Value(item)?.ToString() ?? string.Empty
            : string.Empty;

        return new DataGridTemplateColumn
        {
            Header = column.Header,
            Width = column.Width ?? DataGridLength.Auto,
            CanUserSort = true,
            CustomSortComparer = Comparer<object>.Create((x, y) =>
                Comparer<object?>.Default.Compare(x is T a ? column.Value(a) : null, y is T b ? column.Value(b) : null)),
            CellTemplate = new FuncDataTemplate<object?>((row, _) => new TextBlock
            {
                Text = Text(row),
                Margin = new global::Avalonia.Thickness(8, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = column.AlignRight ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            }),
        };
    }

    private int SelectedIndex => DataGridItems.SelectedItem is { } item ? _items.IndexOf(item) : -1;

    private void DataGridItems_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_isPicker && SelectedIndex >= 0)
        {
            Close(SelectedIndex);
        }
    }

    private void ButtonCancel_OnClick(object? sender, RoutedEventArgs e) => Close(-1);

    private void ButtonOk_OnClick(object? sender, RoutedEventArgs e) => Close(_isPicker ? SelectedIndex : -1);
}
