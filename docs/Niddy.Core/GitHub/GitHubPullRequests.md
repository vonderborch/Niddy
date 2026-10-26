# GitHubPullRequests

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubPullRequests.cs)

Reads a repository's pull requests through GitHub's REST API: a filtered list, or one pull request with its counts and merge state. It's read-only. Two things live elsewhere:
- a pull request's conversation comments come from [`GitHubIssues.ListCommentsAsync`](GitHubIssues.md#reading-issues);
- searching goes through [`GitHubIssues.SearchAsync`](GitHubIssues.md#reading-issues) with `is:pr`, e.g. `repo:owner/name is:pr is:open review-requested:octocat`.

## API

| Member | Description |
|---|---|
| `static Task<Result<IReadOnlyList<GitHubPullRequest>>> ListAsync(string repository, GitHubPullRequestFilter? filter = null, int maxCount = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | A repository's pull requests (`GET /repos/{owner}/{repo}/pulls`). Null `filter` is open ones, newest first. It reads up to 100 per request, as many pages as `maxCount` takes. The counts and `IsMergeable` are null in list results. |
| `static Task<Result<GitHubPullRequest>> GetAsync(string repository, int number, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One pull request (`GET /repos/{owner}/{repo}/pulls/{number}`), with its counts and `IsMergeable`. An issue's number is `NotFound`. |
| `static Task<Result<ItemPage<GitHubPullRequest>>> ListPageAsync(string repository, GitHubPullRequestFilter? filter = null, int page = 1, int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `ListAsync`'s pull requests, for an app that pages them itself, e.g. in a [`TableGrid`](../../Niddy.Avalonia/Tables/TableGrid.md). Returns an [`ItemPage<T>`](../Collections/ItemPage.md), empty past the last page; `PageCount` comes from GitHub's `Link` header and `TotalCount` is null. |
| `List(…)` · `Get(…)` · `ListPage(…)` | Synchronous versions, with the same parameters. They work where [`WebContent`'s](../Web/WebContent.md#synchronous-versions) do: not in the browser, and only with a handler that supports synchronous sends (the shared client does, the native Android and iOS handlers don't). |

- **`repository`** is `owner/name`. Anything else throws `ArgumentException`. A `number`, `maxCount` or `page` under 1, or a `pageSize` outside 1 to 100, throws `ArgumentOutOfRangeException`.
- **`options`** is a [`GitHubReadOptions`](GitHubReadOptions.md): a token, the API address and a disk cache. Null sends no token and caches nothing, which works for public repositories within GitHub's 60 requests an hour per IP address.
- **`httpClient`.** Null uses a shared client. Requests go to the options' [`ApiAddress`](GitHubReadOptions.md#the-api-address), else the client's `BaseAddress`, else `https://api.github.com/`; for GitHub Enterprise Server, set `ApiAddress` to e.g. `https://github.example.com/api/v3/`.
- **Cancelling** `cancellationToken` throws `OperationCanceledException`. Everything else that goes wrong is a failed `Result` with one of the [`ErrorCodes`](#errorcodes).

### GitHubPullRequest

`sealed record GitHubPullRequest`: a pull request, as `ListAsync` and `GetAsync` read it.

| Member | Description |
|---|---|
| `required int Number` | The pull request number, e.g. `42` for `#42`. |
| `required string Title` | The title. |
| `string? Body` | The description, in Markdown, or null if it's empty. |
| `GitHubItemState State` | `Open`, `Closed` (without merging) or `Merged`. |
| `bool IsDraft` | Whether it's a draft. |
| `Uri? Url` | The pull request's page on GitHub. |
| `string? Repository` | The repository it's in, as `owner/name`. |
| `string? Author` | The login of whoever opened it, or null for a deleted account. |
| `IReadOnlyList<string> Labels` · `Assignees` | Label names, assignee logins. |
| `IReadOnlyList<string> RequestedReviewers` | Logins whose review is requested and still pending. Requested teams aren't included. |
| `string? Milestone` | Its milestone's title. |
| `string? HeadBranch` · `HeadSha` | The branch with the changes, e.g. `fix-crash`, and the commit it's at. |
| `string? HeadRepository` | The repository the branch is in: `Repository`, or a fork. Null if the fork was deleted. |
| `string? BaseBranch` | The branch it merges into, e.g. `main`. |
| `DateTimeOffset? CreatedAt` · `UpdatedAt` · `ClosedAt` · `MergedAt` | When it was opened, last updated, closed or merged (null while open), and merged (null if it wasn't). |
| `bool? IsMergeable` | Whether it can be merged without conflicts. Null in lists, and from `GetAsync` while GitHub is still working it out, which it starts on the first request: ask again a moment later. |
| `int? CommentCount` · `ReviewCommentCount` · `CommitCount` · `Additions` · `Deletions` · `ChangedFiles` | Conversation comments, review comments on the diff, commits, lines added and removed, files changed. Null in lists; only `GetAsync` reads them. |

### GitHubPullRequestFilter

`sealed record GitHubPullRequestFilter`: which pull requests `ListAsync` returns, and in what order. Every property is optional.

| Member | Description |
|---|---|
| `GitHubStateFilter State` | `Open` (the default), `Closed` (merged included) or `All`. See [`GitHubStateFilter`](GitHubIssues.md#githubissuefilter). |
| `string? Head` | Only pull requests from this branch, as `owner:branch`, e.g. `octocat:fix-crash`. A bare branch name matches nothing; filter on `HeadBranch` in the results instead. |
| `string? Base` | Only pull requests into this branch, e.g. `main`. |
| `GitHubPullRequestSort Sort` | What to sort by (below). Defaults to `Created`. |
| `bool Ascending` | Oldest or least first. Defaults to false: newest or most first. |

`GitHubPullRequestSort`:

| Value | Sorts by |
|---|---|
| `Created` | When they were opened. |
| `Updated` | When they were last updated. |
| `Popularity` | How many comments they have. |
| `LongRunning` | How long they've been open, ignoring those not updated in the last month. |

### ErrorCodes

`GitHubPullRequests.ErrorCodes` holds the `Error.Code` values the reads fail with. They're the same strings as [`GitHubIssues.ErrorCodes`](GitHubIssues.md#errorcodes)', so one check covers both. Failures from an HTTP response carry an `HttpRequestException` with the `StatusCode` in `Error.Exception`, and their message ends with GitHub's own message.

| Constant | When |
|---|---|
| `Unauthorized` | 401: the token is missing, invalid or expired. |
| `Forbidden` | 403: the token can't read the repository. |
| `RateLimited` | 429, or a 403 from GitHub's rate limits, with nothing cached to fall back on. Try again later, or use a token. |
| `NotFound` | 404: the repository or pull request doesn't exist, or the token can't see it. |
| `Invalid` | 422: GitHub rejected the filter, e.g. a `Head` it can't read. |
| `Failed` | Any other status, or a success response that couldn't be read. |
| `Network` | No response (no connection, a DNS failure or a timeout), with nothing cached to fall back on. |

## Examples

```csharp
using Niddy.Core.GitHub;
using Niddy.Core.Results;

GitHubReadOptions read = new() { Token = token, CacheDirectory = Path.Combine(paths.Cache, "github") };

// Open pull requests into main, least recently updated first
Result<IReadOnlyList<GitHubPullRequest>> stale = await GitHubPullRequests.ListAsync("vonderborch/Niddy",
    new GitHubPullRequestFilter { Base = "main", Sort = GitHubPullRequestSort.Updated, Ascending = true },
    options: read);
foreach (GitHubPullRequest pull in stale.Value ?? [])
    Console.WriteLine($"#{pull.Number} {pull.Title} by {pull.Author}{(pull.IsDraft ? " (draft)" : "")}");

// One pull request's size and merge state
Result<GitHubPullRequest> one = await GitHubPullRequests.GetAsync("vonderborch/Niddy", 42, read);
if (one.TryGetValue(out GitHubPullRequest pr, out Error? error))
    Console.WriteLine($"{pr.State}: +{pr.Additions} -{pr.Deletions} in {pr.ChangedFiles} files");
else if (error!.Code == GitHubPullRequests.ErrorCodes.NotFound)
    Console.WriteLine("No such pull request.");

// Its conversation, and a search
Result<IReadOnlyList<GitHubComment>> comments = await GitHubIssues.ListCommentsAsync("vonderborch/Niddy", 42, options: read);
Result<IReadOnlyList<GitHubIssueInfo>> mine = await GitHubIssues.SearchAsync(
    "repo:vonderborch/Niddy is:pr is:open author:octocat", options: read);

// A console tool, synchronously
GitHubPullRequest? latest = GitHubPullRequests.List("vonderborch/Niddy", maxCount: 1).Value?.FirstOrDefault();
```

## See also

- [GitHubIssues](GitHubIssues.md): issues, search and comments
- [GitHubProjects](GitHubProjects.md): project boards
- [GitHubReadOptions](GitHubReadOptions.md): the token and cache
- [Result](../Results/Result.md), [Error](../Results/Error.md)
