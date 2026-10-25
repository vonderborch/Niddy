using System.Runtime.Versioning;
using System.Text.Json.Nodes;
using Niddy.Core.Collections;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// Reads a repository's pull requests through GitHub's REST API: <see cref="ListAsync"/> and <see cref="GetAsync"/>.
/// Their conversation comments come from <see cref="GitHubIssues.ListCommentsAsync"/>, and
/// <see cref="GitHubIssues.SearchAsync"/> with <c>is:pr</c> searches them.
/// </summary>
/// <remarks>
/// Without a token, GitHub allows 60 requests an hour per IP address; set <see cref="GitHubReadOptions.CacheDirectory"/>
/// so repeat reads are answered with <c>304 Not Modified</c>, which doesn't count.
/// </remarks>
public static class GitHubPullRequests
{
    /// <summary>The <see cref="Error.Code"/> values the reads fail with. They're the same as <see cref="GitHubIssues.ErrorCodes"/>'.</summary>
    public static class ErrorCodes
    {
        /// <summary>401: the token is missing, invalid or expired.</summary>
        public const string Unauthorized = GitHubIssues.ErrorCodes.Unauthorized;

        /// <summary>403: the token can't read the repository.</summary>
        public const string Forbidden = GitHubIssues.ErrorCodes.Forbidden;

        /// <summary>
        /// 403 or 429 from GitHub's rate limits, with nothing cached to fall back on. Try again later, or use a token.
        /// </summary>
        public const string RateLimited = GitHubIssues.ErrorCodes.RateLimited;

        /// <summary>404: the repository or pull request doesn't exist, or the token can't see it.</summary>
        public const string NotFound = GitHubIssues.ErrorCodes.NotFound;

        /// <summary>422: GitHub rejected the filter, e.g. a <see cref="GitHubPullRequestFilter.Head"/> it can't read.</summary>
        public const string Invalid = GitHubIssues.ErrorCodes.Invalid;

        /// <summary>Any other response, or a success response that couldn't be read.</summary>
        public const string Failed = GitHubIssues.ErrorCodes.Failed;

        /// <summary>
        /// The request didn't get a response, with nothing cached to fall back on: no connection, DNS failure, or a
        /// timeout.
        /// </summary>
        public const string Network = GitHubIssues.ErrorCodes.Network;
    }

    /// <summary>
    /// A repository's pull requests (<c>GET /repos/{owner}/{repo}/pulls</c>): by default the 30 most recently opened
    /// open ones. List results leave the <see cref="GitHubPullRequest"/> counts and
    /// <see cref="GitHubPullRequest.IsMergeable"/> null; <see cref="GetAsync"/> reads them.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="filter">Which pull requests, and their order. Null is open ones, newest first.</param>
    /// <param name="maxCount">The most to return. It reads up to 100 per request, as many pages as it needs.</param>
    /// <param name="options">A token and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">
    /// The client to send with. If it has a <see cref="HttpClient.BaseAddress"/>, that's the API address, e.g.
    /// <c>https://github.example.com/api/v3/</c> for GitHub Enterprise Server. Null uses a shared client and
    /// <c>https://api.github.com/</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The pull requests, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>. Failures from
    /// an HTTP response carry an <see cref="HttpRequestException"/> with the status code.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubPullRequest>>> ListAsync(string repository,
        GitHubPullRequestFilter? filter = null, int maxCount = 30, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListCoreAsync(repository, filter, maxCount, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// A repository's pull requests, blocking until they're read. Otherwise the same as <see cref="ListAsync"/>. Like
    /// <see cref="WebContent"/>'s synchronous methods it doesn't work in the browser, and a client you pass needs a
    /// handler that supports synchronous sends.
    /// </summary>
    /// <inheritdoc cref="ListAsync" path="/param"/>
    /// <inheritdoc cref="ListAsync" path="/returns"/>
    /// <inheritdoc cref="ListAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubPullRequest>> List(string repository,
        GitHubPullRequestFilter? filter = null, int maxCount = 30, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListCoreAsync(repository, filter, maxCount, options, httpClient, true, cancellationToken));

