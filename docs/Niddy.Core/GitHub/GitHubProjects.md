# GitHubProjects

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubProjects.cs)

Reads GitHub projects (the current kind, Projects v2) through GitHub's GraphQL API:
- a user's or organization's projects, or the ones linked to a repository;
- one project;
- its items (issues, pull requests and drafts) with their field values, such as Status;
- its fields, with single-select options and iterations, e.g. to lay out a board's columns.

It's read-only. Classic projects were retired by GitHub and aren't supported.

**A token is required.** GitHub's GraphQL API needs one even for public projects, so every method takes [`GitHubReadOptions`](GitHubReadOptions.md) with a `Token`:
- a fine-grained token needs the **Projects: Read** permission on the user or organization that owns the project, plus **Issues** and **Pull requests: Read** on the repositories whose items it should see;
- a classic token needs the `read:project` scope (and `repo` for private repositories' items).

Items the token can't see come back as `Redacted`, with no title, number or link.

## API

| Member | Description |
|---|---|
| `static Task<Result<IReadOnlyList<GitHubProject>>> ListAsync(string owner, GitHubReadOptions options, int maxCount = 30, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | A user's or organization's projects, most recently updated first, closed ones included. An owner that doesn't exist is `NotFound`. |
| `static Task<Result<IReadOnlyList<GitHubProject>>> ListForRepositoryAsync(string repository, GitHubReadOptions options, int maxCount = 30, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | The projects linked to a repository (its Projects tab), in the same order. Projects belong to a user or organization; this is the ones the repository shows. |
| `static Task<Result<GitHubProject>> GetAsync(string owner, int number, GitHubReadOptions options, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One project, by its owner and number: the `5` in `github.com/orgs/acme/projects/5`. |
| `static Task<Result<IReadOnlyList<GitHubProjectItem>>> ListItemsAsync(string owner, int number, GitHubReadOptions options, int maxCount = 100, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | A project's items, in the project's own order (not a view's), with up to 50 field values each. Archived items may be included; check `IsArchived`. |
| `static Task<Result<IReadOnlyList<GitHubProjectField>>> ListFieldsAsync(string owner, int number, GitHubReadOptions options, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | A project's fields (up to 100), built-in ones such as Title, Assignees and Status included. |
| `static Task<Result<ItemPage<GitHubProject>>> ListPageAsync(string owner, GitHubReadOptions options, int pageSize = 30, string? cursor = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `ListAsync`'s projects, for an app that pages them itself. See [Paging yourself](#paging-yourself). |
| `static Task<Result<ItemPage<GitHubProject>>> ListForRepositoryPageAsync(string repository, GitHubReadOptions options, int pageSize = 30, string? cursor = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `ListForRepositoryAsync`'s projects. |
| `static Task<Result<ItemPage<GitHubProjectItem>>> ListItemsPageAsync(string owner, int number, GitHubReadOptions options, int pageSize = 30, string? cursor = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `ListItemsAsync`'s items. |
| `List(…)` · `ListForRepository(…)` · `Get(…)` · `ListItems(…)` · `ListFields(…)` · `ListPage(…)` · `ListForRepositoryPage(…)` · `ListItemsPage(…)` | Synchronous versions, with the same parameters. They work where [`WebContent`'s](../Web/WebContent.md#synchronous-versions) do: not in the browser, and only with a handler that supports synchronous sends (the shared client does, the native Android and iOS handlers don't). |

- **Arguments.** A blank `owner`, a `repository` that isn't `owner/name`, or a blank token throws `ArgumentException`; null `options` throws `ArgumentNullException`; a `number` or `maxCount` under 1, or a `pageSize` outside 1 to 100, throws `ArgumentOutOfRangeException`. Nothing is sent first.
- **Paging.** The lists read up to 100 per request and follow GraphQL's cursors for as many pages as `maxCount` takes. To page in your own UI instead, use the `…PageAsync` methods (see [Paging yourself](#paging-yourself)).
- **`httpClient`.** Null uses a shared client and `https://api.github.com/graphql`. If yours has a `BaseAddress`, that's the REST API address and the GraphQL one is worked out from it: `https://github.example.com/api/v3/` (GitHub Enterprise Server) sends to `https://github.example.com/api/graphql`, and any other address to `graphql` under it.
- **Cancelling** `cancellationToken` throws `OperationCanceledException`. Everything else that goes wrong is a failed `Result` with one of the [`ErrorCodes`](#errorcodes).
- **Caching** works as [`GitHubReadOptions`](GitHubReadOptions.md#the-cache) describes, keyed by the query and its variables. GraphQL has no `ETag`s, so a response older than `CacheDuration` is always fetched again.
- **Rate limit.** GraphQL counts the cost of each query rather than requests: 5,000 points an hour per token. Reading a few hundred items costs a handful of points.

### Paging yourself

`ListPageAsync`, `ListForRepositoryPageAsync` and `ListItemsPageAsync` read one page and return an [`ItemPage<T>`](../Collections/ItemPage.md). They suit a UI with real pages, such as a [`TableGrid`](../../Niddy.Avalonia/Tables/TableGrid.md) through a [`PagedTableSource<T>`](../../Niddy.Avalonia/Tables/PagedTableSource.md) created with `canJumpToPage: false`.

- **Cursors, not page numbers.** GraphQL reads the page after one it has read: pass null for the first page, then the previous page's `NextCursor`. `NextCursor` is null on the last page. Pages can't be jumped to; going back means keeping the cursors you've used, which `TableGrid` does for you.
- **Counts.** `TotalCount` and `PageCount` are GitHub's, so a pager can still say "page 2 of 7".
- **`pageSize`** is 1 to 100; anything else throws `ArgumentOutOfRangeException`. The token is required, as for the other methods.

### GitHubProject

`sealed record GitHubProject`: a project, as `ListAsync`, `ListForRepositoryAsync` and `GetAsync` read it.

| Member | Description |
|---|---|
| `required string Id` | GitHub's node ID for the project, e.g. `PVT_kwDOAB…`. |
| `required int Number` | The project number, which the other methods take. |
| `required string Title` | The title. |
| `string? Description` | The short description, or null if it has none. |
| `Uri? Url` | The project's page on GitHub. |
| `string? Owner` | The login of the user or organization that owns it. |
| `bool IsClosed` | Whether it's closed. |
| `bool IsPublic` | Whether anyone can see it. |
| `int ItemCount` | How many items it has, archived ones included. |
| `DateTimeOffset? CreatedAt` · `UpdatedAt` | When it was created, and last updated. |

### GitHubProjectItem

`sealed record GitHubProjectItem`: an item on a project, as `ListItemsAsync` reads it.

| Member | Description |
|---|---|
| `required string Id` | GitHub's node ID for the item. It's the item's, not the issue's. |
| `GitHubProjectItemType Type` | What it is (below). |
| `string? Title` | The title, or null for a `Redacted` item. |
| `string? Body` | A draft issue's body, in Markdown. Issues' and pull requests' bodies aren't read; use [`GitHubIssues.GetAsync`](GitHubIssues.md#reading-issues) for those. |
| `int? Number` · `Uri? Url` · `string? Repository` | The issue's or pull request's number, page and `owner/name`. Null for a draft. |
| `GitHubItemState? State` | `Open`, `Closed` or `Merged` (pull requests only). Null for a draft. |
| `bool IsArchived` | Whether the item is archived in the project. Check it to leave archived items out. |
| `IReadOnlyDictionary<string, string> Fields` | The item's field values by field name, ignoring case, for the fields that have one: text, numbers (formatted with the invariant culture, e.g. `2.5`), dates (`yyyy-MM-dd`), single-select options and iteration titles. The built-in Title field is here too. Fields without a single value, such as assignees and labels, aren't. |
| `string? Status` | The Status field's value, e.g. `Todo` or `In Progress`, or null if it has none. |
| `string? GetField(string name)` | A field's value by its name, ignoring case, or null. |

`GitHubProjectItemType`:

| Value | Means |
|---|---|
| `Issue` | An issue. |
| `PullRequest` | A pull request. |
| `DraftIssue` | A draft issue, which lives only in the project. |
| `Redacted` | An issue or pull request the token can't see, so it has no title, number or link. Also anything newer GitHub adds. |

### GitHubProjectField

`sealed record GitHubProjectField`: a field on a project, as `ListFieldsAsync` reads it.

| Member | Description |
|---|---|
| `required string Id` | GitHub's node ID for the field. |
| `required string Name` | The field's name, e.g. `Status`. Item `Fields` are keyed by it. |
| `string DataType` | Its type as GitHub names it, e.g. `TEXT`, `NUMBER`, `DATE`, `SINGLE_SELECT`, `ITERATION`, or a built-in one such as `TITLE`, `ASSIGNEES` or `LABELS`. A string, since GitHub adds types. |
| `IReadOnlyList<string> Options` | A single-select field's option names, or an iteration field's iteration titles (current and upcoming, then completed), in the project's order. Empty for other fields. |

### ErrorCodes

`GitHubProjects.ErrorCodes` holds the `Error.Code` values the reads fail with. They're the same strings as [`GitHubIssues.ErrorCodes`](GitHubIssues.md#errorcodes)'. GraphQL reports most errors in a `200` response; their message ends with GitHub's, e.g. `GitHub said: Could not resolve to a ProjectV2 with the number 9.`

| Constant | When |
|---|---|
| `Unauthorized` | 401: the token is invalid or expired. |
| `Forbidden` | The token can't read the project: it lacks the Projects permission or the `read:project` scope. |
| `RateLimited` | GitHub's rate limits, with nothing cached to fall back on. Try again later. |
| `NotFound` | The owner, repository or project doesn't exist, or the token can't see it. |
| `Failed` | Any other status or GraphQL error, or a success response that couldn't be read. |
| `Network` | No response (no connection, a DNS failure or a timeout), with nothing cached to fall back on. |

## Examples

```csharp
using Niddy.Core.GitHub;
using Niddy.Core.Results;

GitHubReadOptions read = new() { Token = token, CacheDirectory = Path.Combine(paths.Cache, "github") };

// An organization's open projects
Result<IReadOnlyList<GitHubProject>> projects = await GitHubProjects.ListAsync("acme", read);
foreach (GitHubProject project in (projects.Value ?? []).Where(p => !p.IsClosed))
    Console.WriteLine($"#{project.Number} {project.Title}: {project.ItemCount} items");

// A board: columns from the Status field's options, cards from the items
Result<IReadOnlyList<GitHubProjectField>> fields = await GitHubProjects.ListFieldsAsync("acme", 5, read);
Result<IReadOnlyList<GitHubProjectItem>> items = await GitHubProjects.ListItemsAsync("acme", 5, read, maxCount: 500);
if (fields.TryGetValue(out IReadOnlyList<GitHubProjectField> all, out Error? error) && items.IsSuccess)
{
    GitHubProjectField? status = all.FirstOrDefault(f => f.Name.Equals("Status", StringComparison.OrdinalIgnoreCase));
    foreach (string column in status?.Options ?? [])
    {
        Console.WriteLine(column);
        foreach (GitHubProjectItem item in items.Value!.Where(i => !i.IsArchived && i.Status == column))
            Console.WriteLine($"  {item.Title} {(item.Number is int n ? $"({item.Repository}#{n})" : "(draft)")}");
    }
}
else
{
    Console.WriteLine((error ?? items.Error)!.Message);
}

// A custom field, and the projects a repository shows
string? priority = items.Value?.FirstOrDefault()?.GetField("Priority");
Result<IReadOnlyList<GitHubProject>> linked = await GitHubProjects.ListForRepositoryAsync("acme/app", read);

// A console tool, synchronously
Result<GitHubProject> roadmap = GitHubProjects.Get("acme", 5, read);
```

## See also

- [GitHubReadOptions](GitHubReadOptions.md): the token and cache
- [GitHubIssues](GitHubIssues.md), [GitHubPullRequests](GitHubPullRequests.md): what the items point to
- [Result](../Results/Result.md), [Error](../Results/Error.md)
