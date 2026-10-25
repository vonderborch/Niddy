# TableGrid

`Niddy.Avalonia.Tables` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Tables/TableGrid.cs) · [pager XAML](../../../src/Niddy.Avalonia/Tables/TableGridPager.axaml)

A `DataGrid` with three additions. Everything a DataGrid does works the same way: `ItemsSource`, `Columns`, `AutoGenerateColumns`, selection, sorting, `CollectionView`, frozen columns, row details, grid lines, styles and templates. The additions:
- **Read-only by default.** `IsReadOnly` defaults to true. Set it to false to edit, as with any DataGrid.
- **Row locking**, optional. With `LockRows`, an editable table keeps each row locked until it's unlocked. By default each row gets a lock button in a column added for it.
- **Paging.** A pager is added under the rows. It has First, Previous, a page box, "of N", Next, Last, a rows-per-page box, "61–90 of 352" and Refresh.

Rows come from either place:
- **`ItemsSource`**, as with any DataGrid. The rows are paged in memory. Sorting (clicking a header) and filtering (`CollectionView.Filter`) apply to all rows, not just the page shown. An `ObservableCollection` updates the counts as it changes.
- **[`Source`](TableSource.md)**, for rows loaded a page at a time, e.g. from a web API or a database query. The source loads each page, sorted, when the table asks. A progress bar shows while a page loads, and a failure shows its message with a Retry button. Use [`PagedTableSource<T>`](PagedTableSource.md) for a function that loads a page, or your own `TableSource<T>` subclass.

Columns can be any `DataGridColumn` (`DataGridTextColumn`, `DataGridCheckBoxColumn`, `DataGridTemplateColumn` and so on). A [`TableGridColumn<T>`](TableGridColumn.md) gets its cells from a function instead of a binding, and formats them for reading.

**The name.** Avalonia 12.1 has its own `Avalonia.Controls.TableView`. This one is `TableGrid` so the two don't clash in files that use both namespaces.

## API

