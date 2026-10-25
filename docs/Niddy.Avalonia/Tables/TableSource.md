# TableSource

`Niddy.Avalonia.Tables` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Tables/TableSource.cs) · [TablePageRequest](../../../src/Niddy.Avalonia/Tables/TablePageRequest.cs)

Where a [`TableGrid`](TableGrid.md) gets its rows when they're loaded a page at a time, e.g. from a web API or a database query. The table asks for a page with a `TablePageRequest`, and the source returns an [`ItemPage<T>`](../../Niddy.Core/Collections/ItemPage.md). Set it as the table's `Source`.

Rows already in memory don't need one: set the table's `ItemsSource`, and it pages, sorts and filters them itself.

Usually you don't derive from it: [`PagedTableSource<T>`](PagedTableSource.md) wraps a function that loads a page. Derive from `TableSource<T>` for a source with its own state, e.g. one that keeps filters and a connection. One source can feed several tables at once.

## API

### TableSource

`abstract class TableSource`. It can't be derived from directly; derive from `TableSource<T>`.

| Member | Description |
|---|---|
| `virtual bool CanJumpToPage` | Whether any page can be loaded directly. Defaults to true. Return false for cursor-based sources: the table then reads forward from the last page it has a cursor for, its page box is read-only, and Last is off. |
| `virtual bool CanSort` | Whether the source sorts by the request's `SortKey`. When false, clicking a column header does nothing. To sort by only some columns, leave it true and set `CanUserSort = false` on the others. Defaults to true. |
| `void Refresh()` | Tells the tables showing this source to load their current page again, e.g. after rows changed. Callable from any thread. |
| `void Reset()` | Tells the tables to go back to the first page, e.g. after a filter changed. Callable from any thread. |

### TableSource\<T>

`abstract class TableSource<T> : TableSource`

| Member | Description |
|---|---|
| `protected abstract Task<Result<ItemPage<T>>> LoadPageAsync(TablePageRequest request, CancellationToken cancellationToken)` | Loads one page. It's called on the UI thread, so await anything slow rather than blocking. `cancellationToken` is cancelled when the table moves on, e.g. to another page, before this one loads. |

What the table does with the result:
- **A failed `Result`** shows its message in the table, with a Retry button. So does an exception, other than a cancellation.
- **The items** are shown in order. An empty page past the end makes the table show the last page instead.
- **`HasMore`** turns Next on.
- **`NextCursor`** is passed back as the next page's `Cursor`.
- **`TotalCount`** fills in the "1–25 of 352" range.
- **`PageCount`** sets "of N" and turns Last on. Without it, the table works out the count when it reaches the last page.

### TablePageRequest

`sealed record TablePageRequest`: what the table asks for.

| Member | Description |
|---|---|
| `required int Page` | The page, from 1. |
| `required int PageSize` | How many rows a full page has. |
| `int Skip` | How many rows come before this page: `(Page - 1) * PageSize`. |
| `string? Cursor` | For cursor-based sources, the `NextCursor` the page before this one returned. Null for the first page. |
| `DataGridColumn? SortColumn` | The column the user sorted by, or null to keep the source's own order. Also null when the sort was set in code (through `CollectionView.SortDescriptions`) for a path no column has; `SortKey` still says what to sort by. |
| `string? SortKey` | What to sort by, or null to keep the source's own order: the sort column's `SortMemberPath`, else its binding's path, else its header text. Set `SortMemberPath` to the API's sort field. |
| `bool SortDescending` | Whether to sort descending. |

## Example

A source with a search box's text as its filter:

```csharp
public sealed class CustomerSource(CustomerApi api) : TableSource<Customer>
{
    private string _search = "";

    public override bool CanSort => false;

    public string Search
    {
        get => _search;
        set
        {
            _search = value;
            Reset();
        }
    }

    protected override async Task<Result<ItemPage<Customer>>> LoadPageAsync(TablePageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            CustomerPage page = await api.FindAsync(_search, request.Skip, request.PageSize, cancellationToken);
            return new ItemPage<Customer>
            {
                Items = page.Customers,
                HasMore = request.Skip + page.Customers.Count < page.Total,
                TotalCount = page.Total,
                PageCount = Math.Max(1, (page.Total + request.PageSize - 1) / request.PageSize),
            };
        }
        catch (HttpRequestException exception)
        {
            return Error.FromException(exception);
        }
    }
}
```

## See also

- [TableGrid](TableGrid.md), [PagedTableSource\<T>](PagedTableSource.md)
- [ItemPage\<T>](../../Niddy.Core/Collections/ItemPage.md), [Result](../../Niddy.Core/Results/Result.md)
