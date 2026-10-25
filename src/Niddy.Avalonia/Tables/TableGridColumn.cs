using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Niddy.Avalonia.Tables;

/// <summary>
/// A <see cref="DataGridColumn"/> whose cells come from a function rather than a binding, formatted for reading:
/// numbers right-aligned, dates in local time, lists joined with commas. It sorts by its values, in memory or (with
/// a <see cref="TableSource"/>) through the source. Create <see cref="TableGridColumn{T}"/>s. It works in any
/// <see cref="DataGrid"/>; editing needs a <see cref="TableGrid"/>.
/// </summary>
public abstract class TableGridColumn : DataGridColumn
{
    private readonly ConditionalWeakTable<Control, string[]> _cellClasses = new();

    private protected TableGridColumn(string header)
    {
        ArgumentNullException.ThrowIfNull(header);
        Header = header;
        CanUserSort = true;
        CustomSortComparer = new RowComparer(this);
    }

    /// <summary>
    /// How cells are aligned. Null (the default) right-aligns numbers and left-aligns everything else.
    /// </summary>
    public HorizontalAlignment? Alignment
    {
        get;
        set
        {
            field = value;
            NotifyPropertyChanged(nameof(Alignment));
        }
    }

    /// <summary>
    /// A .NET format string for values that support one, e.g. <c>N0</c> for numbers or <c>yyyy-MM-dd</c> for dates.
    /// Values are formatted with the current culture.
    /// </summary>
    public string? FormatString
    {
        get;
        set
        {
            field = value;
            NotifyPropertyChanged(nameof(FormatString));
        }
    }

