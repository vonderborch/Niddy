using System.Net;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// Reads a repository's GitHub releases, for update checks and for downloading what they ship:
/// <see cref="GetLatestAsync"/>, <see cref="GetByTagAsync"/> and <see cref="ListAsync"/> read releases,
/// <see cref="CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)"/> compares
/// the latest with the running version, <see cref="DownloadAssetAsync"/> downloads an asset and verifies its SHA-256,
/// and <see cref="GetAssetUrl"/> builds a download URL without calling the API at all. Each has a synchronous version.
/// </summary>
/// <remarks>
/// <para>
/// Without a token GitHub allows 60 API requests an hour per IP address, shared by everything on that address. Set
/// <see cref="GitHubReleaseOptions.CacheDirectory"/> so repeated checks are answered from disk or with a free
/// <c>304</c>, and keep working when GitHub can't be reached. Asset downloads don't count against the limit.
/// </para>
/// <para>
/// Pass an <see cref="HttpClient"/> whose <see cref="HttpClient.BaseAddress"/> is the API address, e.g.
/// <c>https://github.example.com/api/v3/</c>, for GitHub Enterprise Server. The synchronous versions work where
/// <see cref="WebContent"/>'s do: not in the browser, and only with a handler that supports synchronous sends.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(paths.Cache, "releases") };
/// Result&lt;GitHubUpdateCheck&gt; check = await GitHubReleases.CheckForUpdateAsync("vonderborch/MyApp", "1.4.0", options);
/// if (check.TryGetValue(out GitHubUpdateCheck update) &amp;&amp; update.IsUpdateAvailable)
/// {
///     GitHubReleaseAsset? asset = update.Latest.FindAssetForPlatform();
/// }
/// </code>
/// </example>
public static class GitHubReleases
{
    /// <summary>
    /// The <see cref="Error.Code"/> values the lookups fail with. <see cref="DownloadAssetAsync"/> fails with
    /// <see cref="WebContent.ErrorCodes"/> instead.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>404: no such repository or release, the repository has no releases, or the token can't see it.</summary>
        public const string NotFound = "NotFound";

        /// <summary>401: the token is invalid or expired.</summary>
        public const string Unauthorized = "Unauthorized";

        /// <summary>403: the token can't read the repository.</summary>
        public const string Forbidden = "Forbidden";

        /// <summary>403 or 429 from GitHub's rate limits, with nothing cached to fall back on. Try again later, or use a token.</summary>
        public const string RateLimited = "RateLimited";

        /// <summary>Any other response, or a success response that couldn't be read.</summary>
        public const string Failed = "Failed";

        /// <summary>The request didn't get a response, with nothing cached to fall back on: no connection, DNS failure, or a timeout.</summary>
        public const string Network = "Network";

