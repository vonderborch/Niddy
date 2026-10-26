namespace Niddy.Core.GitHub;

/// <summary>
/// Options for reading issues, pull requests and projects with <see cref="GitHubIssues"/>,
/// <see cref="GitHubPullRequests"/> and <see cref="GitHubProjects"/>: a token, the API address and a disk cache.
/// </summary>
public sealed record GitHubReadOptions
{
    internal static readonly GitHubReadOptions Default = new();

    /// <summary>
    /// A token, for private repositories, to raise the rate limit from 60 requests an hour per IP address to 5,000 per
    /// token, and for projects, which can't be read without one. A fine-grained personal access token with read-only
    /// Issues, Pull requests and Projects permissions is enough. Null sends none, which works for public repositories'
    /// issues and pull requests. A token inside a shipped app can be extracted.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// The REST API address, for GitHub Enterprise Server, e.g. <c>https://github.example.com/api/v3/</c>. GraphQL's,
    /// for projects, is worked out from it: <c>https://github.example.com/api/graphql</c>. Null uses the client's
    /// <see cref="HttpClient.BaseAddress"/>, or <c>https://api.github.com/</c> without one.
    /// </summary>
    /// <remarks>
    /// It takes precedence over the client's <see cref="HttpClient.BaseAddress"/>, so one client, or the shared one, can
    /// read from several servers. The token is sent to this address.
    /// </remarks>
    /// <exception cref="ArgumentException">Setting an address that isn't an absolute <c>http</c> or <c>https</c> one.</exception>
    public Uri? ApiAddress
    {
        get;
        init => field = GitHubApi.CheckApiAddress(value, nameof(this.ApiAddress));
    }

    /// <summary>
    /// A folder to cache API responses in, e.g. a folder under <see cref="IO.AppPaths.Cache"/>. Null caches nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cached response younger than <see cref="CacheDuration"/> is used without asking GitHub. An older REST response
    /// is checked with its <c>ETag</c>, and if it hasn't changed GitHub answers <c>304 Not Modified</c>, which doesn't
    /// count against the rate limit; projects come from GraphQL, which has no <c>ETag</c>s, so they're fetched again.
    /// When GitHub can't be reached, is rate limiting, or has a server error, the cached response is used however old
    /// it is.
    /// </para>
    /// <para>Responses are cached per request and per token, as JSON files. Failing to write the cache is ignored.</para>
    /// </remarks>
    public string? CacheDirectory { get; init; }

    /// <summary>
    /// How long a cached response is used without asking GitHub. Defaults to <see cref="TimeSpan.Zero"/>, which always
    /// asks, so issues are never stale while GitHub can be reached, but still saves rate limit through <c>304</c>
    /// responses and still falls back to the cache offline. Ignored without a <see cref="CacheDirectory"/>.
    /// </summary>
    public TimeSpan CacheDuration { get; init; }
}