    /// <summary>The text of a cell whose value is null. Defaults to empty.</summary>
    public string NullText
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
            NotifyPropertyChanged(nameof(NullText));
        }
    } = string.Empty;

    /// <summary>
    /// Compares two cells' values (not rows) when the grid sorts its rows in memory by this column. Null (the
    /// default) puts nulls first, compares text ignoring case and numbers by value, and anything else by its text.
    /// A <see cref="TableSource"/> sorts its own pages, so this isn't used for it.
    /// </summary>
    public IComparer<object?>? Comparer { get; set; }

    /// <summary>The comparer used when <see cref="Comparer"/> is null.</summary>
    internal static IComparer<object?> DefaultComparer { get; } = Comparer<object?>.Create(CompareValues);

    /// <summary>
    /// Whether the cells can be edited: true when the column can edit (<see cref="TableGridColumn{T}.Edit"/> is set),
    /// and neither it nor its grid is read-only. Setting it to true doesn't make a column without an edit function
    /// editable.
    /// </summary>
    public override bool IsReadOnly
    {
        get => !CanEdit || base.IsReadOnly;
        set => base.IsReadOnly = value;
    }

    /// <summary>Whether the column has a way to apply an edit.</summary>
    private protected virtual bool CanEdit => false;

    /// <summary>A row's value in this column, used for its text, alignment and sorting.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The value, or null if <paramref name="row"/> isn't the column's row type.</returns>
    public abstract object? GetValue(object row);

    /// <summary>
    /// A row's text in this column: the value formatted with <see cref="FormatString"/>, or <see cref="NullText"/>.
    /// Dates without a format string are shown in local time, short; lists are joined with commas.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The text.</returns>
    public virtual string GetText(object row) => FormatValue(GetValue(row));

    /// <summary>Formats a value as <see cref="GetText"/> does.</summary>
    private protected string FormatValue(object? value) => value switch
    {
        null => NullText,
        string text => text,
        DateTimeOffset date when FormatString is null => date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
        DateTime date when FormatString is null => date.ToString("g", CultureInfo.CurrentCulture),
        IFormattable formattable => formattable.ToString(FormatString, CultureInfo.CurrentCulture),
        IEnumerable list => string.Join(", ", list.Cast<object?>().Select(FormatValue)),
        _ => value.ToString() ?? NullText,
    };

    /// <summary>Whether the cells are custom controls rather than text.</summary>
    private protected virtual bool HasCellTemplate => false;

    /// <summary>Builds a custom cell for a row, when <see cref="HasCellTemplate"/>.</summary>
    private protected virtual Control? BuildCell(object row) => null;

    /// <summary>The style classes for a row's cell, space-separated, or null.</summary>
    private protected virtual string? GetCellClass(object row) => null;

    /// <summary>A row's text color, or null for the default.</summary>
    private protected virtual IBrush? GetForeground(object row) => null;

    /// <summary>Applies an edited cell's text to a row. Throws to reject it.</summary>
    private protected virtual void SetText(object row, string text) =>
        throw new NotSupportedException("This column can't be edited.");

    /// <summary>
    /// Creates a cell's content. The grid recycles cells as it scrolls and pages, so the content follows its data
    /// context, and a row that raises <see cref="INotifyPropertyChanged.PropertyChanged"/> refreshes its text.
    /// </summary>
    /// <param name="cell">The cell.</param>
    /// <param name="dataItem">The row.</param>
    /// <returns>A <c>TextBlock</c>, or a <c>ContentControl</c> holding the cell template's control.</returns>
    protected override Control GenerateElement(DataGridCell cell, object dataItem)
    {
        Control element;
        if (HasCellTemplate)
        {
            element = new ContentControl { VerticalContentAlignment = VerticalAlignment.Center };
        }
        else
        {
            TextBlock text = new() { Name = "CellTextBlock", TextTrimming = TextTrimming.CharacterEllipsis };
            if (FindTheme("DataGridCellTextBlockTheme") is { } theme)
            {
                text.Theme = theme;
            }
            else
            {
                text.Margin = new global::Avalonia.Thickness(12, 0);
                text.VerticalAlignment = VerticalAlignment.Center;
            }

            element = text;
        }

        CellUpdater.Attach(this, element, dataItem);
        return element;
    }

    /// <summary>Creates the text box a cell is edited in, holding the row's value as text.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="dataItem">The row.</param>
    /// <param name="binding">Always null: edits are applied when the cell is committed.</param>
    /// <returns>A <c>TextBox</c>.</returns>
    protected override Control GenerateEditingElement(DataGridCell cell, object dataItem, out BindingExpressionBase binding)
    {
        TextBox box = new() { Name = "CellTextBox", Text = GetEditingText(dataItem) };
        if (FindTheme("DataGridCellTextBoxTheme") is { } theme)
        {
            box.Theme = theme;
        }

        binding = null!;
        return box;
    }

    /// <summary>
    /// Readies the text box: F2 puts the caret at the end, anything else selects all the text.
    /// </summary>
    /// <param name="editingElement">The text box.</param>
    /// <param name="editingEventArgs">What started the edit.</param>
    /// <returns>The text before editing.</returns>
    protected override object PrepareCellForEdit(Control editingElement, RoutedEventArgs editingEventArgs)
    {
        if (editingElement is not TextBox box)
        {
            return string.Empty;
        }

        if (box.DataContext is { } row)
        {
            box.Text = GetEditingText(row);
        }

        DataValidationErrors.ClearErrors(box);
        string text = box.Text ?? string.Empty;
        if (editingEventArgs is KeyEventArgs { Key: Key.F2 })
        {
            box.SelectionStart = text.Length;
            box.SelectionEnd = text.Length;
        }
        else
        {
            box.SelectionStart = 0;
            box.SelectionEnd = text.Length;
        }

        box.CaretIndex = text.Length;
        return text;
    }

    /// <summary>Puts the text from before editing back.</summary>
    /// <param name="editingElement">The text box.</param>
    /// <param name="uneditedValue">The text before editing.</param>
    protected override void CancelCellEdit(Control editingElement, object uneditedValue)
    {
        if (editingElement is TextBox box)
        {
            box.Text = uneditedValue as string ?? string.Empty;
            DataValidationErrors.ClearErrors(box);
        }
    }

    /// <summary>Refreshes a cell after one of the column's properties changed.</summary>
    /// <param name="element">The cell's content.</param>
    /// <param name="propertyName">The property.</param>
    protected override void RefreshCellContent(Control element, string propertyName) => CellUpdater.Fill(this, element);

    /// <summary>
    /// Applies an edited cell to its row. On failure the text box shows the exception's message and the cell stays
    /// in edit.
    /// </summary>
    /// <returns>Whether the edit was applied.</returns>
    internal bool CommitEdit(object row, Control editingElement)
    {
        if (editingElement is not TextBox box)
        {
            return true;
        }

        try
        {
            SetText(row, box.Text ?? string.Empty);
            DataValidationErrors.ClearErrors(box);
            return true;
        }
        catch (Exception exception)
        {
            DataValidationErrors.SetError(box, exception);
            return false;
        }
    }

    /// <summary>A row's value as editable text: <see cref="FormatValue"/>, but empty for null.</summary>
    private string GetEditingText(object row) => GetValue(row) is { } value ? FormatValue(value) : string.Empty;

    private ControlTheme? FindTheme(string key) =>
        OwningGrid is { } grid && grid.TryFindResource(key, out object? theme) ? theme as ControlTheme : null;

    /// <summary>Fills a text cell for a row. Internal for tests.</summary>
    internal void UpdateText(TextBlock text, object? row)
    {
        object? value = row is null ? null : GetValue(row);
        text.Text = row is null ? string.Empty : GetText(row);
        text.HorizontalAlignment = Alignment ?? (IsNumber(value) ? HorizontalAlignment.Right : HorizontalAlignment.Left);
        text.TextAlignment = text.HorizontalAlignment == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left;
        if (row is not null && GetForeground(row) is { } foreground)
        {
            text.Foreground = foreground;
        }
        else
        {
            text.ClearValue(TextBlock.ForegroundProperty);
        }

        SetClasses(text, row);
    }

    private void UpdateTemplate(ContentControl host, object? row)
    {
        host.Content = row is null ? null : BuildCell(row);
        SetClasses(host, row);
    }

    private void SetClasses(Control cell, object? row)
    {
        if (_cellClasses.TryGetValue(cell, out string[]? previous))
        {
            cell.Classes.RemoveAll(previous);
        }

        string[] classes = (row is null ? null : GetCellClass(row))
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        cell.Classes.AddRange(classes);
        _cellClasses.AddOrUpdate(cell, classes);
    }

    internal static bool IsNumber(object? value) => value is byte or sbyte or short or ushort or int or uint or long
        or ulong or float or double or decimal or Int128 or UInt128 or nint or nuint;

    private static int CompareValues(object? x, object? y)
    {
        if (ReferenceEquals(x, y))
        {
            return 0;
        }

        if (x is null)
        {
            return -1;
        }

        if (y is null)
        {
            return 1;
        }

        if (x is string a && y is string b)
        {
            return string.Compare(a, b, CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
        }

        if (IsNumber(x) && IsNumber(y) && x.GetType() != y.GetType())
        {
            return Convert.ToDouble(x, CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(y, CultureInfo.InvariantCulture));
        }

        if (x.GetType() == y.GetType() && x is IComparable comparable)
        {
            return comparable.CompareTo(y);
        }

        return string.Compare(Convert.ToString(x, CultureInfo.CurrentCulture), Convert.ToString(y, CultureInfo.CurrentCulture),
            CultureInfo.CurrentCulture, CompareOptions.IgnoreCase);
    }

    /// <summary>Compares rows by the column's values, for the grid's in-memory sort.</summary>
    private sealed class RowComparer(TableGridColumn column) : IComparer
    {
        public int Compare(object? x, object? y) => (column.Comparer ?? DefaultComparer)
            .Compare(x is null ? null : column.GetValue(x), y is null ? null : column.GetValue(y));
    }

    /// <summary>
    /// Keeps a cell's content showing its row: the data context as cells are recycled, and the row's own changes
    /// while the cell is on screen.
    /// </summary>
    private sealed class CellUpdater
    {
        private readonly TableGridColumn _column;
        private readonly Control _element;
        private bool _attached;
        private INotifyPropertyChanged? _row;

        private CellUpdater(TableGridColumn column, Control element)
        {
            _column = column;
            _element = element;
            element.DataContextChanged += (_, _) =>
            {
                Fill(column, element);
                Listen();
            };
            element.AttachedToVisualTree += (_, _) =>
            {
                _attached = true;
                Listen();
            };
            element.DetachedFromVisualTree += (_, _) =>
            {
                _attached = false;
                Listen();
            };
        }

        /// <summary>Fills a new cell and keeps it up to date. The cell's handlers keep the updater alive.</summary>
        public static void Attach(TableGridColumn column, Control element, object? dataItem)
        {
            _ = new CellUpdater(column, element);
            Fill(column, element, dataItem);
        }

        public static void Fill(TableGridColumn column, Control element) => Fill(column, element, element.DataContext);

        private static void Fill(TableGridColumn column, Control element, object? row)
        {
            switch (element)
            {
                case TextBlock text:
                    column.UpdateText(text, row);
                    break;
                case ContentControl host:
                    column.UpdateTemplate(host, row);
                    break;
            }
        }

        private void Listen()
        {
            INotifyPropertyChanged? row = _attached ? _element.DataContext as INotifyPropertyChanged : null;
            if (ReferenceEquals(row, _row))
            {
                return;
            }

            _row?.PropertyChanged -= Row_OnPropertyChanged;
            _row = row;
            _row?.PropertyChanged += Row_OnPropertyChanged;
        }

        private void Row_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // A templated cell's controls follow the row themselves; rebuilding them on every change would lose focus
            if (_element is not TextBlock)
            {
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                Fill(_column, _element);
            }
            else
            {
                Dispatcher.UIThread.Post(() => Fill(_column, _element));
            }
        }
    }
}

