using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace Niddy.Avalonia.Tables;

/// <summary>
/// The column of lock buttons a <see cref="TableGrid"/> adds when <see cref="TableGrid.LockRows"/> is on: each button
/// unlocks its row for editing, or locks it again.
/// </summary>
internal sealed class TableGridLockColumn : DataGridColumn
{
    private static readonly Geometry LockIcon = Geometry.Parse(
        "M12,17A2,2 0 0,0 14,15C14,13.89 13.1,13 12,13A2,2 0 0,0 10,15A2,2 0 0,0 12,17M18,8A2,2 0 0,1 20,10V20A2,2 0 0,1 " +
        "18,22H6A2,2 0 0,1 4,20V10C4,8.89 4.9,8 6,8H7V6A5,5 0 0,1 12,1A5,5 0 0,1 17,6V8H18M12,3A3,3 0 0,0 9,6V8H15V6A3,3 " +
        "0 0,0 12,3Z");

    private static readonly Geometry UnlockedIcon = Geometry.Parse(
        "M18,1C15.24,1 13,3.24 13,6V8H4C2.9,8 2,8.9 2,10V20C2,21.1 2.9,22 4,22H16C17.1,22 18,21.1 18,20V10C18,8.9 17.1,8 " +
        "16,8H15V6C15,4.34 16.34,3 18,3C19.66,3 21,4.34 21,6V8H23V6C23,3.24 20.76,1 18,1M10,13C11.1,13 12,13.9 12,15C12," +
        "16.1 11.1,17 10,17C8.9,17 8,16.1 8,15C8,13.9 8.9,13 10,13Z");

    private readonly TableGrid _grid;

    public TableGridLockColumn(TableGrid grid)
    {
        _grid = grid;
        IsReadOnly = true;
        CanUserSort = false;
        CanUserResize = false;
        CanUserReorder = false;
        Width = new DataGridLength(36);
        Header = string.Empty;
    }

    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        PathIcon icon = new() { Width = 14, Height = 14 };
        Button button = new()
        {
            Name = "LockButton",
            Content = icon,
            Background = Brushes.Transparent,
            BorderThickness = new global::Avalonia.Thickness(0),
            Padding = new global::Avalonia.Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = false,
        };

        void Update()
        {
            object? row = button.DataContext;
            bool unlocked = row is not null && _grid.IsRowUnlocked(row);
            icon.Data = unlocked ? UnlockedIcon : LockIcon;
            ToolTip.SetTip(button, unlocked ? "Lock row" : "Unlock row");
            button.IsEnabled = row is not null && (unlocked || _grid.CanUnlockRow?.Invoke(row) != false);
            button.Classes.Set("unlocked", unlocked);
        }

        void Grid_OnLockChanged() => Update();

        button.DataContextChanged += (_, _) => Update();
        button.AttachedToVisualTree += (_, _) =>
        {
            _grid.LockChanged += Grid_OnLockChanged;
            Update();
        };
        button.DetachedFromVisualTree += (_, _) => _grid.LockChanged -= Grid_OnLockChanged;
        button.Click += (_, e) =>
        {
            e.Handled = true;
            if (button.DataContext is not { } row)
            {
                return;
            }

            if (_grid.IsRowUnlocked(row))
            {
                _grid.LockRow(row);
            }
            else
            {
                _grid.UnlockRow(row);
            }
        };
        Update();
        return button;
    }

    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase binding)
    {
        binding = null!;
        return GenerateElement(cell, dataItem);
    }

    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs) => string.Empty;
}
