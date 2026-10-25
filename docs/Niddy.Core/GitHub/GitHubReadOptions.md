# GitHubReadOptions

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubReadOptions.cs)

`sealed record GitHubReadOptions`: the token and disk cache that [`GitHubIssues`](GitHubIssues.md#reading-issues)' reads, [`GitHubPullRequests`](GitHubPullRequests.md) and [`GitHubProjects`](GitHubProjects.md) use. Make one and pass it to every call.

## API

| Member | Description |
|---|---|
| `string? Token { get; init; }` | A GitHub token. Needed for private repositories and for projects (which can't be read without one), and it raises the rate limit from 60 requests an hour per IP address to 5,000 per token. Null sends none, which works for public repositories' issues and pull requests. |
| `string? CacheDirectory { get; init; }` | A folder to cache responses in, e.g. under [`AppPaths.Cache`](../IO/AppPaths.md). Null caches nothing. |
| `TimeSpan CacheDuration { get; init; }` | How long a cached response is used without asking GitHub. Defaults to `TimeSpan.Zero`: always ask. Ignored without a `CacheDirectory`. |

### The token

A fine-grained personal access token with read-only **Issues**, **Pull requests** and (on the owner) **Projects** permissions covers everything. A classic token needs `repo` for private repositories and `read:project` for projects.

A token inside a shipped app can be extracted. Give it only the read permissions above, limited to the repositories it needs, or read from your own server instead. The token is only sent to the API address, in the `Authorization` header, and isn't stored or logged; cache files are named from a hash of it.

### The cache

With a `CacheDirectory`:
- **A cached response younger than `CacheDuration`** is used without a request.
- **An older REST response** is checked with its `ETag`. If it hasn't changed, GitHub answers `304 Not Modified`, which doesn't count against the rate limit. Projects come from GraphQL, which has no `ETag`s, so they're fetched again.
- **When GitHub can't be reached, is rate limiting, or has a server error,** the cached response is used however old it is, so reads keep working offline.
- **Failures aren't cached**, GraphQL errors included.

Because `CacheDuration` defaults to zero, reads are never stale while GitHub can be reached, and still save rate limit through `304`s and fall back to the cache offline. Raise it (to a few minutes, say) to make repeat reads instant, at the cost of seeing changes late.

Responses are cached per request and per token, one JSON file each. Failing to write the cache is ignored.

## Example

```csharp
using Niddy.Core.GitHub;
using Niddy.Core.Results;

GitHubReadOptions read = new()
{
    Token = settings.GitHubToken,                        // null is fine for public issues and pull requests
    CacheDirectory = Path.Combine(paths.Cache, "github"),
    CacheDuration = TimeSpan.FromMinutes(5),
};

Result<IReadOnlyList<GitHubIssueInfo>> issues = await GitHubIssues.ListAsync("owner/repo", options: read);
Result<IReadOnlyList<GitHubPullRequest>> pulls = await GitHubPullRequests.ListAsync("owner/repo", options: read);
Result<IReadOnlyList<GitHubProjectItem>> board = await GitHubProjects.ListItemsAsync("owner", 5, read);   // needs the token
```

## See also

- [GitHubIssues](GitHubIssues.md), [GitHubPullRequests](GitHubPullRequests.md), [GitHubProjects](GitHubProjects.md)
- [GitHubReleases](GitHubReleases.md), whose `GitHubReleaseOptions` caches the same way
- [AppPaths](../IO/AppPaths.md)
