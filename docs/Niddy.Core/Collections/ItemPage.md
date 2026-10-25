# ItemPage\<T>

`Niddy.Core.Collections` · Niddy.Core · [source](../../../src/Niddy.Core/Collections/ItemPage.cs)

One page of a longer list, for an app that does its own paging: the page's items, and what's known about the rest. Sources differ in what they can say:
- some know the total;
- some only know whether there's another page;
- cursor-based ones, such as GraphQL APIs, need a `NextCursor` to read the next page.

The `…PageAsync` methods on [`GitHubIssues`](../GitHub/GitHubIssues.md#paging-yourself), [`GitHubPullRequests`](../GitHub/GitHubPullRequests.md) and [`GitHubProjects`](../GitHub/GitHubProjects.md#paging-yourself) return one. It's also what a [`TableGrid`](../../Niddy.Avalonia/Tables/TableGrid.md)'s source returns for each page.

## API

`sealed record ItemPage<T>`

| Member | Description |
|---|---|
| `required IReadOnlyList<T> Items` | The page's items, in order. Can be empty, e.g. past the end. |
| `bool HasMore` | Whether there's a page after this one. |
| `string? NextCursor` | For cursor-based sources, what to pass to read the next page. It's null on the last page, and for sources that are read by page number. |
| `int? TotalCount` | How many items there are across all pages, or null if the source doesn't say. |
| `int? PageCount` | How many pages there are at this page size, or null if the source doesn't say. |
| `static ItemPage<T> Empty` | An empty last page: no items, `PageCount` 1, `TotalCount` 0. |

- **Set what you know.** Leave `TotalCount` and `PageCount` null rather than guessing. A `TableGrid` shows "page 3" and keeps Next on while `HasMore` is true, then works out the page count when it reaches the last page.
- **A short page isn't the last page.** Some sources filter after reading, so only `HasMore` says whether there's more. `GitHubIssues.ListPageAsync`, for example, leaves out pull requests.

## Example

```csharp
// Read every page of a repository's open issues, 50 at a time
int page = 1;
while (true)
{
    Result<ItemPage<GitHubIssueInfo>> result = await GitHubIssues.ListPageAsync("vonderborch/Niddy",
        page: page, pageSize: 50);
    if (!result.TryGetValue(out ItemPage<GitHubIssueInfo> issues, out Error? error))
    {
        Console.WriteLine(error!.Message);
        break;
    }

    foreach (GitHubIssueInfo issue in issues.Items)
    {
        Console.WriteLine($"#{issue.Number} {issue.Title}");
    }

    if (!issues.HasMore)
    {
        break;
    }

    page++;
}
```

Your own paged source:

```csharp
public async Task<Result<ItemPage<Order>>> GetOrdersAsync(int page, int pageSize, CancellationToken cancellationToken)
{
    int total = await _db.Orders.CountAsync(cancellationToken);
    List<Order> orders = await _db.Orders.OrderByDescending(order => order.PlacedAt)
        .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    return new ItemPage<Order>
    {
        Items = orders,
        HasMore = page * pageSize < total,
        TotalCount = total,
        PageCount = Math.Max(1, (total + pageSize - 1) / pageSize),
    };
}
```

## See also

- [TableGrid](../../Niddy.Avalonia/Tables/TableGrid.md) and [PagedTableSource\<T>](../../Niddy.Avalonia/Tables/PagedTableSource.md): show pages like these in a table
- [Result](../Results/Result.md)
