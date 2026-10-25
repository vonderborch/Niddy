# PagedTableSource\<T>

`Niddy.Avalonia.Tables` · Niddy.Avalonia · [source](../../../src/Niddy.Avalonia/Tables/PagedTableSource.cs)

A [`TableSource`](TableSource.md) whose pages come from a function, so the app controls the paging, e.g. a web API or a database query that loads one page at a time. The function gets a [`TablePageRequest`](TableSource.md#tablepagerequest) with the page, page size, cursor and sort, and returns an [`ItemPage<T>`](../../Niddy.Core/Collections/ItemPage.md).

Niddy.Core's `…PageAsync` methods fit it directly:
- [`GitHubIssues.ListPageAsync`](../../Niddy.Core/GitHub/GitHubIssues.md#paging-yourself), `SearchPageAsync` and `ListCommentsPageAsync`;
- [`GitHubPullRequests.ListPageAsync`](../../Niddy.Core/GitHub/GitHubPullRequests.md);
- [`GitHubProjects`' cursor-based ones](../../Niddy.Core/GitHub/GitHubProjects.md#paging-yourself).

## API

`sealed class PagedTableSource<T> : TableSource<T>`

| Member | Description |
|---|---|
| `PagedTableSource(Func<TablePageRequest, CancellationToken, Task<Result<ItemPage<T>>>> loadPage, bool canJumpToPage = true, bool canSort = false)` | Creates a source. `loadPage` is called on the UI thread, so it should await anything slow rather than block. Null throws `ArgumentNullException`. |
| `bool CanJumpToPage` | `canJumpToPage`. Pass false for cursor-based APIs, which can only read the page after one they've read. The table then pages forward with each page's `NextCursor`, and back with the cursors it kept. |
| `bool CanSort` | `canSort`. Pass true if `loadPage` sorts by the request's `SortKey`. Defaults to false, so clicking a header does nothing. For an API that can only sort by some fields, pass true and set `CanUserSort = false` on the other columns. |
| `Refresh()` · `Reset()` | From [`TableSource`](TableSource.md): reload the page, or go back to the first. E.g. call `Reset()` when a search box changes what `loadPage` reads. |

- **Cancellation.** The token is cancelled when the table moves on before the page arrives. Pass it to the API so the request is dropped.
- **Errors.** A failed `Result`, or an exception, shows in the table with a Retry button.

## Example

```csharp
GitHubReadOptions read = new() { Token = token };
TableGrid table = new() { PageSize = 30 };
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("#", issue => issue.Number)
{
    Format = issue => $"#{issue.Number}",
    CanUserSort = false,
});
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Title", issue => issue.Title)
{
    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
    CanUserSort = false,
});
table.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Updated", issue => issue.UpdatedAt) { SortMemberPath = "updated" });

table.Source = new PagedTableSource<GitHubIssueInfo>((request, cancellationToken) =>
{
    GitHubIssueFilter filter = new()
    {
        Sort = request.SortKey == "updated" ? GitHubIssueSort.Updated : GitHubIssueSort.Created,
        Ascending = request.SortKey is not null && !request.SortDescending,
    };
    return GitHubIssues.ListPageAsync("vonderborch/Niddy", filter, request.Page, request.PageSize, read,
        cancellationToken: cancellationToken);
}, canSort: true);
```

More, including a cursor-based project table: [A GitHub table](../../examples/github-table.md).

## See also

- [TableGrid](TableGrid.md), [TableSource](TableSource.md)
- [ItemPage\<T>](../../Niddy.Core/Collections/ItemPage.md)
