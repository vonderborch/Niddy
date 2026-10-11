namespace Niddy.Core.GitHub;

/// <summary>
/// Options for <see cref="GitHubReleases"/>' lookups: a token, whether prereleases count, and a disk cache.
/// </summary>
public sealed record GitHubReleaseOptions
{
    internal static readonly GitHubReleaseOptions Default = new();

    /// <summary>
    /// A token for private repositories, or to raise the rate limit from 60 requests an hour per IP address to 5,000
    /// per token. A fine-grained personal access token with read-only Contents permission is enough. Null sends none,
    /// which works for public repositories. A token inside a shipped app can be extracted.
    /// </summary>
    public string? Token { get; init; }

    /// <summary>
    /// Whether <see cref="GitHubReleases.GetLatestAsync"/> and <see cref="GitHubReleases.CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)"/>
    /// consider prereleases. Without it they use GitHub's latest release, which is never a prerelease or a draft.
    /// With it they pick the highest version among the 30 most recent releases, prereleases included.
    /// </summary>
    public bool IncludePrereleases { get; init; }

    /// <summary>
    /// A folder to cache API responses in, e.g. a folder under <see cref="IO.AppPaths.Cache"/>. Null caches nothing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A cached response younger than <see cref="CacheDuration"/> is used without asking GitHub. An older one is
    /// checked with its <c>ETag</c>, and if it hasn't changed GitHub answers <c>304 Not Modified</c>, which doesn't count
    /// against the rate limit. When GitHub can't be reached, is rate limiting, or has a server error, the cached
    /// response is used however old it is, so an update check keeps working offline.
    /// </para>
    /// <para>
    /// Responses are cached per URL and per token, as JSON files. Failing to write the cache is ignored.
    /// </para>
    /// </remarks>
    public string? CacheDirectory { get; init; }

    /// <summary>
    /// How long a cached response is used without asking GitHub. Defaults to an hour. <see cref="TimeSpan.Zero"/>
    /// always asks, which still saves rate limit through <c>304</c> responses and still falls back to the cache.
    /// Ignored without a <see cref="CacheDirectory"/>.
    /// </summary>
    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromHours(1);
}
