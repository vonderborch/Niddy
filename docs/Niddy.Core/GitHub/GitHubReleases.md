# GitHubReleases

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubReleases.cs)

Reads a repository's GitHub releases, for update checks and for downloading what they ship. It can:
- **read releases:** the latest, one by tag, or a list;
- **check for an update:** compare the latest release with the running version, semantic-versioning style;
- **pick an asset** for an operating system and architecture by its file name;
- **download an asset**, verified against the SHA-256 GitHub recorded or a checksum file in the release;
- **build a download URL** without calling the API at all.

Every call has a synchronous version. Failures come back as a [`Result`](../Results/Result.md), and a disk cache keeps update checks inside GitHub's rate limit and working offline.

## API

| Member | Description |
|---|---|
| `static Task<Result<GitHubRelease>> GetLatestAsync(string repository, GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | The latest release. By default that's GitHub's latest, which is never a draft or a prerelease. With `IncludePrereleases`, it's the highest version among the 30 most recent releases that aren't drafts. A repository with no releases is `NotFound`. |
| `static Task<Result<GitHubRelease>> GetByTagAsync(string repository, string tag, GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | The release tagged `tag`, exactly as it is on GitHub. A blank tag throws `ArgumentException`. |
| `static Task<Result<IReadOnlyList<GitHubRelease>>> ListAsync(string repository, int maxCount = 30, GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Up to `maxCount` of the most recent releases, newest first. Prereleases are included, and drafts too if the token can push to the repository. It reads up to 100 per request, as many pages as it needs. A `maxCount` below 1 throws `ArgumentOutOfRangeException`. |
| `static Task<Result<GitHubUpdateCheck>> CheckForUpdateAsync(string repository, string currentVersion, GitHubReleaseOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Whether the latest release (as `GetLatestAsync` finds it) is newer than `currentVersion`. See [Update checks](#update-checks). |
| `static Task<Result<GitHubUpdateCheck>> CheckForUpdateAsync(string repository, Version currentVersion, …)` | The same, for a `Version` such as the entry assembly's `GetName().Version`. |
| `static Task<Result<FileInfo>> DownloadAssetAsync(GitHubRelease release, GitHubReleaseAsset asset, string path, DownloadOptions? options = null, string? token = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Downloads one of `release`'s assets to `path`, checking its SHA-256. See [Downloading assets](#downloading-assets). |
| `static string GetAssetUrl(string repository, string assetName, string? tag = null, Uri? baseAddress = null)` | An asset's download URL, built without calling the API. See [Download URLs without the API](#download-urls-without-the-api). |
| `GetLatest`, `GetByTag`, `List`, `CheckForUpdate` (both overloads), `DownloadAsset` | Synchronous versions, with the same parameters. They work where [`WebContent`'s](../Web/WebContent.md#synchronous-versions) do: not in the browser, and only with a handler that supports synchronous sends (the shared client does, the native Android and iOS handlers don't). |

- **`repository`** is `owner/name`. Anything else throws `ArgumentException`.
- **`httpClient`.** Null uses a shared client and `https://api.github.com/`. Pass your own to set a timeout, a proxy or a test handler. If it has a `BaseAddress`, that's the API address, e.g. `https://github.example.com/api/v3/` for GitHub Enterprise Server.
- **Cancelling** `cancellationToken` throws `OperationCanceledException`. Everything else that goes wrong is a failed `Result`.

### Rate limits and the cache

Without a token, GitHub allows 60 API requests an hour per IP address, shared by everything on that address: a whole office, or a CI runner. A token raises that to 5,000 an hour. Asset downloads from `DownloadUrl` don't count.

Set `CacheDirectory` and lookups go easy on the limit:
- **A fresh response** (younger than `CacheDuration`, an hour by default) is used without asking GitHub.
- **An older one** is checked with its `ETag`. If the release hasn't changed, GitHub answers `304 Not Modified`, which is free.
- **When GitHub can't answer** (no network, a rate limit, a 5xx), the cached response is used however old it is. An update check keeps working offline.
- **Errors aren't cached:** a `NotFound` or `Unauthorized` is returned as it is.

Responses are cached per URL and per token, one JSON file each. Failing to write the cache is ignored.

### Update checks

`CheckForUpdateAsync` reads the version from the tag, starting at its first digit. So `v1.2.0`, `release-1.2.0` and `1.2.0` all mean `1.2.0`. It compares versions the way semantic versioning does:

| Comparison | Result |
|---|---|
| `1.2` vs `1.2.0.0` | Equal: missing parts are zeros. |
| `1.9` vs `1.10` | `1.10` is newer: parts compare as numbers. |
| `1.2.0-beta.2` vs `1.2.0-beta.10` vs `1.2.0` | Each is newer than the one before. A prerelease is older than its release. |
| `1.2.0+abc123` vs `1.2.0` | Equal: `+build` metadata is ignored, so `AssemblyInformationalVersion` works as it is. |

- A `currentVersion` with no version in it, such as `dev`, throws `ArgumentException` before anything is sent.
- A latest release whose tag has no version, such as `nightly`, is an `InvalidVersion` failure.
- With `IncludePrereleases`, users on any version are offered prereleases too. A common pattern is to turn it on only for users already on a prerelease, or behind a "beta updates" setting.

### Picking an asset

`release.FindAssetForPlatform()` picks the asset built for this OS and architecture from the words in the file names. See [GitHubRelease](#githubrelease).

### Downloading assets

`DownloadAssetAsync` downloads with [`WebContent.DownloadFileAsync`](../Web/WebContent.md#downloading), so the file is atomic, and `options` carries overwriting, progress, a size limit and resuming. On top of that, it finds the expected SHA-256. The first of these wins:

1. `DownloadOptions.Sha256`, if you set one.
2. The hash GitHub recorded on upload (`GitHubReleaseAsset.Sha256`). Assets uploaded since mid-2025 have one.
3. A `{asset}.sha256` or `{asset}.sha256sum` file in the release, holding a lone hash or a `sha256sum` line.
4. The asset's line in a checksum list in the release, such as `SHA256SUMS` or `app_1.0_checksums.txt`. Both `sha256sum` output (`hash  name`, `hash *name`) and BSD style (`SHA256 (name) = hash`) are read. File names match exactly, ignoring folders.

A mismatch is a `ChecksumMismatch` failure that saves nothing. With none of these, the file is saved unchecked; set `DownloadOptions.Sha256` to insist on one.

- **A checksum file that can't be read** fails the download with that failure, and nothing is downloaded. A `.sha256` file without a hash in it is `InvalidContent`.
- **`token`.** Without one, the asset comes from `DownloadUrl`. With one, it comes through the API (`ApiUrl`), which is how private repositories' assets are downloaded. GitHub redirects to its file storage, and the token isn't sent there. Checksum files come the same way.
- **`asset` must be one of `release.Assets`**, or it throws `ArgumentException`.
- **Failures** use [`WebContent.ErrorCodes`](../Web/WebContent.md#errorcodes), not `GitHubReleases.ErrorCodes`.

### Download URLs without the API

`GetAssetUrl("owner/app", "app-linux-x64.tar.gz")` gives `https://github.com/owner/app/releases/latest/download/app-linux-x64.tar.gz`. Pass a `tag` for `…/releases/download/{tag}/…`, and a `baseAddress` such as `https://github.example.com/` for GitHub Enterprise Server. The tag and the name are escaped.

- **Use it when asset names are predictable**, with no version in them. Pass the URL to `WebContent.DownloadFileAsync`.
- **No API call** is made, so it doesn't count against the rate limit.
- **Public repositories only.**
- **"Latest" is GitHub's latest release**, never a prerelease.
- **A missing asset** only shows up when you download it, as `WebContent.ErrorCodes.NotFound`.

### ErrorCodes

`GitHubReleases.ErrorCodes` holds the `Error.Code` values the lookups fail with. Failures from an HTTP response carry an `HttpRequestException` with the `StatusCode` in `Error.Exception`, and their message ends with GitHub's own message.

| Constant | When |
|---|---|
| `NotFound` | 404: no such repository or release, the repository has no releases, or the token can't see it (GitHub answers 404 for private repositories the token has no access to). |
| `Unauthorized` | 401: the token is invalid or expired. |
| `Forbidden` | 403: the token can't read the repository. |
| `RateLimited` | 429, or a 403 from GitHub's rate limits, with nothing cached to fall back on. |
| `Failed` | Any other status, or a response that couldn't be read. |
| `Network` | No response, with nothing cached to fall back on: no connection, a DNS failure or a timeout. |
| `InvalidVersion` | `CheckForUpdateAsync` only: the latest release's tag has no version in it. |

### GitHubReleaseOptions

`sealed record GitHubReleaseOptions`: options for the lookups. Null uses the defaults: no token, no prereleases, no cache.

| Property | Description |
|---|---|
| `string? Token` | A token for private repositories, or for the higher rate limit. A fine-grained personal access token with read-only **Contents** permission is enough. Null sends none, which works for public repositories. |
| `bool IncludePrereleases` | Whether `GetLatestAsync` and `CheckForUpdateAsync` consider prereleases. Doesn't apply to `GetByTagAsync` or `ListAsync`. |
| `string? CacheDirectory` | A folder to cache API responses in, e.g. a folder under [`AppPaths.Cache`](../IO/AppPaths.md). Null caches nothing. |
| `TimeSpan CacheDuration` | How long a cached response is used without asking GitHub. Defaults to an hour. `TimeSpan.Zero` always asks, which still saves rate limit through `304`s and still falls back to the cache. |

### GitHubRelease

`sealed record GitHubRelease`: a release, as the API describes it.

| Member | Description |
|---|---|
| `string TagName` | The tag, e.g. `v1.2.0`. |
| `string Name` | The title, or the tag if it has none. |
| `string? Notes` | The release notes, in Markdown, or null. |
| `Uri? Url` | The release's page on GitHub, for a "what's new" link. |
| `DateTimeOffset? PublishedAt` | When it was published, or null for a draft. |
| `bool IsPrerelease` · `bool IsDraft` | How it's marked. Drafts only show up with a token that can push to the repository. |
| `IReadOnlyList<GitHubReleaseAsset> Assets` | The attached files, in GitHub's order. |
| `Version? Version` | The version in the tag, from its first digit, without any prerelease label: `v1.2.0-beta.1` is `1.2.0`. Null if the tag has no version. |
| `GitHubReleaseAsset? GetAsset(string name)` | The asset with that name, ignoring case, or null. |
| `GitHubReleaseAsset? FindAssetForPlatform(OS? os = null, Architecture? architecture = null, Func<GitHubReleaseAsset, bool>? filter = null)` | The asset built for an OS and architecture, or null. Null `os` and `architecture` are the ones this is running on. See below. |

`FindAssetForPlatform` goes by the words in the file names.
- **OS words:**
  - Windows: `windows`, `win64`, `msvc`, `.exe`, `.msi`
  - macOS: `macos`, `osx`, `darwin`, `.dmg`
  - Linux: `linux`, `.AppImage`, `.deb`, `.rpm`
  - others: `android`/`.apk`, `ios`/`.ipa`, `freebsd`
- **Architecture words:**
  - x64: `x64`, `x86_64`, `amd64`
  - arm64: `arm64`, `aarch64`
  - x86: `x86`, `i386`, `i686`
  - arm: `arm`, `armv7`, `armhf`
- **Target triples** such as `aarch64-apple-darwin` and `x86_64-pc-windows-msvc` work too.
- **Mapped systems:** iPadOS uses iOS assets, and Mac Catalyst uses macOS ones.

How it chooses:
- An asset must name the OS, and no other. An asset naming a different architecture is skipped.
- Among the rest, one naming the architecture beats a `universal` one (which suits x64 and arm64). A `universal` one beats one that names no architecture.
- A glibc build beats a `musl` one. Otherwise, the asset listed first wins.
- Checksum and signature files are never picked.
- Use `filter` to choose between formats, e.g. `a => a.Name.EndsWith(".zip")`. When names don't follow these patterns, use `GetAsset` with a name you build.

### GitHubReleaseAsset

`sealed record GitHubReleaseAsset`: a file attached to a release.

| Property | Description |
|---|---|
| `string Name` | The file name. |
| `Uri DownloadUrl` | The public download URL. Works without a token for public repositories, and doesn't count against the API rate limit. |
| `Uri? ApiUrl` | The asset's API URL, which downloads it with a token: the way to download from a private repository. |
| `long Size` | The size in bytes. |
| `string? ContentType` | The MIME type the uploader gave, or null. |
| `string? Sha256` | The SHA-256 hash GitHub computed on upload, as lowercase hex, or null for assets uploaded before mid-2025. |
| `long DownloadCount` | How many times it's been downloaded. |

### GitHubUpdateCheck

`sealed record GitHubUpdateCheck(bool IsUpdateAvailable, string CurrentVersion, GitHubRelease Latest)`: what `CheckForUpdateAsync` found. `CurrentVersion` is the version that was checked, as given (a `Version` is formatted with `ToString()`). `Latest` is the latest release, whether or not it's newer.

## Tokens

Public repositories need no token. For a private one, or for the higher rate limit, use a [fine-grained personal access token](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/managing-your-personal-access-tokens) limited to the one repository, with only read-only **Contents** permission. A token inside a shipped app can be extracted, so never ship one with more access than that. The token is only sent to the API address, in the `Authorization` header, and it isn't stored or logged. The cache files are keyed by a hash of it, so they don't contain it.

## Examples

```csharp
using System.Reflection;
using Niddy.Core.GitHub;
using Niddy.Core.IO;
using Niddy.Core.Results;
using Niddy.Core.Web;

// An update check on startup that's cached for an hour and works offline
GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(paths.Cache, "releases") };
Version current = Assembly.GetEntryAssembly()!.GetName().Version!;
Result<GitHubUpdateCheck> check = await GitHubReleases.CheckForUpdateAsync("vonderborch/MyApp", current, options);
if (check.TryGetValue(out GitHubUpdateCheck update) && update.IsUpdateAvailable)
{
    // Download the build for this machine, verified, with progress
    GitHubReleaseAsset? asset = update.Latest.FindAssetForPlatform(filter: a => !a.Name.EndsWith(".msi"));
    if (asset is not null)
    {
        Result<FileInfo> saved = await GitHubReleases.DownloadAssetAsync(update.Latest, asset,
            Path.Combine(paths.Cache, asset.Name),
            new DownloadOptions { Overwrite = true, Progress = progress, Resume = true });
    }
}

// Release notes for every version since the user's, newest first
Result<IReadOnlyList<GitHubRelease>> releases = await GitHubReleases.ListAsync("vonderborch/MyApp", 50, options);
IEnumerable<string> notes = releases.GetValueOrDefault([])
    .Where(r => !r.IsDraft && r.Version > current)
    .Select(r => $"## {r.Name}\n\n{r.Notes}");

// A private repository
Result<GitHubRelease> release = await GitHubReleases.GetByTagAsync("team/tools", "v3.1.0",
    new GitHubReleaseOptions { Token = token });

// No API call: a predictable asset name in the latest release
string url = GitHubReleases.GetAssetUrl("vonderborch/MyApp", "myapp-linux-x64.tar.gz");
Result<FileInfo> file = await WebContent.DownloadFileAsync(url, "myapp.tar.gz");

// A console tool, synchronously
Result<GitHubRelease> newest = GitHubReleases.GetLatest("vonderborch/MyApp");
```

## See also

- [WebContent](../Web/WebContent.md): `DownloadOptions`, `DownloadProgress` and the download error codes
- [Platform](../SystemInfo/Platform.md): the `OS` that `FindAssetForPlatform` uses
- [Archive](../IO/Archives/Archive.md), to extract a downloaded release safely
- [GitHubIssues](GitHubIssues.md): open an issue from the app
- [Result](../Results/Result.md), [Error](../Results/Error.md)