        /// <summary>The latest release's tag has no version in it, so it can't be compared.</summary>
        public const string InvalidVersion = "InvalidVersion";
    }

    // Checksum files are small; a bigger one isn't one.
    private const long MaxChecksumFileBytes = 1024 * 1024;

    /// <summary>
    /// The latest release: GitHub's (never a draft or prerelease), or, with
    /// <see cref="GitHubReleaseOptions.IncludePrereleases"/>, the highest version among the 30 most recent releases.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="options">A token, prereleases, a cache. Null uses the defaults: no token, no prereleases, no cache.</param>
    /// <param name="httpClient">
    /// The client to send with. If it has a <see cref="HttpClient.BaseAddress"/>, that's the API address. Null uses a
    /// shared client and <c>https://api.github.com/</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The release, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>. A repository with no
    /// releases is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubRelease>> GetLatestAsync(string repository, GitHubReleaseOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        GetLatestCoreAsync(repository, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// The latest release, blocking until it's read. Otherwise the same as <see cref="GetLatestAsync"/>.
    /// </summary>
    /// <inheritdoc cref="GetLatestAsync" path="/param"/>
    /// <inheritdoc cref="GetLatestAsync" path="/returns"/>
    /// <inheritdoc cref="GetLatestAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubRelease> GetLatest(string repository, GitHubReleaseOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(GetLatestCoreAsync(repository, options, httpClient, true, cancellationToken));

    /// <summary>The release with the tag <paramref name="tag"/>, e.g. <c>v1.2.0</c>.</summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="tag">The release's tag, exactly as it is on GitHub.</param>
    /// <param name="options">A token and a cache. <see cref="GitHubReleaseOptions.IncludePrereleases"/> doesn't apply. Null uses the defaults.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="GetLatestAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The release, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>, or <paramref name="tag"/> is blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubRelease>> GetByTagAsync(string repository, string tag,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        GetByTagCoreAsync(repository, tag, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// The release with the tag <paramref name="tag"/>, blocking until it's read. Otherwise the same as
    /// <see cref="GetByTagAsync"/>.
    /// </summary>
    /// <inheritdoc cref="GetByTagAsync" path="/param"/>
    /// <inheritdoc cref="GetByTagAsync" path="/returns"/>
    /// <inheritdoc cref="GetByTagAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubRelease> GetByTag(string repository, string tag, GitHubReleaseOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(GetByTagCoreAsync(repository, tag, options, httpClient, true, cancellationToken));

    /// <summary>
    /// The most recent releases, newest first, prereleases included (and drafts, if the token can push to the
    /// repository). It reads 100 per request, as many pages as it needs.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="maxCount">The most releases to return.</param>
    /// <param name="options">A token and a cache. <see cref="GitHubReleaseOptions.IncludePrereleases"/> doesn't apply. Null uses the defaults.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="GetLatestAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>The releases, possibly none, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubRelease>>> ListAsync(string repository, int maxCount = 30,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListCoreAsync(repository, maxCount, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// The most recent releases, blocking until they're read. Otherwise the same as <see cref="ListAsync"/>.
    /// </summary>
    /// <inheritdoc cref="ListAsync" path="/param"/>
    /// <inheritdoc cref="ListAsync" path="/returns"/>
    /// <inheritdoc cref="ListAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubRelease>> List(string repository, int maxCount = 30,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListCoreAsync(repository, maxCount, options, httpClient, true, cancellationToken));

    /// <summary>
    /// Whether the latest release (as <see cref="GetLatestAsync"/> finds it) is newer than
    /// <paramref name="currentVersion"/>.
    /// </summary>
    /// <remarks>
    /// Versions are read from their first digit, so a tag of <c>v1.2.0</c> or <c>release-1.2.0</c> matches
    /// <c>1.2.0</c>, and compared the way semantic versioning compares them: <c>1.2</c> equals <c>1.2.0.0</c>,
    /// <c>1.2.0-beta.2</c> is older than <c>1.2.0-beta.10</c>, which is older than <c>1.2.0</c>, and <c>+build</c>
    /// metadata is ignored. <c>AssemblyInformationalVersion</c>, as <c>1.2.0+abc123</c>, works as it is.
    /// </remarks>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="currentVersion">The running version, e.g. <c>1.4.0</c>, <c>v1.4.0</c> or <c>1.5.0-beta.1</c>.</param>
    /// <param name="options">A token, prereleases, a cache. Null uses the defaults.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="GetLatestAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The answer and the latest release, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.
    /// A latest release whose tag has no version is <see cref="ErrorCodes.InvalidVersion"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, or <paramref name="currentVersion"/> has no version in it.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubUpdateCheck>> CheckForUpdateAsync(string repository, string currentVersion,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        CheckForUpdateCoreAsync(repository, currentVersion, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// Whether the latest release is newer than <paramref name="currentVersion"/>, e.g. the entry assembly's
    /// <c>GetName().Version</c>. Otherwise the same as the overload that takes a string.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="currentVersion">The running version.</param>
    /// <param name="options">A token, prereleases, a cache. Null uses the defaults.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="GetLatestAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <inheritdoc cref="CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/returns"/>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="currentVersion"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubUpdateCheck>> CheckForUpdateAsync(string repository, Version currentVersion,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        return CheckForUpdateCoreAsync(repository, currentVersion.ToString(), options, httpClient, false, cancellationToken)
            .AsTask();
    }

    /// <summary>
    /// Whether the latest release is newer than <paramref name="currentVersion"/>, blocking until it's known.
    /// Otherwise the same as <see cref="CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)"/>.
    /// </summary>
    /// <inheritdoc cref="CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/param"/>
    /// <inheritdoc cref="CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubUpdateCheck> CheckForUpdate(string repository, string currentVersion,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(CheckForUpdateCoreAsync(repository, currentVersion, options, httpClient, true, cancellationToken));

    /// <summary>
    /// Whether the latest release is newer than <paramref name="currentVersion"/>, blocking until it's known.
    /// Otherwise the same as <see cref="CheckForUpdateAsync(string, Version, GitHubReleaseOptions?, HttpClient?, CancellationToken)"/>.
    /// </summary>
    /// <inheritdoc cref="CheckForUpdateAsync(string, Version, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/param"/>
    /// <inheritdoc cref="CheckForUpdateAsync(string, Version, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="CheckForUpdateAsync(string, Version, GitHubReleaseOptions?, HttpClient?, CancellationToken)" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubUpdateCheck> CheckForUpdate(string repository, Version currentVersion,
        GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(currentVersion);
        return SharedHttp.RunSync(CheckForUpdateCoreAsync(repository, currentVersion.ToString(), options, httpClient, true,
            cancellationToken));
    }

    /// <summary>
    /// Downloads one of <paramref name="release"/>'s assets to <paramref name="path"/>, and checks its SHA-256 when the
    /// release gives one. Like <see cref="WebContent.DownloadFileAsync"/>, which it uses, the file only appears once
    /// it has all arrived and matched.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The expected hash is, in order: <see cref="DownloadOptions.Sha256"/>; the hash GitHub recorded on upload
    /// (<see cref="GitHubReleaseAsset.Sha256"/>); a <c>{asset}.sha256</c> or <c>{asset}.sha256sum</c> file in the
    /// release; or the asset's line in a checksum list in the release, such as <c>SHA256SUMS</c> or
    /// <c>checksums.txt</c>. With none of those the file is saved unchecked; set <see cref="DownloadOptions.Sha256"/>
    /// to insist on one.
    /// </para>
    /// <para>
    /// Without a token it downloads from <see cref="GitHubReleaseAsset.DownloadUrl"/>. With one, it downloads through
    /// the API (<see cref="GitHubReleaseAsset.ApiUrl"/>), which is how private repositories' assets are downloaded;
    /// GitHub redirects to its file storage, and the token isn't sent there.
    /// </para>
    /// </remarks>
    /// <param name="release">The release the asset belongs to; its other assets are where checksums are looked for.</param>
    /// <param name="asset">The asset to download, e.g. from <see cref="GitHubRelease.FindAssetForPlatform"/>.</param>
    /// <param name="path">The file to save to. Missing folders are created.</param>
    /// <param name="options">Overwriting, progress, a size limit, a checksum, resuming. Null uses the defaults.</param>
    /// <param name="token">A token that can read the repository, for private repositories. Null downloads publicly.</param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The saved file, or a failure whose <see cref="Error.Code"/> is one of <see cref="WebContent.ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="path"/> is blank, <paramref name="asset"/> isn't one of <paramref name="release"/>'s assets, or
    /// <see cref="DownloadOptions.Sha256"/> isn't a SHA-256 hash.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="DownloadOptions.MaxBytes"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<FileInfo>> DownloadAssetAsync(GitHubRelease release, GitHubReleaseAsset asset, string path,
        DownloadOptions? options = null, string? token = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        DownloadAssetCoreAsync(release, asset, path, options, token, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// Downloads one of <paramref name="release"/>'s assets, blocking until it's done. Otherwise the same as
    /// <see cref="DownloadAssetAsync"/>.
    /// </summary>
    /// <inheritdoc cref="DownloadAssetAsync" path="/param"/>
    /// <inheritdoc cref="DownloadAssetAsync" path="/returns"/>
    /// <inheritdoc cref="DownloadAssetAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<FileInfo> DownloadAsset(GitHubRelease release, GitHubReleaseAsset asset, string path,
        DownloadOptions? options = null, string? token = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(DownloadAssetCoreAsync(release, asset, path, options, token, httpClient, true, cancellationToken));

    /// <summary>
    /// The download URL of the asset <paramref name="assetName"/> in the latest release, or in the release tagged
    /// <paramref name="tag"/>, built without calling the API: <c>https://github.com/{owner}/{repo}/releases/latest/download/{asset}</c>.
    /// </summary>
    /// <remarks>
    /// For when the asset's name is predictable, e.g. <c>myapp-linux-x64.tar.gz</c> with no version in it: pass it to
    /// <see cref="WebContent.DownloadFileAsync"/>. GitHub redirects it to the file, it doesn't count against the API
    /// rate limit, and it only works for public repositories. "Latest" is GitHub's latest release, never a
    /// prerelease. The URL is fixed, so a missing asset only shows when downloading it, as
    /// <see cref="WebContent.ErrorCodes.NotFound"/>.
    /// </remarks>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="assetName">The asset's file name.</param>
    /// <param name="tag">The release's tag. Null is the latest release.</param>
    /// <param name="baseAddress">The GitHub site, for GitHub Enterprise Server. Null means <c>https://github.com/</c>.</param>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>, or <paramref name="assetName"/> is blank.</exception>
    public static string GetAssetUrl(string repository, string assetName, string? tag = null, Uri? baseAddress = null)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(assetName);
        string release = string.IsNullOrWhiteSpace(tag) ? "latest/download" : $"download/{Uri.EscapeDataString(tag.Trim())}";
        return new Uri(GitHubApi.EnsureTrailingSlash(baseAddress ?? GitHubApi.DefaultWebAddress),
            $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/releases/{release}/{Uri.EscapeDataString(assetName.Trim())}")
            .AbsoluteUri;
    }

    private static async ValueTask<Result<GitHubRelease>> GetLatestCoreAsync(string repository,
        GitHubReleaseOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        options ??= GitHubReleaseOptions.Default;
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        string display = $"{owner}/{name}";
        string path = GitHubApi.RepositoryPath(owner, name);
        if (!options.IncludePrereleases)
        {
            return await GetOneAsync($"{path}/releases/latest", options, httpClient,
                $"{display} has no releases, or doesn't exist, or the token can't see it.", display, sync,
                cancellationToken).ConfigureAwait(false);
        }

        Result<IReadOnlyList<GitHubRelease>> recent = await GetPageAsync($"{path}/releases?per_page=30", options,
            httpClient, display, sync, cancellationToken).ConfigureAwait(false);
        if (!recent.TryGetValue(out IReadOnlyList<GitHubRelease> releases, out Error? error))
        {
            return error!;
        }

        GitHubRelease? latest = Latest(releases.Where(release => !release.IsDraft));
        return latest is null
            ? new Error($"{display} has no releases.", ErrorCodes.NotFound)
            : Result<GitHubRelease>.Success(latest);
    }

    private static ValueTask<Result<GitHubRelease>> GetByTagCoreAsync(string repository, string tag,
        GitHubReleaseOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        string display = $"{owner}/{name}";
        return GetOneAsync($"{GitHubApi.RepositoryPath(owner, name)}/releases/tags/{Uri.EscapeDataString(tag.Trim())}",
            options ?? GitHubReleaseOptions.Default, httpClient,
            $"{display} has no release tagged {tag.Trim()}, or doesn't exist, or the token can't see it.", display, sync,
            cancellationToken);
    }

    private static async ValueTask<Result<IReadOnlyList<GitHubRelease>>> ListCoreAsync(string repository, int maxCount,
        GitHubReleaseOptions? options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        options ??= GitHubReleaseOptions.Default;
        string display = $"{owner}/{name}";
        int perPage = Math.Min(maxCount, 100);
        List<GitHubRelease> all = [];
        for (int page = 1; all.Count < maxCount; page++)
        {
            Result<IReadOnlyList<GitHubRelease>> result = await GetPageAsync(
                $"{GitHubApi.RepositoryPath(owner, name)}/releases?per_page={perPage}&page={page}", options, httpClient,
                display, sync, cancellationToken).ConfigureAwait(false);
            if (!result.TryGetValue(out IReadOnlyList<GitHubRelease> releases, out Error? error))
            {
                return error!;
            }

            all.AddRange(releases.Take(maxCount - all.Count));
            if (releases.Count < perPage)
            {
                break;
            }
        }

        return Result<IReadOnlyList<GitHubRelease>>.Success(all);
    }

    private static async ValueTask<Result<GitHubUpdateCheck>> CheckForUpdateCoreAsync(string repository,
        string currentVersion, GitHubReleaseOptions? options, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        GitHubApi.ParseRepository(repository);
        ArgumentNullException.ThrowIfNull(currentVersion);
        ReleaseVersion current = ReleaseVersion.Parse(currentVersion)
            ?? throw new ArgumentException($"\"{currentVersion}\" has no version in it.", nameof(currentVersion));

        Result<GitHubRelease> latest = await GetLatestCoreAsync(repository, options, httpClient, sync, cancellationToken)
            .ConfigureAwait(false);
        if (!latest.TryGetValue(out GitHubRelease release, out Error? error))
        {
            return error!;
        }

        if (ReleaseVersion.Parse(release.TagName) is not { } latestVersion)
        {
            return new Error($"The latest release's tag, {release.TagName}, has no version in it to compare.",
                ErrorCodes.InvalidVersion);
        }

        return new GitHubUpdateCheck(latestVersion.CompareTo(current) > 0, currentVersion, release);
    }

    private static async ValueTask<Result<FileInfo>> DownloadAssetCoreAsync(GitHubRelease release,
        GitHubReleaseAsset asset, string path, DownloadOptions? options, string? token, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!release.Assets.Contains(asset))
        {
            throw new ArgumentException($"{asset.Name} isn't one of {release.TagName}'s assets.", nameof(asset));
        }

        options ??= new DownloadOptions();
        // Fail on a bad hash before anything is sent.
        WebContent.ParseSha256(options.Sha256, nameof(options));
        if (options.Sha256 is null)
        {
            Result<string?> expected = await FindSha256Async(release, asset, token, httpClient, sync, cancellationToken)
                .ConfigureAwait(false);
            if (!expected.TryGetValue(out string? sha256, out Error? error))
            {
                return error!;
            }

            options = options with { Sha256 = sha256 };
        }

        (Uri url, Action<HttpRequestMessage>? configure) = Source(asset, token, "application/octet-stream");
        return await WebContent.DownloadCoreAsync(url.AbsoluteUri, path, null, options, httpClient, configure, sync,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The asset's SHA-256 from GitHub, a <c>.sha256</c> file beside it, or a checksum list in the release; null for none.
    /// </summary>
    private static async ValueTask<Result<string?>> FindSha256Async(GitHubRelease release, GitHubReleaseAsset asset,
        string? token, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        if (asset.Sha256 is not null)
        {
            return Result<string?>.Success(asset.Sha256);
        }

        GitHubReleaseAsset? single = release.GetAsset(asset.Name + ".sha256") ?? release.GetAsset(asset.Name + ".sha256sum");
        if (single is not null)
        {
            Result<string> content = await ReadChecksumFileAsync(single, token, httpClient, sync, cancellationToken)
                .ConfigureAwait(false);
            if (!content.TryGetValue(out string text, out Error? error))
            {
                return error!;
            }

            return ChecksumManifest.FindSingle(text) is { } hash
                ? Result<string?>.Success(hash)
                : new Error($"{single.Name} doesn't hold a SHA-256 hash.", WebContent.ErrorCodes.InvalidContent);
        }

        foreach (GitHubReleaseAsset manifest in release.Assets.Where(a => a != asset && ChecksumManifest.IsManifestName(a.Name)))
        {
            Result<string> content = await ReadChecksumFileAsync(manifest, token, httpClient, sync, cancellationToken)
                .ConfigureAwait(false);
            if (!content.TryGetValue(out string text, out Error? error))
            {
                return error!;
            }

            if (ChecksumManifest.Find(text, asset.Name) is { } hash)
            {
                return Result<string?>.Success(hash);
            }
        }

        return Result<string?>.Success(null);
    }

    private static ValueTask<Result<string>> ReadChecksumFileAsync(GitHubReleaseAsset file, string? token,
        HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (Uri url, Action<HttpRequestMessage>? configure) = Source(file, token, "application/octet-stream");
        return WebContent.ReadTextCoreAsync(url.AbsoluteUri, MaxChecksumFileBytes, httpClient, configure, sync,
            cancellationToken);
    }

    // With a token, an asset is downloaded through the API; GitHub redirects to its storage, and HttpClient doesn't
    // carry the Authorization header across the redirect.
    private static (Uri Url, Action<HttpRequestMessage>? Configure) Source(GitHubReleaseAsset asset, string? token,
        string accept)
    {
        if (string.IsNullOrWhiteSpace(token) || asset.ApiUrl is null)
        {
            return (asset.DownloadUrl, null);
        }

        return (asset.ApiUrl, request =>
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
            GitHubApi.AddApiHeaders(request, token);
        });
    }

    private static async ValueTask<Result<GitHubRelease>> GetOneAsync(string relative, GitHubReleaseOptions options,
        HttpClient? httpClient, string notFound, string display, bool sync, CancellationToken cancellationToken)
    {
        Result<JsonNode> json = await GetJsonAsync(relative, options, httpClient, notFound, display, sync,
            cancellationToken).ConfigureAwait(false);
        if (!json.TryGetValue(out JsonNode node, out Error? error))
        {
            return error!;
        }

        try
        {
            return GitHubRelease.FromJson(node);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(ex);
        }
    }

    private static async ValueTask<Result<IReadOnlyList<GitHubRelease>>> GetPageAsync(string relative,
        GitHubReleaseOptions options, HttpClient? httpClient, string display, bool sync, CancellationToken cancellationToken)
    {
        Result<JsonNode> json = await GetJsonAsync(relative, options, httpClient,
            $"{display} doesn't exist, or the token can't see it.", display, sync, cancellationToken).ConfigureAwait(false);
        if (!json.TryGetValue(out JsonNode node, out Error? error))
        {
            return error!;
        }

        try
        {
            if (node is not JsonArray array)
            {
                throw new JsonException("Expected a list of releases.");
            }

            List<GitHubRelease> releases = [.. array.OfType<JsonNode>().Select(GitHubRelease.FromJson)];
            return Result<IReadOnlyList<GitHubRelease>>.Success(releases);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(ex);
        }
    }

    private static async ValueTask<Result<JsonNode>> GetJsonAsync(string relative, GitHubReleaseOptions options,
        HttpClient? httpClient, string notFound, string display, bool sync, CancellationToken cancellationToken)
    {
        HttpClient client = SharedHttp.ClientFor(httpClient, sync);
        Uri address = GitHubApi.ApiAddress(client, relative);
        GitHubResponse response;
        try
        {
            response = await GitHubApi.GetAsync(client, address, options.Token, options.CacheDirectory,
                options.CacheDuration, sync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (GitHubApi.ToNetworkError(ex, ErrorCodes.Network, cancellationToken) is { } error)
        {
            return error;
        }

        if (!response.IsSuccess)
        {
            return ToError(response, notFound, display);
        }

        try
        {
            return JsonNode.Parse(response.Content) is { } node
                ? Result<JsonNode>.Success(node)
                : Unreadable(new JsonException("GitHub returned JSON null."));
        }
        catch (JsonException ex)
        {
            return Unreadable(ex);
        }
    }

    /// <summary>The release with the highest version, or the first if none has a version. Null for none.</summary>
    private static GitHubRelease? Latest(IEnumerable<GitHubRelease> releases)
    {
        GitHubRelease? first = null;
        GitHubRelease? best = null;
        ReleaseVersion? bestVersion = null;
        foreach (GitHubRelease release in releases)
        {
            first ??= release;
            if (ReleaseVersion.Parse(release.TagName) is { } version && (bestVersion is null || version.CompareTo(bestVersion) > 0))
            {
                best = release;
                bestVersion = version;
            }
        }

        return best ?? first;
    }

    private static Error Unreadable(Exception exception) =>
        new($"GitHub's response couldn't be read: {exception.Message}", ErrorCodes.Failed, exception);

    private static Error ToError(GitHubResponse response, string notFound, string repository)
    {
        HttpStatusCode status = response.Status;
        (string message, string code) = status switch
        {
            _ when response.IsRateLimited =>
                ("GitHub's rate limit was hit. Try again later, use a token, or set a cache directory.", ErrorCodes.RateLimited),
            HttpStatusCode.Unauthorized => ("The GitHub token is invalid or expired.", ErrorCodes.Unauthorized),
            HttpStatusCode.Forbidden => ($"The GitHub token can't read {repository}.", ErrorCodes.Forbidden),
            HttpStatusCode.NotFound => (notFound, ErrorCodes.NotFound),
            _ => ($"GitHub returned {(int)status} {response.ReasonPhrase}.", ErrorCodes.Failed),
        };

        return GitHubApi.ToError(response, message, code);
    }
}