/// <summary>A <see cref="TableGrid"/> column of <typeparamref name="T"/> rows.</summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed class TableGridColumn<T> : TableGridColumn
{
    /// <summary>Creates a column.</summary>
    /// <param name="header">The header text.</param>
    /// <param name="value">Gets a row's value, which is formatted for its text and compared for sorting.</param>
    public TableGridColumn(string header, Func<T, object?> value) : base(header)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value;
    }

    /// <summary>Gets a row's value.</summary>
    public Func<T, object?> Value { get; }

    /// <summary>
    /// A row's text, instead of formatting <see cref="Value"/>; e.g. <c>issue =&gt; $"#{issue.Number}"</c>. Null text
    /// shows <see cref="TableGridColumn.NullText"/>.
    /// </summary>
    public Func<T, string?>? Format { get; init; }

    /// <summary>
    /// Builds a row's cell as a control instead of text, e.g. a link button or a colored label. Called each time the
    /// cell shows a different row.
    /// </summary>
    public Func<T, Control>? CellTemplate { get; init; }

    /// <summary>
    /// Style classes for a row's cell (a <c>TextBlock</c>, or the <c>ContentControl</c> around a
    /// <see cref="CellTemplate"/>), space-separated, or null. E.g. <c>issue =&gt; issue.IsOverdue ? "warning" : null</c>.
    /// </summary>
    public Func<T, string?>? CellClass { get; init; }

    /// <summary>A row's text color, or null for the default. Ignored with a <see cref="CellTemplate"/>.</summary>
    public Func<T, IBrush?>? Foreground { get; init; }

    /// <summary>
    /// Makes the column editable: applies the text the user typed to a row, e.g.
    /// <c>(item, text) =&gt; item.Quantity = int.Parse(text)</c>. Throw to reject it: the cell shows the message and
    /// stays in edit. The text box starts with the value formatted with <see cref="TableGridColumn.FormatString"/>
    /// (not <see cref="Format"/>). Null (the default) leaves the column read-only. Needs a <see cref="TableGrid"/>
    /// whose <c>IsReadOnly</c> is false.
    /// </summary>
    public Action<T, string>? Edit { get; init; }

    /// <inheritdoc />
    public override object? GetValue(object row) => row is T item ? Value(item) : null;

    /// <inheritdoc />
    public override string GetText(object row) => row is T item && Format is { } format
        ? format(item) ?? NullText
        : base.GetText(row);

    private protected override bool CanEdit => Edit is not null;

    private protected override bool HasCellTemplate => CellTemplate is not null;

    private protected override Control? BuildCell(object row) => row is T item ? CellTemplate?.Invoke(item) : null;

    private protected override string? GetCellClass(object row) => row is T item ? CellClass?.Invoke(item) : null;

    private protected override IBrush? GetForeground(object row) => row is T item ? Foreground?.Invoke(item) : null;

    private protected override void SetText(object row, string text)
    {
        if (row is T item && Edit is { } edit)
        {
            edit(item, text);
        }
        else
        {
            base.SetText(row, text);
        }
    }
}
