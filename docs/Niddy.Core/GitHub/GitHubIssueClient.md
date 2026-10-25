# GitHubIssueClient

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubIssueClient.cs)

[`GitHubIssues`](GitHubIssues.md) set up once for one repository. The repository, token, `HttpClient`, browser launcher and a default reporter are given when it's made, so each report only needs the [`GitHubIssue`](GitHubIssue.md). Make one at startup and keep it for the app's lifetime, e.g. as a singleton. It holds no state between calls, so it's safe to use from any thread.

Every method does what the matching `GitHubIssues` method does, including [adding repeat reports to the open issue](GitHubIssues.md#duplicates). The static methods are still there for one-off calls.

## API

| Member | Description |
|---|---|
| `GitHubIssueClient(string repository, string? token = null, HttpClient? httpClient = null)` | `repository` is `owner/name`; it throws `ArgumentException` otherwise. `token` is as for [`CreateAsync`](GitHubIssues.md#tokens-and-privacy), or null to only use the browser form; a blank one counts as null. `httpClient` is as for `CreateAsync`: null uses a shared client, and its `BaseAddress` is the API address for GitHub Enterprise Server. |
| `string Repository` | `owner/name`, trimmed. |
| `bool HasToken` | Whether there's a token, so `CreateAsync` can be used. |
| `HttpClient? HttpClient` | The client given to the constructor. |
| `GitHubIssueReporter? Reporter { init; }` | The reporter for issues that don't set their own. An issue's own `Reporter` wins. |
| `Uri? BaseAddress { init; }` | The GitHub site for the browser form, for GitHub Enterprise Server. Null means `https://github.com/`. |
| `Action<string>? OpenUrl { init; }` | Opens the form's URL, for `CreateOrOpenAsync` and `OpenInBrowser`. Null uses [`UrlLauncher.Open`](../Web/UrlLauncher.md), which isn't supported on Android, watchOS or WASI. In an Avalonia app, set it to `url => _ = UriLauncher.TryLaunchAsync(control, url)`. |
| `Task<Result<GitHubIssueReference>> CreateAsync(GitHubIssue issue, CancellationToken cancellationToken = default)` | [`GitHubIssues.CreateAsync`](GitHubIssues.md#api). Throws `InvalidOperationException` when there's no token. |
| `Task<Result<GitHubIssueSubmission>> CreateOrOpenAsync(GitHubIssue issue, CancellationToken cancellationToken = default)` | [`GitHubIssues.CreateOrOpenAsync`](GitHubIssues.md#createoropenasync): the API if there's a token, the form with `OpenUrl` otherwise or when the API refuses. |
| `Result<GitHubIssueReference> Create(…)` · `Result<GitHubIssueSubmission> CreateOrOpen(…)` | Synchronous versions, with the same parameters and the same [limits](GitHubIssues.md#synchronous-versions): not in the browser, and `HttpClient` needs a handler that supports synchronous sends. |
| `string GetNewIssueUrl(GitHubIssue issue)` | [`GitHubIssues.GetNewIssueUrl`](GitHubIssues.md#api) with `BaseAddress`. |
| `void OpenInBrowser(GitHubIssue issue)` | Opens `GetNewIssueUrl` with `OpenUrl`, or `UrlLauncher.Open` if that's null. Needs no token. |

All of them throw `ArgumentException` if the issue's title is blank, and the async ones throw `OperationCanceledException` when cancelled. Everything else that goes wrong is a failed `Result` with one of [`GitHubIssues.ErrorCodes`](GitHubIssues.md#errorcodes).

The token isn't exposed, and the class doesn't override `ToString`, so logging the client doesn't log the token.

## Example

```csharp
using Niddy.Core.GitHub;
using Niddy.Core.Results;

// At startup, in an Avalonia app. The token may be null: then reports open in the browser.
GitHubIssueClient issues = new("vonderborch/Niddy", token)
{
    Reporter = new GitHubIssueReporter(Name: settings.UserName),
    OpenUrl = url => _ = UriLauncher.TryLaunchAsync(mainWindow, url),
};

// In the crash handler: the same crash twice becomes one issue with a comment
Result<GitHubIssueSubmission> sent = await issues.CreateOrOpenAsync(GitHubIssue.FromException(exception));
if (sent.IsSuccess)
{
    GitHubIssueSubmission submission = sent.Value!;
    string message = submission.Issue switch
    {
        null => "Finish the report in your browser.",
        { AddedAsComment: true } issue => $"Already reported as #{issue.Number}; your report was added to it.",
        { } issue => $"Reported as #{issue.Number}.",
    };
    Toast.Show(mainWindow, message);
}

// From a "Send feedback" button
await issues.CreateOrOpenAsync(GitHubIssue.FeatureRequest(title, text));
```

## See also

- [GitHubIssues](GitHubIssues.md): the static methods, duplicates, error codes, tokens and privacy
- [GitHubIssue](GitHubIssue.md): what goes in the issue, and fingerprints
- [UrlLauncher](../Web/UrlLauncher.md)