    /// <summary>
    /// One pull request by its number (<c>GET /repos/{owner}/{repo}/pulls/{number}</c>), with its counts and
    /// <see cref="GitHubPullRequest.IsMergeable"/>.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="options">A token and a cache. Null uses no token and no cache, which works for public repositories.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The pull request, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a missing
    /// pull request, or the number of an issue, is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubPullRequest>> GetAsync(string repository, int number,
        GitHubReadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        GetCoreAsync(repository, number, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One pull request by its number, blocking until it's read. Otherwise the same as <see cref="GetAsync"/>; see
    /// <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="GetAsync" path="/param"/>
    /// <inheritdoc cref="GetAsync" path="/returns"/>
    /// <inheritdoc cref="GetAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubPullRequest> Get(string repository, int number, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(GetCoreAsync(repository, number, options, httpClient, true, cancellationToken));

    /// <summary>
    /// One page of a repository's pull requests, for an app that pages them itself, e.g. in a table with page buttons.
    /// Otherwise the same as <see cref="ListAsync"/>. The total isn't known: <see cref="ItemPage{T}.PageCount"/> comes
    /// from GitHub's <c>Link</c> header and <see cref="ItemPage{T}.TotalCount"/> is null.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="filter">Which pull requests, and their order. Null is open ones, newest first.</param>
    /// <param name="page">Which page, from 1.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="options">A token and a cache. Null uses no token and no cache, which works for public repositories.</param>
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
    public static Task<Result<ItemPage<GitHubPullRequest>>> ListPageAsync(string repository,
        GitHubPullRequestFilter? filter = null, int page = 1, int pageSize = 30, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListPageCoreAsync(repository, filter, page, pageSize, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One page of a repository's pull requests, blocking until it's read. Otherwise the same as
    /// <see cref="ListPageAsync"/>; see <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListPageAsync" path="/param"/>
    /// <inheritdoc cref="ListPageAsync" path="/returns"/>
    /// <inheritdoc cref="ListPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubPullRequest>> ListPage(string repository,
        GitHubPullRequestFilter? filter = null, int page = 1, int pageSize = 30, GitHubReadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListPageCoreAsync(repository, filter, page, pageSize, options, httpClient, true,
            cancellationToken));

    private static ValueTask<Result<IReadOnlyList<GitHubPullRequest>>> ListCoreAsync(string repository,
        GitHubPullRequestFilter? filter, int maxCount, GitHubReadOptions? options, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return GitHubReader.ListAsync(
            $"{GitHubApi.RepositoryPath(owner, name)}/pulls{(filter ?? GitHubPullRequestFilter.Default).ToQuery()}",
            maxCount, json => json as JsonArray, GitHubPullRequest.FromJson, options ?? GitHubReadOptions.Default,
            httpClient, $"The repository {owner}/{name} doesn't exist, or the token can't see it.", $"{owner}/{name}",
            sync, cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubPullRequest>>> ListPageCoreAsync(string repository,
        GitHubPullRequestFilter? filter, int page, int pageSize, GitHubReadOptions? options, HttpClient? httpClient,
        bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        GitHubReader.CheckPage(page, pageSize);
        return GitHubReader.PageAsync(
            $"{GitHubApi.RepositoryPath(owner, name)}/pulls{(filter ?? GitHubPullRequestFilter.Default).ToQuery()}",
            page, pageSize, json => json as JsonArray, GitHubPullRequest.FromJson, null, null,
            options ?? GitHubReadOptions.Default, httpClient,
            $"The repository {owner}/{name} doesn't exist, or the token can't see it.", $"{owner}/{name}", sync,
            cancellationToken);
    }

    private static ValueTask<Result<GitHubPullRequest>> GetCoreAsync(string repository, int number,
        GitHubReadOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        return GitHubReader.GetAsync($"{GitHubApi.RepositoryPath(owner, name)}/pulls/{number}",
            GitHubPullRequest.FromJson, options ?? GitHubReadOptions.Default, httpClient,
            $"{owner}/{name} has no pull request #{number}, or the token can't see the repository.", $"{owner}/{name}",
            sync, cancellationToken);
    }
}
