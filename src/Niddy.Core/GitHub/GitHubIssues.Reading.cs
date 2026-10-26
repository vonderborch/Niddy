using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Niddy.Core.Collections;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

// Reading existing issues. Creating them is in GitHubIssues.cs.
public static partial class GitHubIssues
{
    /// <summary>The most results GitHub's search returns for one query.</summary>
    private const int MaxSearchResults = 1_000;

    /// <summary>
    /// A repository's issues (<c>GET /repos/{owner}/{repo}/issues</c>): by default the 30 most recently opened open
    /// ones, without pull requests.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="filter">Which issues, and their order. Null is open issues, newest first.</param>
    /// <param name="maxCount">The most to return. It reads up to 100 per request, as many pages as it needs.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">
    /// The client to send with, or null for a shared one. It sends to <paramref name="options"/>'
    /// <see cref="GitHubReadOptions.ApiAddress"/>, else to the client's <see cref="HttpClient.BaseAddress"/>, else to
    /// <c>https://api.github.com/</c>. For GitHub Enterprise Server, set the option to e.g.
    /// <c>https://github.example.com/api/v3/</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The issues, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>. Failures from an HTTP
    /// response carry an <see cref="HttpRequestException"/> with the status code.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubIssueInfo>>> ListAsync(string repository,
        GitHubIssueFilter? filter = null, int maxCount = 30, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListCoreAsync(repository, filter, maxCount, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// A repository's issues, blocking until they're read. Otherwise the same as <see cref="ListAsync"/>; see
    /// <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where
    /// the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListAsync" path="/param"/>
    /// <inheritdoc cref="ListAsync" path="/returns"/>
    /// <inheritdoc cref="ListAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubIssueInfo>> List(string repository, GitHubIssueFilter? filter = null,
        int maxCount = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListCoreAsync(repository, filter, maxCount, options, httpClient, true, cancellationToken));

