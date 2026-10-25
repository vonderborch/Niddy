# GitHubIssues

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubIssues.cs)

Opens a GitHub issue from anywhere in an app (a bug report, a feature request, a question) and reads existing ones. It isn't tied to any other part of Niddy, so call it from a crash handler, a "Send feedback" button or a console tool. There are three ways to open one:

- **`CreateAsync`** creates the issue through GitHub's REST API with a token, without leaving the app.
- **`OpenInBrowser`** / **`GetNewIssueUrl`** need no token. They open GitHub's new-issue form, filled in, and the user reviews it and submits it as themselves.
- **`CreateOrOpenAsync`** tries the API and falls back to the form when there's no token or the API refuses.

What goes in the issue is a [`GitHubIssue`](GitHubIssue.md): title, body, labels, collapsible [sections](GitHubIssueSection.md) such as logs, an optional reporter, extra details and system info. `GitHubIssue.FromException(ex)` makes a crash report in one call. An issue with a [`Fingerprint`](GitHubIssue.md#fingerprint), which crash reports have, isn't opened twice: a repeat report becomes a comment on the open issue (see [Duplicates](#duplicates)).

To set the repository, token, client and reporter once instead of on every call, make a [`GitHubIssueClient`](GitHubIssueClient.md).

To read issues, `ListAsync`, `GetAsync`, `SearchAsync` and `ListCommentsAsync` return [`GitHubIssueInfo`](#githubissueinfo) and [`GitHubComment`](#githubcomment) (see [Reading issues](#reading-issues)). Pull requests have their own [`GitHubPullRequests`](GitHubPullRequests.md), and projects [`GitHubProjects`](GitHubProjects.md).

## API

| Member | Description |
|---|---|
| `static Task<Result<GitHubIssueReference>> CreateAsync(string repository, string token, GitHubIssue issue, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Creates the issue (`POST /repos/{owner}/{repo}/issues`). `repository` is `owner/name`. The body is `issue.FormatBody()`, cut to GitHub's 65,536-character limit, sections first. With a `Fingerprint`, it adds to an open issue with the same one instead (see [Duplicates](#duplicates)). Returns the issue, or a failure with one of the `ErrorCodes`. |
| `static string GetNewIssueUrl(string repository, GitHubIssue issue, Uri? baseAddress = null)` | The new-issue form's URL, filled in with the title, formatted body, labels, assignees and type. A body that would make the URL longer than about 8,000 characters is cut the same way, sections first, with each cut marked "… (cut)". The milestone is left out: the form takes milestone names, not numbers. |
| `static Task<Result<GitHubIssueSubmission>> CreateOrOpenAsync(string repository, string? token, GitHubIssue issue, HttpClient? httpClient = null, Action<string>? openUrl = null, Uri? baseAddress = null, CancellationToken cancellationToken = default)` | `CreateAsync` if there's a token, falling back to the form (see below). |
| `static Task<Result<IReadOnlyList<GitHubIssueInfo>>> ListAsync(string repository, GitHubIssueFilter? filter = null, int maxCount = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | A repository's issues (`GET /repos/{owner}/{repo}/issues`). Null `filter` is open issues, newest first, without pull requests. See [Reading issues](#reading-issues). |
| `static Task<Result<GitHubIssueInfo>> GetAsync(string repository, int number, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One issue by number. A pull request's number gives it as an issue, with `IsPullRequest` set. |
| `static Task<Result<IReadOnlyList<GitHubIssueInfo>>> SearchAsync(string query, int maxCount = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | GitHub's issue search (`GET /search/issues`), e.g. `repo:owner/name is:open label:bug crash`. Best match first, across repositories unless the query narrows it; at most 1,000 results. |
| `static Task<Result<IReadOnlyList<GitHubComment>>> ListCommentsAsync(string repository, int number, int maxCount = 100, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | An issue's or pull request's comments, oldest first. For a pull request, the conversation's, not review comments on the diff. |
| `static Task<Result<ItemPage<GitHubIssueInfo>>> ListPageAsync(string repository, GitHubIssueFilter? filter = null, int page = 1, int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `ListAsync`'s issues, for an app that pages them itself (see [Paging yourself](#paging-yourself)). Empty past the last page. `PageCount` comes from GitHub's `Link` header; `TotalCount` is null. |
| `static Task<Result<ItemPage<GitHubIssueInfo>>> SearchPageAsync(string query, int page = 1, int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `SearchAsync`'s results. `TotalCount` is how many GitHub found, which can be over 1,000; `PageCount` counts only the pages within the first 1,000, the most that can be read. |
| `static Task<Result<ItemPage<GitHubComment>>> ListCommentsPageAsync(string repository, int number, int page = 1, int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | One page of `ListCommentsAsync`'s comments, oldest first. `PageCount` from the `Link` header; `TotalCount` is null. |
| `const int MaxSearchResults` | 1,000: the most `SearchAsync` returns for one query. |
| `static Result<GitHubIssueReference> Create(…)` · `static Result<GitHubIssueSubmission> CreateOrOpen(…)` · `List(…)` · `Get(…)` · `Search(…)` · `ListComments(…)` · `ListPage(…)` · `SearchPage(…)` · `ListCommentsPage(…)` | Synchronous versions of the methods above, with the same parameters. See [Synchronous versions](#synchronous-versions). |
| `static void OpenInBrowser(string repository, GitHubIssue issue, Uri? baseAddress = null)` | Opens `GetNewIssueUrl` with [`UrlLauncher.Open`](../Web/UrlLauncher.md), so it throws `PlatformNotSupportedException` on Android, watchOS and WASI. In an Avalonia app, pass `GetNewIssueUrl` to [`UriLauncher.TryLaunchAsync`](../../Niddy.Avalonia/UriLauncher.md) instead, which works everywhere. |

All of them throw `ArgumentException` if `repository` isn't `owner/name` or the issue's title is blank, and `CreateAsync` also does if `token` is blank. The reads throw `ArgumentException` for a blank search query and `ArgumentOutOfRangeException` for a `number` or `maxCount` under 1; the page methods throw it for a `page` under 1 or a `pageSize` outside 1 to 100, and `SearchPageAsync` for a page that starts past the first 1,000 results. Cancelling the token throws `OperationCanceledException`; everything else that goes wrong is a failed `Result`.

**`httpClient`.** Null uses a shared client and `https://api.github.com/`. Pass your own to set a timeout, a proxy or a test handler. If it has a `BaseAddress`, that's the API address, e.g. `https://github.example.com/api/v3/` for GitHub Enterprise Server. For the browser methods, `baseAddress` is the site instead, e.g. `https://github.example.com/`.

### Synchronous versions

`Create`, `CreateOrOpen`, `List`, `Get`, `Search` and `ListComments` are for console tools and code that can't be async. They work like [`WebContent`'s synchronous versions](../Web/WebContent.md#synchronous-versions):
- They're real synchronous calls, so they can't deadlock, but they block the calling thread.
- They aren't available in the browser.
- A client you pass needs a handler that supports synchronous sends: `SocketsHttpHandler` does, the native Android and iOS handlers don't (they throw `NotSupportedException`). Without a client, they use a shared one that works everywhere else.

### Duplicates

When the issue has a [`Fingerprint`](GitHubIssue.md#fingerprint), `CreateAsync` (and so `CreateOrOpenAsync` with a token) first searches the repository's open issues for its marker (`GET /search/issues`, oldest first). If it finds one:
- it adds the report to that issue as a comment (`POST /repos/{owner}/{repo}/issues/{number}/comments`) instead of opening a new one. The comment is the formatted body without the marker, under the new title in bold when the title differs (a different message for the same crash, say);
- the result is the existing issue, with `CommentUrl` set and `AddedAsComment` true. Labels, assignees, milestone and type aren't applied to it.

Closed issues don't count, so a crash that comes back after its issue was closed opens a new one.

If the search or the comment fails for any reason (a rate limit, the issue being locked, no network), a new issue is created instead: a duplicate is better than a lost report. If that fails too, its failure is what's returned.

Things to know:
- **Search lags.** GitHub indexes a new issue for search within about a minute, so two reports a few seconds apart can still open two issues.
- **Search has its own rate limit**, 30 requests a minute with a token. A report sent past it opens a new issue.
- The token needs to read issues as well as write them; the recommended **Issues: Read and write** permission covers both.
- The browser form can't check: without a token, or when `CreateOrOpenAsync` falls back to the form, the user may submit a duplicate. The marker is still in the body, so later reports through the API find it.

### CreateOrOpenAsync

It goes to the browser form when:
- `token` is null or blank;
- the API answers `Unauthorized`, `Forbidden`, `NotFound`, `RateLimited` or `Invalid`. The user may have the access the token lacks, or a rate limit of their own, and they can fix what GitHub rejected.

Any other failure is returned, because the form wouldn't work either: `Network`, `IssuesDisabled`, `Failed` (a 5xx, or a created issue whose response couldn't be read, where opening the form would make a duplicate).

`openUrl` opens the form. Null uses `UrlLauncher.Open`, which isn't supported on Android, watchOS or WASI. In an Avalonia app, pass `url => _ = UriLauncher.TryLaunchAsync(control, url)`. If `openUrl` throws, the result is a `BrowserUnavailable` failure, whose message also gives the API's refusal, if there was one.

#### GitHubIssueSubmission

`sealed record GitHubIssueSubmission`: which way the issue went.

| Member | Description |
|---|---|
| `GitHubIssueReference? Issue` | The issue the API created, or null if the form was opened. |
| `bool OpenedInBrowser` | True when the form was opened. The issue exists only once the user submits it, which the app can't see. |
| `string? NewIssueUrl` | The form's URL, when it was opened. |
| `Error? ApiError` | Why the API wasn't used: its failure, or null when there was no token. |

### ErrorCodes

`GitHubIssues.ErrorCodes` holds the `Error.Code` values `CreateAsync`, `CreateOrOpenAsync` and the reads fail with. [`GitHubPullRequests.ErrorCodes`](GitHubPullRequests.md#errorcodes) and [`GitHubProjects.ErrorCodes`](GitHubProjects.md#errorcodes) use the same values. Failures from an HTTP response carry an `HttpRequestException` with the `StatusCode` in `Error.Exception`, and their message ends with GitHub's own message, e.g. `GitHub said: Validation Failed (type invalid)`.

| Constant | When |
|---|---|
| `Unauthorized` | 401: the token is missing, invalid or expired. |
| `Forbidden` | 403: the token can't create issues in the repository, or can't read it. |
| `RateLimited` | 429, or a 403 from GitHub's rate limits, and for reads, nothing cached to fall back on. Try again later, or use a token. |
| `NotFound` | 404: the repository or issue doesn't exist, or the token can't see it (GitHub answers 404 for private repositories the token has no access to). |
| `IssuesDisabled` | 410: the repository has issues turned off. |
| `Invalid` | 422: GitHub rejected the issue, e.g. an unknown issue type or milestone, or a read's filter or search query. |
| `Failed` | Any other status, a success response without an issue number and URL, or a read's response that couldn't be read. |
| `Network` | No response: no connection, a DNS failure or a timeout, and for reads, nothing cached to fall back on. |
| `BrowserUnavailable` | `CreateOrOpenAsync` needed the browser and couldn't open it. |

### Reading issues

`ListAsync`, `GetAsync`, `SearchAsync` and `ListCommentsAsync` read through the REST API. They need no token for public repositories; pass [`GitHubReadOptions`](GitHubReadOptions.md) for a token (private repositories, a higher rate limit) and a disk cache.

- **Paging.** `maxCount` is the most to return. They read up to 100 per request and as many pages as that takes, so a large `maxCount` costs several requests. To page in your own UI instead, use the `…PageAsync` methods (see [Paging yourself](#paging-yourself)).
- **Pull requests.** GitHub's issues list includes pull requests. `ListAsync` leaves them out unless `GitHubIssueFilter.IncludePullRequests` is set, and still counts them towards a page, so a page of nothing but pull requests reads the next one. `GetAsync` and `SearchAsync` return them as [`GitHubIssueInfo`](#githubissueinfo) with `IsPullRequest` set; use [`GitHubPullRequests`](GitHubPullRequests.md) for their branches and merge state.
- **Search** has its own rate limit (30 requests a minute with a token, 10 without), returns at most 1,000 results for a query (`maxCount` is capped there), and takes up to a minute to see a new or edited issue. A query GitHub can't run, e.g. a `repo:` that doesn't exist, is `Invalid`.
- **Failures** are `ErrorCodes` results, as for `CreateAsync`. With a cache directory, a network failure, a rate limit or a 5xx returns the cached copy instead, however old.

#### Paging yourself

`ListPageAsync`, `SearchPageAsync` and `ListCommentsPageAsync` read one page, by number, and return an [`ItemPage<T>`](../Collections/ItemPage.md): the items, `HasMore`, and the `PageCount` (and for search, `TotalCount`) where GitHub says. They suit a UI with real pages, such as a [`TableGrid`](../../Niddy.Avalonia/Tables/TableGrid.md) through a [`PagedTableSource<T>`](../../Niddy.Avalonia/Tables/PagedTableSource.md).

- **`page`** is from 1 and **`pageSize`** is 1 to 100; anything else throws `ArgumentOutOfRangeException`. A page past the end is empty, with `HasMore` false.
- **Pull requests** are left out of `ListPageAsync`'s pages after they're read, as for `ListAsync`, so a page can hold fewer than `pageSize` issues, even none, with more after it. Set `GitHubIssueFilter.IncludePullRequests` for full pages.
- **Search** pages must start within the first 1,000 results. Page on demand rather than reading ahead: search's rate limit is 30 requests a minute.
- Caching, failures and cancelling work as for the other reads.

#### GitHubIssueInfo

`sealed record GitHubIssueInfo`: an existing issue, as the reads return it. ([`GitHubIssue`](GitHubIssue.md) is the one for opening an issue.)

| Member | Description |
|---|---|
| `required int Number` | The issue number, e.g. `42` for `#42`. |
| `required string Title` | The title. |
| `string? Body` | The body, in Markdown, or null if it's empty. |
| `GitHubItemState State` | `Open` or `Closed`; `Merged` for a merged pull request. |
| `string? StateReason` | Why it's in its state, as GitHub says it, e.g. `completed`, `not_planned`, `duplicate` or `reopened`. |
| `bool IsPullRequest` | Whether it's a pull request. |
| `Uri? Url` | The issue's page on GitHub. |
| `string? Repository` | `owner/name`. Useful for search results, which can span repositories. |
| `string? Author` | The login of whoever opened it, or null for a deleted account. |
| `IReadOnlyList<string> Labels` · `Assignees` | Label names, assignee logins. |
| `string? Milestone` | Its milestone's title. |
| `int CommentCount` | How many comments it has. |
| `bool IsLocked` | Whether the conversation is locked. |
| `DateTimeOffset? CreatedAt` · `UpdatedAt` · `ClosedAt` | When it was opened, last updated, and closed (null while open). |

#### GitHubIssueFilter

`sealed record GitHubIssueFilter`: which issues `ListAsync` returns, and in what order. Every property is optional.

| Member | Description |
|---|---|
| `GitHubStateFilter State` | `Open` (the default), `Closed` or `All`. |
| `IReadOnlyList<string> Labels` | Only issues with all of these labels. |
| `string? Assignee` | Only issues assigned to this login; `none` for unassigned ones, `*` for assigned ones. |
| `string? Creator` | Only issues opened by this login. |
| `string? Mentioned` | Only issues that mention this login. |
| `string? Milestone` | Only issues in this milestone, by its number (not its title); `none` for none, `*` for any. |
| `DateTimeOffset? Since` | Only issues updated at or after this time. |
| `GitHubIssueSort Sort` | `Created` (the default), `Updated` or `Comments`. |
| `bool Ascending` | Oldest or fewest first. Defaults to false: newest or most first. |
| `bool IncludePullRequests` | Whether to include pull requests. Defaults to false. |

`GitHubStateFilter` (`Open`, `Closed`, `All`) is also what [`GitHubPullRequestFilter.State`](GitHubPullRequests.md#githubpullrequestfilter) takes; `Closed` includes merged pull requests. `GitHubItemState` (`Open`, `Closed`, `Merged`) is the state an issue, pull request or [project item](GitHubProjects.md#githubprojectitem) is in; only pull requests are `Merged`.

#### GitHubComment

`sealed record GitHubComment`: a comment, as `ListCommentsAsync` reads it.

| Member | Description |
|---|---|
| `required long Id` | GitHub's ID for the comment. |
| `required string Body` | The comment, in Markdown. |
| `string? Author` | The login of whoever wrote it, or null for a deleted account. |
| `Uri? Url` | The comment's link on GitHub. |
| `DateTimeOffset? CreatedAt` · `UpdatedAt` | When it was written, and last edited (the same if it never was). |

### GitHubIssueReference

`sealed record GitHubIssueReference(int Number, Uri Url)`: the issue `CreateAsync` created, or the open one it added the report to.

| Member | Description |
|---|---|
| `int Number` | The issue number, e.g. `42` for `#42`. |
| `Uri Url` | The issue's page on GitHub. |
| `Uri? CommentUrl` | The comment the report was added as, when the issue already existed ([Duplicates](#duplicates)); null for a new issue. |
| `bool AddedAsComment` | Whether `CommentUrl` is set. |

## Tokens and privacy

- **A token in a shipped app can be extracted**, and then anyone can use it. Use a [fine-grained personal access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens) limited to the one repository, with only the **Issues: Read and write** permission, so the worst it can do is open and edit issues there. For anything more, send reports to your own server and call `CreateAsync` there, or use `OpenInBrowser`, which needs no token.
- **Labels, assignees and milestones need push access.** Through the API GitHub silently drops them when the token can't push to the repository, so an issues-only token creates the issue without them. The browser form does the same for users who can't set them.
- **Issues are as public as the repository.** On a public repository, everything in the issue is public, including the reporter's name and email. Ask before including them, and before setting `IncludeSystemInfo`.
- The token isn't stored or logged. It's only sent to the API address, in the `Authorization` header.

## Examples

```csharp
using Niddy.Core.GitHub;
using Niddy.Core.Results;

// A crash report with the end of the log and the user's details, if they gave them
GitHubIssue issue = GitHubIssue.FromException(exception, body: whatTheUserWasDoing);
issue = issue with
{
    Sections = [.. issue.Sections, GitHubIssueSection.FromLatestLogFile(paths.Logs)],
    Reporter = new GitHubIssueReporter(Name: userName, Email: userEmail),
    Details = new Dictionary<string, string> { ["Document"] = Path.GetFileName(path) },
};

Result<GitHubIssueReference> result = await GitHubIssues.CreateAsync("vonderborch/Niddy", token, issue);
string message = result.Match(
    created => created.AddedAsComment
        ? $"Thanks! This was already reported as #{created.Number}; your report was added to it."
        : $"Thanks! Reported as #{created.Number}.",
    error => error.Code == GitHubIssues.ErrorCodes.Network ? "You're offline. Try again later." : error.Message);

// No token: let the user submit it
GitHubIssues.OpenInBrowser("vonderborch/Niddy", GitHubIssue.FeatureRequest("Dark mode", "Please add one."));

// The API if possible, the form if not (token may be null), from an Avalonia view
Result<GitHubIssueSubmission> sent = await GitHubIssues.CreateOrOpenAsync("vonderborch/Niddy", token, issue,
    openUrl: url => _ = UriLauncher.TryLaunchAsync(this, url));
if (sent.IsSuccess)
    Toast.Show(this, sent.Value!.OpenedInBrowser ? "Finish the report in your browser." : $"Reported as #{sent.Value.Issue!.Number}.");

// Any kind of issue, with an issue type and a timeout
using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(15) };
await GitHubIssues.CreateAsync("vonderborch/Niddy", token,
    new GitHubIssue { Title = "How do I change the log folder?", Labels = ["question"], Type = "Task" }, client, ct);

// GitHub Enterprise Server
using HttpClient enterprise = new() { BaseAddress = new Uri("https://github.example.com/api/v3/") };
await GitHubIssues.CreateAsync("team/app", token, issue, enterprise);
string url = GitHubIssues.GetNewIssueUrl("team/app", issue, new Uri("https://github.example.com/"));

// A console tool, synchronously
Result<GitHubIssueReference> filed = GitHubIssues.Create("vonderborch/Niddy", token, issue);

// Open bugs, most recently updated first, cached for a few minutes
GitHubReadOptions read = new()
{
    Token = token,
    CacheDirectory = Path.Combine(paths.Cache, "github"),
    CacheDuration = TimeSpan.FromMinutes(5),
};
Result<IReadOnlyList<GitHubIssueInfo>> bugs = await GitHubIssues.ListAsync("vonderborch/Niddy",
    new GitHubIssueFilter { Labels = ["bug"], Sort = GitHubIssueSort.Updated }, maxCount: 50, options: read);
foreach (GitHubIssueInfo bug in bugs.Value ?? [])
    Console.WriteLine($"#{bug.Number} {bug.Title} ({bug.CommentCount} comments)");

// Search, then read the first hit's comments
Result<IReadOnlyList<GitHubIssueInfo>> found = await GitHubIssues.SearchAsync(
    "repo:vonderborch/Niddy is:issue is:open crash on save", options: read);
if (found.TryGetValue(out IReadOnlyList<GitHubIssueInfo> hits, out Error? error) && hits.Count > 0)
{
    Result<IReadOnlyList<GitHubComment>> comments = await GitHubIssues.ListCommentsAsync("vonderborch/Niddy",
        hits[0].Number, options: read);
}
```

## See also

- [GitHubIssueClient](GitHubIssueClient.md): the same, set up once for a repository
- [GitHubPullRequests](GitHubPullRequests.md), [GitHubProjects](GitHubProjects.md), [GitHubReadOptions](GitHubReadOptions.md)
- [GitHubIssue](GitHubIssue.md), with `GitHubIssueReporter`
- [GitHubIssueSection](GitHubIssueSection.md): logs and other collapsible blocks
- [Result](../Results/Result.md), [Error](../Results/Error.md)
- [GitHubReleases](GitHubReleases.md): read releases, check for updates, download assets
- [UrlLauncher](../Web/UrlLauncher.md)
