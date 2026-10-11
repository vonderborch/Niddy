# GitHubIssues

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubIssues.cs)

Opens a GitHub issue from anywhere in an app: a bug report, a feature request, a question. It isn't tied to any other part of Niddy, so call it from a crash handler, a "Send feedback" button or a console tool. There are three ways:

- **`CreateAsync`** creates the issue through GitHub's REST API with a token, without leaving the app.
- **`OpenInBrowser`** / **`GetNewIssueUrl`** need no token. They open GitHub's new-issue form, filled in, and the user reviews it and submits it as themselves.
- **`CreateOrOpenAsync`** tries the API and falls back to the form when there's no token or the API refuses.

What goes in the issue is a [`GitHubIssue`](GitHubIssue.md): title, body, labels, collapsible [sections](GitHubIssueSection.md) such as logs, an optional reporter, extra details and system info. `GitHubIssue.FromException(ex)` makes a crash report in one call.

## API

| Member | Description |
|---|---|
| `static Task<Result<GitHubIssueReference>> CreateAsync(string repository, string token, GitHubIssue issue, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Creates the issue (`POST /repos/{owner}/{repo}/issues`). `repository` is `owner/name`. The body is `issue.FormatBody()`, cut to GitHub's 65,536-character limit, sections first. Returns the new issue, or a failure with one of the `ErrorCodes`. |
| `static string GetNewIssueUrl(string repository, GitHubIssue issue, Uri? baseAddress = null)` | The new-issue form's URL, filled in with the title, formatted body, labels, assignees and type. A body that would make the URL longer than about 8,000 characters is cut the same way, sections first, with each cut marked "… (cut)". The milestone is left out: the form takes milestone names, not numbers. |
| `static Task<Result<GitHubIssueSubmission>> CreateOrOpenAsync(string repository, string? token, GitHubIssue issue, HttpClient? httpClient = null, Action<string>? openUrl = null, Uri? baseAddress = null, CancellationToken cancellationToken = default)` | `CreateAsync` if there's a token, falling back to the form (see below). |
| `static Result<GitHubIssueReference> Create(…)` · `static Result<GitHubIssueSubmission> CreateOrOpen(…)` | Synchronous versions of `CreateAsync` and `CreateOrOpenAsync`, with the same parameters. See [Synchronous versions](#synchronous-versions). |
| `static void OpenInBrowser(string repository, GitHubIssue issue, Uri? baseAddress = null)` | Opens `GetNewIssueUrl` with [`UrlLauncher.Open`](../Web/UrlLauncher.md), so it throws `PlatformNotSupportedException` on Android, watchOS and WASI. In an Avalonia app, pass `GetNewIssueUrl` to [`UriLauncher.TryLaunchAsync`](../../Niddy.Avalonia/UriLauncher.md) instead, which works everywhere. |

All of them throw `ArgumentException` if `repository` isn't `owner/name` or the issue's title is blank, and `CreateAsync` also does if `token` is blank. Cancelling the token throws `OperationCanceledException`; everything else that goes wrong is a failed `Result`.

**`httpClient`.** Null uses a shared client and `https://api.github.com/`. Pass your own to set a timeout, a proxy or a test handler. If it has a `BaseAddress`, that's the API address, e.g. `https://github.example.com/api/v3/` for GitHub Enterprise Server. For the browser methods, `baseAddress` is the site instead, e.g. `https://github.example.com/`.

### Synchronous versions

`Create` and `CreateOrOpen` are for console tools and code that can't be async. They work like [`WebContent`'s synchronous versions](../Web/WebContent.md#synchronous-versions):
- They're real synchronous calls, so they can't deadlock, but they block the calling thread.
- They aren't available in the browser.
- A client you pass needs a handler that supports synchronous sends: `SocketsHttpHandler` does, the native Android and iOS handlers don't (they throw `NotSupportedException`). Without a client, they use a shared one that works everywhere else.

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

`GitHubIssues.ErrorCodes` holds the `Error.Code` values `CreateAsync` and `CreateOrOpenAsync` fail with. Failures from an HTTP response carry an `HttpRequestException` with the `StatusCode` in `Error.Exception`, and their message ends with GitHub's own message, e.g. `GitHub said: Validation Failed (type invalid)`.

| Constant | When |
|---|---|
| `Unauthorized` | 401: the token is missing, invalid or expired. |
| `Forbidden` | 403: the token can't create issues in the repository. |
| `RateLimited` | 429, or a 403 from GitHub's rate limits. Try again later. |
| `NotFound` | 404: the repository doesn't exist, or the token can't see it (GitHub answers 404 for private repositories the token has no access to). |
| `IssuesDisabled` | 410: the repository has issues turned off. |
| `Invalid` | 422: GitHub rejected the issue, e.g. an unknown issue type or milestone. |
| `Failed` | Any other status, or a success response without an issue number and URL. |
| `Network` | No response: no connection, a DNS failure or a timeout. |
| `BrowserUnavailable` | `CreateOrOpenAsync` needed the browser and couldn't open it. |

### GitHubIssueReference

`sealed record GitHubIssueReference(int Number, Uri Url)`: the issue `CreateAsync` created. `Url` is its page on GitHub.

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
    created => $"Thanks! Reported as #{created.Number}.",
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
```

## See also

- [GitHubIssue](GitHubIssue.md), with `GitHubIssueReporter`
- [GitHubIssueSection](GitHubIssueSection.md): logs and other collapsible blocks
- [Result](../Results/Result.md), [Error](../Results/Error.md)
- [GitHubReleases](GitHubReleases.md): read releases, check for updates, download assets
- [UrlLauncher](../Web/UrlLauncher.md)
