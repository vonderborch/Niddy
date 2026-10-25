# TableGridColumn

`Niddy.Avalonia.Tables` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Tables/TableGridColumn.cs)

A `DataGridColumn` whose cells come from a function rather than a binding, formatted for reading: numbers right-aligned, dates in local time, lists joined with commas. Create a `TableGridColumn<T>` for rows of type `T`.

- It works in any `DataGrid`. Editing it needs a [`TableGrid`](TableGrid.md), which applies the edit through the column's `Edit` function.
- Everything a `DataGridColumn` has works as usual: `Header`, `Width`, `MinWidth`, `MaxWidth`, `CanUserSort`, `CanUserResize`, `CanUserReorder`, `SortMemberPath`, `IsVisible`, `DisplayIndex` and so on.
- It's sortable by default (`CanUserSort` is true). In memory it sorts by its values. With a [`TableSource`](TableSource.md), the source sorts, by the column's `SortMemberPath` if set, else its header text.

## API

### TableGridColumn

`abstract class TableGridColumn : DataGridColumn`. It can't be derived from outside Niddy; create a `TableGridColumn<T>`.

| Member | Description |
|---|---|
| `HorizontalAlignment? Alignment` | How cells are aligned. Null (the default) right-aligns numbers and left-aligns everything else. |
| `string? FormatString` | A .NET format string for values that take one, e.g. `N0`, `C`, `P1` or `yyyy-MM-dd`. Values are formatted in the current culture. |
| `string NullText` | The text of a null value. Defaults to empty. |
| `IComparer<object?>? Comparer` | Compares two cells' values (not rows) when the grid sorts its rows in memory by this column. Null (the default) uses the default comparer below. Not used with a `TableSource`, which sorts its own pages. |
| `bool IsReadOnly` | True unless the column has an `Edit` function. Setting it to true makes an editable column read-only; setting it to false doesn't make a column without `Edit` editable. |
| `abstract object? GetValue(object row)` | A row's value in this column. It's used for the cell's text and alignment and for sorting. It's null if `row` isn't the column's row type. |
| `virtual string GetText(object row)` | A row's text: the value formatted as below. |

**How values become text**, when no `Format` is given:
- null shows `NullText`;
- a string shows as it is;
- a `DateTimeOffset` without a `FormatString` shows in local time, short date and time (`g`); a `DateTime` shows the same way, unconverted;
- anything else formattable (numbers, dates with a `FormatString`, enums, `TimeSpan`, `Guid`) uses `FormatString` and the current culture;
- a list (any `IEnumerable` except a string) is joined with ", ", each item formatted the same way;
- anything else uses `ToString()`.

**The default comparer** puts nulls first. It compares strings in the current culture, ignoring case, and numbers by value even across types (an `int` against a `double`). Other values of the same type use `IComparable`, and anything else is compared by its text.

### TableGridColumn\<T>

`sealed class TableGridColumn<T>(string header, Func<T, object?> value) : TableGridColumn`. The properties below are `init`-only; the base class's and `DataGridColumn`'s can be set at any time.

| Member | Description |
|---|---|
| `Func<T, object?> Value` | Gets a row's value. It's formatted for the text and compared for sorting. |
| `Func<T, string?>? Format` | A row's text, used instead of formatting `Value`, e.g. `issue => $"#{issue.Number}"`. Sorting still uses `Value`. Null text shows `NullText`. |
| `Func<T, Control>? CellTemplate` | Builds a row's cell as a control instead of text, e.g. a link button, a colored badge or a progress bar. It's called each time the cell shows a different row, because the grid recycles cells as it scrolls and pages. |
| `Func<T, string?>? CellClass` | Style classes for a row's cell, space-separated, or null, e.g. `order => order.IsOverdue ? "warning" : null`. They go on the cell's `TextBlock`, or on the `ContentControl` around a `CellTemplate`. |
| `Func<T, IBrush?>? Foreground` | A row's text color, or null for the default. Ignored with a `CellTemplate`. |
| `Action<T, string>? Edit` | Makes the column editable: applies the text the user typed to a row, e.g. `(line, text) => line.Quantity = int.Parse(text)`. Null (the default) leaves the column read-only. |

- **Editing.**
  - The cell is edited in a `TextBox` that starts with the value formatted with `FormatString` (not `Format`). F2 puts the caret at the end; any other way into edit selects all the text.
  - Committing calls `Edit`. If it throws, the text box shows the exception's message and the cell stays in edit.
  - Escape puts the text back.
  - It needs a `TableGrid` whose `IsReadOnly` is false, and the row unlocked if the grid has `LockRows` on.
- **Live cells.** A row that raises `INotifyPropertyChanged.PropertyChanged` refreshes its cells' text, so an edit or a change made elsewhere shows straight away.
- **Mixed rows.** A row that isn't a `T` gets a null value, so it shows `NullText`.

## Example

```csharp
TableGrid table = new();
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("#", issue => issue.Number)
{
    Format = issue => $"#{issue.Number}",
    Width = new DataGridLength(70),
});
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Title", issue => issue.Title)
{
    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
    MinWidth = 200,
});
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Labels", issue => issue.Labels) { NullText = "—" });
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Comments", issue => issue.CommentCount) { FormatString = "N0" });
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Updated", issue => issue.UpdatedAt)
{
    FormatString = "yyyy-MM-dd",
    CellClass = issue => issue.UpdatedAt < DateTimeOffset.Now.AddMonths(-6) ? "stale" : null,
});
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("State", issue => issue.State)
{
    CanUserSort = false,
    CellTemplate = issue => new Border
    {
        Background = issue.State == GitHubItemState.Open ? Brushes.SeaGreen : Brushes.MediumPurple,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(6, 1),
        HorizontalAlignment = HorizontalAlignment.Left,
        Child = new TextBlock { Text = issue.State.ToString(), Foreground = Brushes.White },
    },
});
```

```xml
<!-- In the window's or app's styles -->
<Style Selector="DataGridCell TextBlock.stale">
    <Setter Property="Opacity" Value="0.6" />
</Style>
```

## See also

- [TableGrid](TableGrid.md), [TableSource](TableSource.md)