`partial class TableGrid : DataGrid`. Only the members it adds or changes are listed; the rest are [DataGrid's](https://docs.avaloniaui.net/docs/reference/controls/datagrid/).

### Paging

| Member | Description |
|---|---|
| `TableGrid()` | Creates an empty, read-only table. |
| `TableSource? Source` | Where the rows come from when they're loaded a page at a time, or null (the default) to use `ItemsSource`. Setting it sets `ItemsSource` to the page shown and goes to the first page. Setting `ItemsSource` clears it. |
| `int PageSize` | Rows per page. Defaults to 25 and must be at least 1. Changing it keeps the first row shown on screen, except for a source that can't jump to pages, which goes back to the first page. |
| `IReadOnlyList<int> PageSizes` | The choices in the rows-per-page box. Defaults to 10, 25, 50 and 100. `PageSize` is added if it's missing. |
| `bool ShowPager` | Whether the pager shows. Defaults to true. Without it, rows from `ItemsSource` aren't paged: they all show. A `Source` still loads a page at a time, and `GoToPageAsync` moves between pages. |
| `bool ShowPageSizes` | Whether the pager has the rows-per-page box. Defaults to true. |
| `string EmptyText` | What shows over the rows when there are none. Defaults to "Nothing to show." Empty shows nothing. |
| `int Page` | The page shown, from 1. Read-only; use `GoToPageAsync`. |
| `int? PageCount` | How many pages there are. Null while a `Source` hasn't said; it's worked out once the last page has loaded. |
| `int? TotalCount` | How many rows there are across all pages, or null if the `Source` doesn't say. With `ItemsSource`, it's the rows left after filtering. |
| `bool HasMore` | Whether there's a page after this one. |
| `bool IsLoading` | Whether a page is loading from the `Source`. |
| `Error? Error` | Why the last load from the `Source` failed, or null if it didn't. |
| `Task RefreshAsync()` | Shows the rows again. A `Source` loads the page shown again, and rows from `ItemsSource` are sorted and filtered again. |
| `Task GoToPageAsync(int page)` | Goes to a page, committing any edit first. Pages under 1 go to the first page, and past `PageCount` to the last. The task is already complete for rows from `ItemsSource`. |

The properties above, and `LockRows` and `ShowLockButtons` below, are Avalonia properties (`SourceProperty`, `PageSizeProperty`, `PageProperty` and so on), so they can be bound and styled. `Page`, `PageCount`, `TotalCount`, `HasMore`, `IsLoading` and `Error` are read-only.

### Editing and locking

| Member | Description |
|---|---|
| `bool IsReadOnly` | DataGrid's own, but defaults to true. |
| `bool LockRows` | Whether rows are locked until unlocked, when the table can be edited. A locked row's cells don't go into edit mode. Defaults to false. Turning it on or off locks every row. |
| `bool ShowLockButtons` | Whether `LockRows` adds `LockColumn`. Defaults to true. Turn it off to lock and unlock rows from code, e.g. from your own button. |
| `DataGridColumn LockColumn` | The column of lock buttons. It's added as the first column while the table is editable and `LockRows` and `ShowLockButtons` are on, and removed otherwise. Move it with its `DisplayIndex`, or change its `Width`. |
| `Func<object, bool>? CanUnlockRow` | Whether a row may be unlocked, or null (the default) to allow every row. A row it refuses keeps a disabled lock button, and `UnlockRow` returns false for it. |
| `bool IsRowUnlocked(object item)` | Whether a row has been unlocked and not locked again. |
| `bool UnlockRow(object item)` | Unlocks a row, as its lock button does. Returns false if `CanUnlockRow` refused it. |
| `void LockRow(object item)` | Locks a row again, committing its edit, or cancelling it if it doesn't commit. |
| `void LockAllRows()` | Locks every row, committing any edit first. |

### Rows

| Member | Description |
|---|---|
| `Func<object, string?>? RowClass` | Style classes for a row, space-separated, or null, e.g. `item => ((Issue)item).IsClosed ? "muted" : null`. Applied as rows load, so set it before the rows, or call `RefreshRowClasses` after. Unlocked rows also have the `unlocked` class. |
| `void RefreshRowClasses()` | Applies `RowClass` to the rows shown again, e.g. after the rows' items changed. |
| `event EventHandler<TableGridItemEventArgs>? ItemActivated` | A row was double-tapped, or Enter was pressed on the selected row. Not raised while a cell is being edited, or for Enter on a row that can be edited, where Enter moves on as in any DataGrid. |

### Behavior

- **Paging from `ItemsSource`** uses the grid's own `DataGridCollectionView` paging. `CollectionView` is the view of all rows, and enumerating it gives the page shown. If rows are removed from under the page shown, the table moves to the new last page.
- **Paging from a `Source`:**
  - Next stays on while `HasMore` is true.
  - Last needs a known `PageCount` and a source that can jump to pages.
  - An empty page past the end (e.g. after rows were removed) shows the last page instead.
  - A cursor-based source (`CanJumpToPage` false) is read forward from the last page the table has a cursor for. Going back reuses the cursors it kept, and the page box is read-only.
- **Loading never throws.** `RefreshAsync` and `GoToPageAsync` complete when the page has loaded, has failed, or has been replaced by a newer load. A failed `Result`, or an exception from the source, becomes `Error` and the Retry button.
- **Loads are coalesced.** Setting `Source`, `PageSize` and a sort together loads once. A load that is overtaken has its `CancellationToken` cancelled, and its result is dropped.
- **Sorting with a `Source`** happens in the source. Clicking a header goes to the first page, and asks the source for it with the column in [`TablePageRequest.SortColumn`](TableSource.md#tablepagerequest) and its sort key in `SortKey`: the column's `SortMemberPath`, else its binding's path, else its header text. The table shows the rows in the order the source returned them. A source whose `CanSort` is false ignores header clicks. To sort by only some columns, set `CanUserSort = false` on the others.
- **Source changes.** `source.Refresh()` reloads the page, and `source.Reset()` goes back to the first page. Both can be called from any thread. The table only listens while it's attached; a change made while it was detached reloads when it's attached again.
- **Locks** are kept by item. Rows from `ItemsSource` stay unlocked across pages until `ItemsSource` changes. With a `Source`, every page load locks all rows, because the items are new.
- **Editing.** Bound columns (`DataGridTextColumn` and so on) edit as in any DataGrid. A `TableGridColumn<T>` edits through its `Edit` function, and is read-only without one.
- **Generated columns.** With `AutoGenerateColumns="True"`, property names are split into words for the headers ("CreatedAt" becomes "Created At"). A `Source`'s pages are typed, so the grid sees the row type before any rows load.
- **The DataGrid theme is needed.** [`NiddyApp`](../Hosting/NiddyApp.md) adds it. Without `NiddyApp`, add `<StyleInclude Source="avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"/>` to `App.axaml`'s styles. Without it, the grid has no template and shows no rows. The table uses DataGrid's theme as it is (`StyleKeyOverride`), so `DataGrid` styles apply to it too.
- **Threads.** Everything runs on the UI thread, sources' `LoadPageAsync` included. Await slow work rather than blocking.
- **Styling.**
  - Rows and cells take normal styles: `DataGridRow.muted` for a `RowClass` of `muted`, `DataGridRow.unlocked` for unlocked rows, and `TextBlock.warning` for a column's `CellClass` of `warning`.
  - The lock button is a `Button#LockButton`, with the `unlocked` class while its row is unlocked.

### TableGridItemEventArgs

`sealed class TableGridItemEventArgs(object item) : EventArgs`: the row of an `ItemActivated` event.

| Member | Description |
|---|---|
| `object Item` | The row's item. |

## Examples

From XAML, binding a collection and generating the columns:

```xml
<UserControl xmlns:niddy="https://github.com/vonderborch/Niddy">
    <niddy:TableGrid ItemsSource="{Binding Orders}" AutoGenerateColumns="True" PageSize="50"
                     SelectedItem="{Binding SelectedOrder}" EmptyText="No orders yet." />
</UserControl>
```

In memory, with columns:

```csharp
TableGrid table = new() { PageSize = 50 };
table.Columns.Add(new TableGridColumn<Order>("Order", order => order.Number) { Format = order => $"#{order.Number}" });
table.Columns.Add(new TableGridColumn<Order>("Customer", order => order.Customer)
{
    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
});
table.Columns.Add(new TableGridColumn<Order>("Total", order => order.Total) { FormatString = "C" });
table.Columns.Add(new TableGridColumn<Order>("Placed", order => order.PlacedAt) { FormatString = "yyyy-MM-dd" });
table.RowClass = item => ((Order)item).IsCancelled ? "muted" : null;
table.ItemsSource = orders;
table.ItemActivated += (_, e) => OpenOrder((Order)e.Item);

// Show only open orders; the counts and pages follow.
table.CollectionView!.Filter = item => !((Order)item).IsCancelled;
```

Editable, with each row unlocked by its lock button:

```xml
<niddy:TableGrid ItemsSource="{Binding Lines}" IsReadOnly="False" LockRows="True" AutoGenerateColumns="False">
    <niddy:TableGrid.Columns>
        <DataGridTextColumn Header="Product" Binding="{Binding Product}" IsReadOnly="True" />
        <DataGridTextColumn Header="Quantity" Binding="{Binding Quantity}" />
        <DataGridCheckBoxColumn Header="Urgent" Binding="{Binding IsUrgent}" />
    </niddy:TableGrid.Columns>
</niddy:TableGrid>
```

The same from code, with a `TableGridColumn<T>` that edits and shipped lines that can't be unlocked:

```csharp
TableGrid lines = new() { IsReadOnly = false, LockRows = true, ItemsSource = orderLines };
lines.Columns.Add(new TableGridColumn<OrderLine>("Product", line => line.Product));
lines.Columns.Add(new TableGridColumn<OrderLine>("Quantity", line => line.Quantity)
{
    // Throwing rejects the edit: the cell shows the message and stays in edit.
    Edit = (line, text) => line.Quantity = int.Parse(text, CultureInfo.CurrentCulture),
});
lines.CanUnlockRow = item => !((OrderLine)item).IsShipped;
```

Pages loaded by the app, here a database query:

```csharp
TableGrid table = new() { PageSize = 50 };
table.Columns.Add(new TableGridColumn<Order>("Placed", order => order.PlacedAt) { SortMemberPath = "placed" });
table.Columns.Add(new TableGridColumn<Order>("Total", order => order.Total) { FormatString = "C", SortMemberPath = "total" });
table.Columns.Add(new TableGridColumn<Order>("Customer", order => order.Customer) { CanUserSort = false });

table.Source = new PagedTableSource<Order>(async (request, cancellationToken) =>
{
    IQueryable<Order> query = request.SortKey switch
    {
        "total" => request.SortDescending ? db.Orders.OrderByDescending(o => o.Total) : db.Orders.OrderBy(o => o.Total),
        "placed" when !request.SortDescending => db.Orders.OrderBy(o => o.PlacedAt),
        _ => db.Orders.OrderByDescending(o => o.PlacedAt),
    };
    int total = await query.CountAsync(cancellationToken);
    List<Order> rows = await query.Skip(request.Skip).Take(request.PageSize).ToListAsync(cancellationToken);
    return new ItemPage<Order>
    {
        Items = rows,
        HasMore = request.Skip + rows.Count < total,
        TotalCount = total,
        PageCount = Math.Max(1, (total + request.PageSize - 1) / request.PageSize),
    };
}, canSort: true);
```

For GitHub issues and project items, see [A GitHub table](../../examples/github-table.md).

## See also

- [TableGridColumn](TableGridColumn.md), [TableSource](TableSource.md), [PagedTableSource\<T>](PagedTableSource.md)
- [ItemPage\<T>](../../Niddy.Core/Collections/ItemPage.md)
- [`Dialog.Table`](../Dialogs/Dialog.md#built-in-dialogs), for a table in a dialog
