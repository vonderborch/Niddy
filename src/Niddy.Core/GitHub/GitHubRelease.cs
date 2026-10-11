using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.GitHub;

/// <summary>A GitHub release, as <see cref="GitHubReleases"/> reads it from the API.</summary>
public sealed record GitHubRelease
{
    /// <summary>The release's tag, e.g. <c>v1.2.0</c>.</summary>
    public required string TagName { get; init; }

    /// <summary>The release's title, or the tag if it has none.</summary>
    public required string Name { get; init; }

    /// <summary>The release notes, in Markdown, or null if there are none.</summary>
    public string? Notes { get; init; }

    /// <summary>The release's page on GitHub, for a "what's new" link, or null if the response didn't include it.</summary>
    public Uri? Url { get; init; }

    /// <summary>When the release was published, or null for a draft.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Whether it's marked as a prerelease.</summary>
    public bool IsPrerelease { get; init; }

    /// <summary>Whether it's a draft. Drafts only show up with a token that can push to the repository.</summary>
    public bool IsDraft { get; init; }

    /// <summary>The files attached to the release, in GitHub's order.</summary>
    public IReadOnlyList<GitHubReleaseAsset> Assets { get; init; } = [];

    /// <summary>
    /// The version in <see cref="TagName"/>: its numbers from the first digit on, so <c>v1.2.0</c>,
    /// <c>release-1.2.0</c> and <c>1.2.0-beta.1</c> are all <c>1.2.0</c>. Null if the tag has no version. A prerelease
    /// label isn't part of a <see cref="System.Version"/>; <see cref="GitHubReleases.CheckForUpdateAsync(string, string, GitHubReleaseOptions?, HttpClient?, CancellationToken)"/>
    /// takes it into account.
    /// </summary>
    public Version? Version => ReleaseVersion.Parse(this.TagName)?.Version;

    /// <summary>The asset called <paramref name="name"/>, ignoring case, or null.</summary>
    public GitHubReleaseAsset? GetAsset(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return this.Assets.FirstOrDefault(asset => string.Equals(asset.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The asset built for an operating system and architecture, going by the words in the file names, or null.
    /// </summary>
    /// <remarks>
    /// <para>
    /// It recognizes the usual spellings: <c>windows</c>/<c>win64</c>/<c>msvc</c>/<c>.exe</c>/<c>.msi</c>,
    /// <c>macos</c>/<c>osx</c>/<c>darwin</c>/<c>.dmg</c>, <c>linux</c>/<c>.AppImage</c>/<c>.deb</c>/<c>.rpm</c>,
    /// <c>android</c>/<c>.apk</c>, <c>ios</c>/<c>.ipa</c>, <c>freebsd</c>; and <c>x64</c>/<c>x86_64</c>/<c>amd64</c>,
    /// <c>arm64</c>/<c>aarch64</c>, <c>x86</c>/<c>i386</c>/<c>i686</c>, <c>arm</c>/<c>armv7</c>/<c>armhf</c>. Rust-style
    /// target triples such as <c>aarch64-apple-darwin</c> work too.
    /// </para>
    /// <para>
    /// An asset must name the OS, and no other. Among those, one naming the architecture beats a <c>universal</c> one
    /// (which suits x64 and arm64), which beats one that names no architecture; one naming a different architecture
    /// is skipped. A glibc build beats a <c>musl</c> one, and otherwise the asset listed first wins. Checksum and
    /// signature files are never picked. When names don't follow a pattern this understands, use
    /// <paramref name="filter"/>, or <see cref="GetAsset"/> with a name you build.
    /// </para>
    /// </remarks>
    /// <param name="os">The operating system. Null is the one this is running on.</param>
    /// <param name="architecture">The architecture. Null is the one this process is running as.</param>
    /// <param name="filter">Further limits which assets count, e.g. <c>a =&gt; a.Name.EndsWith(".zip")</c>. Null allows all.</param>
    public GitHubReleaseAsset? FindAssetForPlatform(OS? os = null, Architecture? architecture = null,
        Func<GitHubReleaseAsset, bool>? filter = null) =>
        ReleaseAssetMatcher.Find(this.Assets, os ?? Platform.Info.OS, architecture ?? RuntimeInformation.ProcessArchitecture,
            filter);

    /// <summary>Reads a release from the API's JSON. Throws if it's missing the tag or an asset's name or URL.</summary>
    internal static GitHubRelease FromJson(JsonNode json)
    {
        string tag = json["tag_name"]?.GetValue<string>() ?? throw new JsonException("The release has no tag_name.");
        string? name = json["name"]?.GetValue<string>();
        List<GitHubReleaseAsset> assets = [];
        if (json["assets"] is JsonArray array)
        {
            foreach (JsonNode? asset in array)
            {
                if (asset is not null)
                {
                    assets.Add(AssetFromJson(asset));
                }
            }
        }

        return new GitHubRelease
        {
            TagName = tag,
            Name = string.IsNullOrWhiteSpace(name) ? tag : name,
            Notes = NullIfBlank(json["body"]?.GetValue<string>()),
            Url = ToUri(json["html_url"]?.GetValue<string>()),
            PublishedAt = json["published_at"] is JsonValue published && published.TryGetValue(out DateTimeOffset at) ? at : null,
            IsPrerelease = json["prerelease"]?.GetValue<bool>() ?? false,
            IsDraft = json["draft"]?.GetValue<bool>() ?? false,
            Assets = assets,
        };
    }

    private static GitHubReleaseAsset AssetFromJson(JsonNode json)
    {
        string? digest = json["digest"]?.GetValue<string>();
        return new GitHubReleaseAsset
        {
            Name = json["name"]?.GetValue<string>() ?? throw new JsonException("A release asset has no name."),
            DownloadUrl = ToUri(json["browser_download_url"]?.GetValue<string>())
                ?? throw new JsonException("A release asset has no browser_download_url."),
            ApiUrl = ToUri(json["url"]?.GetValue<string>()),
            Size = json["size"]?.GetValue<long>() ?? 0,
            ContentType = NullIfBlank(json["content_type"]?.GetValue<string>()),
            Sha256 = digest is not null && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)
                ? digest["sha256:".Length..].ToLowerInvariant()
                : null,
            DownloadCount = json["download_count"]?.GetValue<long>() ?? 0,
        };
    }

    private static Uri? ToUri(string? url) => Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri : null;

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
