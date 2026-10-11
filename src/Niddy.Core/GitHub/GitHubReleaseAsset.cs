namespace Niddy.Core.GitHub;

/// <summary>A file attached to a <see cref="GitHubRelease"/>, as GitHub's API describes it.</summary>
public sealed record GitHubReleaseAsset
{
    /// <summary>The file name, e.g. <c>myapp-1.2.0-linux-x64.tar.gz</c>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The public download URL (<c>browser_download_url</c>). It works without a token for public repositories and
    /// doesn't count against the API rate limit.
    /// </summary>
    public required Uri DownloadUrl { get; init; }

    /// <summary>
    /// The asset's API URL, which downloads it with a token (<c>Accept: application/octet-stream</c>), the way to
    /// download from a private repository. Null if the response didn't include it.
    /// </summary>
    public Uri? ApiUrl { get; init; }

    /// <summary>The size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>The MIME type the uploader gave, e.g. <c>application/zip</c>, or null.</summary>
    public string? ContentType { get; init; }

    /// <summary>
    /// The SHA-256 hash GitHub computed when the file was uploaded, as 64 lowercase hex characters, or null for
    /// assets uploaded before GitHub started recording one (mid-2025).
    /// </summary>
    public string? Sha256 { get; init; }

    /// <summary>How many times the asset has been downloaded.</summary>
    public long DownloadCount { get; init; }
}