    /// <summary>
    /// One issue by its number (<c>GET /repos/{owner}/{repo}/issues/{number}</c>). A pull request's number gives the
    /// pull request as an issue, with <see cref="GitHubIssueInfo.IsPullRequest"/> set.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="number">The issue number.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The issue, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a missing issue is
    /// <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubIssueInfo>> GetAsync(string repository, int number,
        GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        GetCoreAsync(repository, number, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One issue by its number, blocking until it's read. Otherwise the same as <see cref="GetAsync"/>; see
    /// <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where
    /// the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="GetAsync" path="/param"/>
    /// <inheritdoc cref="GetAsync" path="/returns"/>
    /// <inheritdoc cref="GetAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubIssueInfo> Get(string repository, int number, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(GetCoreAsync(repository, number, options, httpClient, true, cancellationToken));

    /// <summary>
    /// Searches issues and pull requests with GitHub's search syntax (<c>GET /search/issues</c>), e.g.
    /// <c>repo:owner/name is:issue is:open label:bug crash</c>. Results are in best-match order and can span
    /// repositories; add <c>repo:</c>, <c>org:</c> or <c>user:</c> to narrow them, and <c>is:issue</c> or <c>is:pr</c>
    /// for one kind.
    /// </summary>
    /// <remarks>
    /// Search has its own rate limit: 30 requests a minute with a token, 10 without. GitHub returns at most 1,000
    /// results for a query, and a new or edited issue takes up to a minute to show up.
    /// </remarks>
    /// <param name="query">The search, as typed into GitHub's search box.</param>
    /// <param name="maxCount">The most to return, up to 1,000. It reads up to 100 per request.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which only finds public issues.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The issues and pull requests found, or a failure whose <see cref="Error.Code"/> is one of
    /// <see cref="ErrorCodes"/>: a query GitHub can't run, e.g. with a <c>repo:</c> that doesn't exist, is
    /// <see cref="ErrorCodes.Invalid"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubIssueInfo>>> SearchAsync(string query, int maxCount = 30,
        GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SearchCoreAsync(query, maxCount, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// Searches issues and pull requests, blocking until it's done. Otherwise the same as <see cref="SearchAsync"/>;
    /// see <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where
    /// the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="SearchAsync" path="/remarks"/>
    /// <inheritdoc cref="SearchAsync" path="/param"/>
    /// <inheritdoc cref="SearchAsync" path="/returns"/>
    /// <inheritdoc cref="SearchAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubIssueInfo>> Search(string query, int maxCount = 30,
        GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(SearchCoreAsync(query, maxCount, options, httpClient, true, cancellationToken));

    /// <summary>
    /// The comments on an issue or pull request, oldest first (<c>GET /repos/{owner}/{repo}/issues/{number}/comments</c>).
    /// For a pull request these are the conversation's comments, not review comments on lines of the diff.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="number">The issue or pull request number.</param>
    /// <param name="maxCount">The most to return. It reads up to 100 per request, as many pages as it needs.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The comments, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a missing issue is
    /// <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> or <paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubComment>>> ListCommentsAsync(string repository, int number,
        int maxCount = 100, GitHubReadOptions? options = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListCommentsCoreAsync(repository, number, maxCount, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// The comments on an issue or pull request, blocking until they're read. Otherwise the same as
    /// <see cref="ListCommentsAsync"/>; see
    /// <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where
    /// the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListCommentsAsync" path="/param"/>
    /// <inheritdoc cref="ListCommentsAsync" path="/returns"/>
    /// <inheritdoc cref="ListCommentsAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubComment>> ListComments(string repository, int number, int maxCount = 100,
        GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListCommentsCoreAsync(repository, number, maxCount, options, httpClient, true,
            cancellationToken));

    /// <summary>
    /// One page of a repository's issues, for an app that pages them itself, e.g. in a table with page buttons.
    /// Otherwise the same as <see cref="ListAsync"/>.
    /// </summary>
    /// <remarks>
    /// GitHub lists pull requests with issues, and they're left out after the page is read (unless
    /// <see cref="GitHubIssueFilter.IncludePullRequests"/> is set), so a page can hold fewer than
    /// <paramref name="pageSize"/> issues, even none, with more pages after it. The total isn't known:
    /// <see cref="ItemPage{T}.PageCount"/> comes from GitHub's <c>Link</c> header and
    /// <see cref="ItemPage{T}.TotalCount"/> is null.
    /// </remarks>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="filter">Which issues, and their order. Null is open issues, newest first.</param>
    /// <param name="page">Which page, from 1.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The page, empty past the last one, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than 1, or <paramref name="pageSize"/> isn't 1 to 100.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<ItemPage<GitHubIssueInfo>>> ListPageAsync(string repository,
        GitHubIssueFilter? filter = null, int page = 1, int pageSize = 30, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListPageCoreAsync(repository, filter, page, pageSize, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One page of a repository's issues, blocking until it's read. Otherwise the same as <see cref="ListPageAsync"/>;
    /// see <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where the
    /// synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListPageAsync" path="/remarks"/>
    /// <inheritdoc cref="ListPageAsync" path="/param"/>
    /// <inheritdoc cref="ListPageAsync" path="/returns"/>
    /// <inheritdoc cref="ListPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubIssueInfo>> ListPage(string repository, GitHubIssueFilter? filter = null,
        int page = 1, int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListPageCoreAsync(repository, filter, page, pageSize, options, httpClient, true,
            cancellationToken));

    /// <summary>
    /// One page of a search's results, for an app that pages them itself. Otherwise the same as
    /// <see cref="SearchAsync"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="ItemPage{T}.TotalCount"/> is how many GitHub found, which can be more than 1,000, but only the first
    /// 1,000 can be read: <see cref="ItemPage{T}.PageCount"/> counts those pages. Search has its own rate limit of 30
    /// requests a minute with a token and 10 without, so page on demand rather than reading ahead.
    /// </remarks>
    /// <param name="query">The search, as typed into GitHub's search box.</param>
    /// <param name="page">Which page, from 1. It must start within the first 1,000 results.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which only finds public issues.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The page, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>, as for
    /// <see cref="SearchAsync"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="query"/> is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="page"/> is less than 1 or starts past the first 1,000 results, or <paramref name="pageSize"/>
    /// isn't 1 to 100.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<ItemPage<GitHubIssueInfo>>> SearchPageAsync(string query, int page = 1,
        int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SearchPageCoreAsync(query, page, pageSize, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One page of a search's results, blocking until it's read. Otherwise the same as <see cref="SearchPageAsync"/>;
    /// see <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where the
    /// synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="SearchPageAsync" path="/remarks"/>
    /// <inheritdoc cref="SearchPageAsync" path="/param"/>
    /// <inheritdoc cref="SearchPageAsync" path="/returns"/>
    /// <inheritdoc cref="SearchPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubIssueInfo>> SearchPage(string query, int page = 1, int pageSize = 30,
        GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(SearchPageCoreAsync(query, page, pageSize, options, httpClient, true, cancellationToken));

    /// <summary>
    /// One page of the comments on an issue or pull request, oldest first, for an app that pages them itself.
    /// Otherwise the same as <see cref="ListCommentsAsync"/>. The total isn't known:
    /// <see cref="ItemPage{T}.PageCount"/> comes from GitHub's <c>Link</c> header.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="number">The issue or pull request number.</param>
    /// <param name="page">Which page, from 1.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="options">A token, the API address and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The page, empty past the last one, or a failure whose <see cref="Error.Code"/> is one of
    /// <see cref="ErrorCodes"/>: a missing issue is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="number"/> or <paramref name="page"/> is less than 1, or <paramref name="pageSize"/> isn't 1 to 100.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<ItemPage<GitHubComment>>> ListCommentsPageAsync(string repository, int number,
        int page = 1, int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListCommentsPageCoreAsync(repository, number, page, pageSize, options, httpClient, false, cancellationToken)
            .AsTask();

    /// <summary>
    /// One page of the comments on an issue or pull request, blocking until it's read. Otherwise the same as
    /// <see cref="ListCommentsPageAsync"/>; see
    /// <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where the
    /// synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListCommentsPageAsync" path="/param"/>
    /// <inheritdoc cref="ListCommentsPageAsync" path="/returns"/>
    /// <inheritdoc cref="ListCommentsPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubComment>> ListCommentsPage(string repository, int number, int page = 1,
        int pageSize = 30, GitHubReadOptions? options = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListCommentsPageCoreAsync(repository, number, page, pageSize, options, httpClient, true,
            cancellationToken));

    private static ValueTask<Result<IReadOnlyList<GitHubIssueInfo>>> ListCoreAsync(string repository,
        GitHubIssueFilter? filter, int maxCount, GitHubReadOptions? options, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        filter ??= GitHubIssueFilter.Default;
        bool includePullRequests = filter.IncludePullRequests;
        return GitHubReader.ListAsync($"{GitHubApi.RepositoryPath(owner, name)}/issues{filter.ToQuery()}", maxCount,
            json => json as JsonArray,
            json => includePullRequests || json["pull_request"] is null ? GitHubIssueInfo.FromJson(json) : null,
            options ?? GitHubReadOptions.Default, httpClient, NotFound(owner, name), $"{owner}/{name}", sync,
            cancellationToken);
    }

    private static ValueTask<Result<GitHubIssueInfo>> GetCoreAsync(string repository, int number,
        GitHubReadOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return GitHubReader.GetAsync($"{GitHubApi.RepositoryPath(owner, name)}/issues/{number}", GitHubIssueInfo.FromJson,
            options ?? GitHubReadOptions.Default, httpClient,
            $"{owner}/{name} has no issue #{number}, or the token can't see the repository.", $"{owner}/{name}", sync,
            cancellationToken);
    }

    private static ValueTask<Result<IReadOnlyList<GitHubIssueInfo>>> SearchCoreAsync(string query, int maxCount,
        GitHubReadOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return GitHubReader.ListAsync($"search/issues?q={Uri.EscapeDataString(query.Trim())}",
            Math.Min(maxCount, MaxSearchResults), json => json["items"] as JsonArray, GitHubIssueInfo.FromJson,
            options ?? GitHubReadOptions.Default, httpClient, "GitHub's search isn't available.", "the search", sync,
            cancellationToken);
    }

    private static ValueTask<Result<IReadOnlyList<GitHubComment>>> ListCommentsCoreAsync(string repository, int number,
        int maxCount, GitHubReadOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return GitHubReader.ListAsync($"{GitHubApi.RepositoryPath(owner, name)}/issues/{number}/comments", maxCount,
            json => json as JsonArray, GitHubComment.FromJson, options ?? GitHubReadOptions.Default, httpClient,
            $"{owner}/{name} has no issue #{number}, or the token can't see the repository.", $"{owner}/{name}", sync,
            cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubIssueInfo>>> ListPageCoreAsync(string repository,
        GitHubIssueFilter? filter, int page, int pageSize, GitHubReadOptions? options, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        GitHubReader.CheckPage(page, pageSize);
        filter ??= GitHubIssueFilter.Default;
        bool includePullRequests = filter.IncludePullRequests;
        return GitHubReader.PageAsync($"{GitHubApi.RepositoryPath(owner, name)}/issues{filter.ToQuery()}", page, pageSize,
            json => json as JsonArray,
            json => includePullRequests || json["pull_request"] is null ? GitHubIssueInfo.FromJson(json) : null,
            null, null, options ?? GitHubReadOptions.Default, httpClient, NotFound(owner, name), $"{owner}/{name}", sync,
            cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubIssueInfo>>> SearchPageCoreAsync(string query, int page,
        int pageSize, GitHubReadOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        GitHubReader.CheckPage(page, pageSize);
        if ((long)(page - 1) * pageSize >= MaxSearchResults)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page,
                $"GitHub's search returns only the first {MaxSearchResults} results.");
        }

        return GitHubReader.PageAsync($"search/issues?q={Uri.EscapeDataString(query.Trim())}", page, pageSize,
            json => json["items"] as JsonArray, GitHubIssueInfo.FromJson, json => GitHubReader.Int(json, "total_count"),
            MaxSearchResults, options ?? GitHubReadOptions.Default, httpClient, "GitHub's search isn't available.",
            "the search", sync, cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubComment>>> ListCommentsPageCoreAsync(string repository, int number,
        int page, int pageSize, GitHubReadOptions? options, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        GitHubReader.CheckPage(page, pageSize);
        return GitHubReader.PageAsync($"{GitHubApi.RepositoryPath(owner, name)}/issues/{number}/comments", page,
            pageSize, json => json as JsonArray, GitHubComment.FromJson, null, null,
            options ?? GitHubReadOptions.Default, httpClient,
            $"{owner}/{name} has no issue #{number}, or the token can't see the repository.", $"{owner}/{name}", sync,
            cancellationToken);
    }

    private static string NotFound(string owner, string name) =>
        $"The repository {owner}/{name} doesn't exist, or the token can't see it.";
}
