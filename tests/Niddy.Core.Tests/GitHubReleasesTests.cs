using System.Net;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using Niddy.Core.GitHub;
using Niddy.Core.Results;
using Niddy.Core.SystemInfo;
using Niddy.Core.Web;

namespace Niddy.Core.Tests;

// Nothing here reaches GitHub: every request goes to a fake handler.
public sealed class GitHubReleasesTests : IDisposable
{
    private const string Api = "https://api.github.com/repos/owner/repo/";

    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-releases-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(this._dir, recursive: true);

    [Fact]
    public async Task GetLatestAsync_ReadsTheRelease()
    {
        byte[] bytes = "zip"u8.ToArray();
        JsonObject release = Release("v1.2.0", Asset("app.zip", bytes, digest: true));
        release["name"] = "Version 1.2";
        release["body"] = "## What's new";
        release["published_at"] = "2026-03-04T05:06:07Z";
        FakeHandler handler = new(_ => Json(release));

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            new GitHubReleaseOptions { Token = "secret" }, new HttpClient(handler), Token);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(Api + "releases/latest", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer secret", request.Headers.Authorization!.ToString());
        Assert.Equal("2022-11-28", Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
        Assert.NotEmpty(request.Headers.UserAgent);

        GitHubRelease latest = result.Value!;
        Assert.Equal("v1.2.0", latest.TagName);
        Assert.Equal("Version 1.2", latest.Name);
        Assert.Equal("## What's new", latest.Notes);
        Assert.Equal(new Uri("https://github.com/owner/repo/releases/tag/v1.2.0"), latest.Url);
        Assert.Equal(new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero), latest.PublishedAt);
        Assert.Equal(new Version(1, 2, 0), latest.Version);
        Assert.False(latest.IsPrerelease);

        GitHubReleaseAsset asset = Assert.Single(latest.Assets);
        Assert.Equal("app.zip", asset.Name);
        Assert.Equal(new Uri("https://github.com/owner/repo/releases/download/v1.2.0/app.zip"), asset.DownloadUrl);
        Assert.Equal(new Uri(Api + "releases/assets/app.zip"), asset.ApiUrl);
        Assert.Equal(3, asset.Size);
        Assert.Equal("application/zip", asset.ContentType);
        Assert.Equal(Sha256(bytes), asset.Sha256);
        Assert.Equal(7, asset.DownloadCount);
        Assert.Same(asset, latest.GetAsset("APP.ZIP"));
    }

    [Fact]
    public async Task GetLatestAsync_WithoutATitleOrNotes_UsesTheTag()
    {
        JsonObject release = Release("v1.0");
        release["name"] = "";
        release["body"] = null;

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            httpClient: new HttpClient(new FakeHandler(_ => Json(release))), cancellationToken: Token);

        Assert.Equal("v1.0", result.Value!.Name);
        Assert.Null(result.Value.Notes);
        Assert.Null(result.Value.PublishedAt);
        Assert.Empty(result.Value.Assets);
    }

    [Fact]
    public async Task GetLatestAsync_UsesTheClientsBaseAddress()
    {
        FakeHandler handler = new(_ => Json(Release("v1")));
        HttpClient client = new(handler) { BaseAddress = new Uri("https://github.example.com/api/v3") };

        await GitHubReleases.GetLatestAsync("owner/repo", httpClient: client, cancellationToken: Token);

        Assert.Equal("https://github.example.com/api/v3/repos/owner/repo/releases/latest",
            handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task GetLatestAsync_ApiAddressOption_TakesPrecedenceOverTheClients()
    {
        FakeHandler handler = new(_ => Json(Release("v1")));
        HttpClient client = new(handler) { BaseAddress = new Uri("https://other.example.com/api/v3/") };
        GitHubReleaseOptions options = new() { ApiAddress = new Uri("https://github.example.com/api/v3/") };

        await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);

        Assert.Equal("https://github.example.com/api/v3/repos/owner/repo/releases/latest",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, GitHubReleases.ErrorCodes.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, GitHubReleases.ErrorCodes.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubReleases.ErrorCodes.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubReleases.ErrorCodes.RateLimited)]
    [InlineData(HttpStatusCode.InternalServerError, GitHubReleases.ErrorCodes.Failed)]
    public async Task GetLatestAsync_FailedStatus_IsAFailure(HttpStatusCode status, string code)
    {
        FakeHandler handler = new(_ => Json(new JsonObject { ["message"] = "Nope" }, status));

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(code, result.Error?.Code);
        Assert.EndsWith("GitHub said: Nope", result.Error?.Message);
        Assert.Equal(status, Assert.IsType<HttpRequestException>(result.Error?.Exception).StatusCode);
    }

    [Fact]
    public async Task GetLatestAsync_RateLimited403_IsRateLimited()
    {
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Json(new JsonObject { ["message"] = "API rate limit exceeded" },
                HttpStatusCode.Forbidden);
            response.Headers.Add("x-ratelimit-remaining", "0");
            return response;
        });

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.RateLimited, result.Error?.Code);
    }

    [Fact]
    public async Task GetLatestAsync_NoConnectionOrTimeout_IsANetworkFailure()
    {
        HttpClient unreachable = new(new FakeHandler(_ => throw new HttpRequestException("No such host")));
        HttpClient slow = new(new FakeHandler(_ => throw new TaskCanceledException("Timed out")));

        Result<GitHubRelease> noConnection = await GitHubReleases.GetLatestAsync("owner/repo", httpClient: unreachable,
            cancellationToken: Token);
        Result<GitHubRelease> timedOut = await GitHubReleases.GetLatestAsync("owner/repo", httpClient: slow,
            cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.Network, noConnection.Error?.Code);
        Assert.Equal("Couldn't reach GitHub: No such host", noConnection.Error?.Message);
        Assert.Equal(GitHubReleases.ErrorCodes.Network, timedOut.Error?.Code);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("""{"name": "no tag"}""")]
    [InlineData("""{"tag_name": "v1", "assets": [{"name": "a.zip"}]}""")]
    public async Task GetLatestAsync_UnreadableResponse_Fails(string content)
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.Failed, result.Error?.Code);
        Assert.StartsWith("GitHub's response couldn't be read", result.Error?.Message);
    }

    [Fact]
    public async Task GetLatestAsync_IncludingPrereleases_PicksTheHighestVersionThatIsntADraft()
    {
        JsonArray releases =
        [
            Release("v2.0.0", draft: true),
            Release("v1.10.0"),
            Release("v2.0.0-beta.10", prerelease: true),
            Release("v2.0.0-beta.2", prerelease: true),
            Release("v1.9.0"),
        ];
        FakeHandler handler = new(_ => Json(releases));

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            new GitHubReleaseOptions { IncludePrereleases = true }, new HttpClient(handler), Token);

        Assert.Equal(Api + "releases?per_page=30", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("v2.0.0-beta.10", result.Value!.TagName);
        Assert.True(result.Value.IsPrerelease);
    }

    [Fact]
    public async Task GetLatestAsync_IncludingPrereleases_WithoutVersions_PicksTheNewest()
    {
        JsonArray releases = [Release("nightly"), Release("stable")];

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            new GitHubReleaseOptions { IncludePrereleases = true }, new HttpClient(new FakeHandler(_ => Json(releases))),
            Token);

        Assert.Equal("nightly", result.Value!.TagName);
    }

    [Fact]
    public async Task GetLatestAsync_IncludingPrereleases_OnlyDrafts_IsNotFound()
    {
        JsonArray releases = [Release("v1", draft: true)];

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo",
            new GitHubReleaseOptions { IncludePrereleases = true }, new HttpClient(new FakeHandler(_ => Json(releases))),
            Token);

        Assert.Equal(GitHubReleases.ErrorCodes.NotFound, result.Error?.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("owner")]
    [InlineData("owner/repo/extra")]
    [InlineData("owner/")]
    [InlineData("my owner/repo")]
    public async Task BadRepositories_Throw(string repository)
    {
        HttpClient client = new(new FakeHandler(_ => Json(Release("v1"))));

        await Assert.ThrowsAnyAsync<ArgumentException>(() => GitHubReleases.GetLatestAsync(repository, httpClient: client,
            cancellationToken: Token));
        Assert.ThrowsAny<ArgumentException>(() => GitHubReleases.GetAssetUrl(repository, "a.zip"));
    }

    [Fact]
    public async Task GetByTagAsync_EscapesTheTag()
    {
        FakeHandler handler = new(_ => Json(Release("v1.0+build 5")));

        Result<GitHubRelease> result = await GitHubReleases.GetByTagAsync("owner/repo", "v1.0+build 5",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal("v1.0+build 5", result.Value!.TagName);
        Assert.Equal(Api + "releases/tags/v1.0%2Bbuild%205", handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task GetByTagAsync_MissingTag_IsNotFoundAndSaysWhichTag()
    {
        FakeHandler handler = new(_ => Json(new JsonObject { ["message"] = "Not Found" }, HttpStatusCode.NotFound));

        Result<GitHubRelease> result = await GitHubReleases.GetByTagAsync("owner/repo", "v9",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.NotFound, result.Error?.Code);
        Assert.Contains("v9", result.Error?.Message);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => GitHubReleases.GetByTagAsync("owner/repo", " ",
            httpClient: new HttpClient(handler), cancellationToken: Token));
    }

    [Theory]
    [InlineData(5, new[] { 20 }, 5, new[] { "per_page=5&page=1" })]
    [InlineData(150, new[] { 100, 50 }, 150, new[] { "per_page=100&page=1", "per_page=100&page=2" })]
    [InlineData(250, new[] { 100, 30 }, 130, new[] { "per_page=100&page=1", "per_page=100&page=2" })]
    [InlineData(200, new[] { 100, 100 }, 200, new[] { "per_page=100&page=1", "per_page=100&page=2" })]
    [InlineData(30, new[] { 0 }, 0, new[] { "per_page=30&page=1" })]
    public async Task ListAsync_ReadsAsManyPagesAsItNeeds(int maxCount, int[] pageSizes, int expected, string[] queries)
    {
        int served = 0;
        FakeHandler handler = new(request =>
        {
            int page = int.Parse(request.RequestUri!.Query.Split("page=")[^1]);
            JsonArray releases = [.. Enumerable.Range(0, pageSizes[page - 1]).Select(_ => (JsonNode)Release($"v{++served}"))];
            return Json(releases);
        });

        Result<IReadOnlyList<GitHubRelease>> result = await GitHubReleases.ListAsync("owner/repo", maxCount,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(expected, result.Value!.Count);
        Assert.Equal(queries.Select(query => Api + "releases?" + query), handler.Requests.Select(r => r.RequestUri!.AbsoluteUri));
        Assert.Equal("v1", result.Value.FirstOrDefault()?.TagName ?? "v1");
    }

    [Fact]
    public async Task ListAsync_FailureOnALaterPage_IsAFailure()
    {
        FakeHandler handler = new(request => request.RequestUri!.Query.EndsWith("page=1")
            ? Json(new JsonArray([.. Enumerable.Range(0, 100).Select(i => (JsonNode)Release($"v{i}"))]))
            : Json(new JsonObject(), HttpStatusCode.BadGateway));

        Result<IReadOnlyList<GitHubRelease>> result = await GitHubReleases.ListAsync("owner/repo", 200,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.Failed, result.Error?.Code);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubReleases.ListAsync("owner/repo", 0,
            httpClient: new HttpClient(handler), cancellationToken: Token));
    }

    [Theory]
    [InlineData("1.4.0", "v1.5.0", true)]
    [InlineData("1.5.0", "v1.5.0", false)]
    [InlineData("v1.5", "release-1.5.0.0", false)]
    [InlineData("1.5.0-beta.2", "v1.5.0", true)]
    [InlineData("1.5.0-beta.2", "v1.5.0-beta.10", true)]
    [InlineData("1.5.0-beta.10", "v1.5.0-beta.2", false)]
    [InlineData("1.5.0-alpha", "v1.5.0-beta", true)]
    [InlineData("1.5.0-rc.1", "v1.5.0-rc.1.1", true)]
    [InlineData("1.5.0+abc123", "v1.5.0", false)]
    [InlineData("2.0.0", "v1.9.9", false)]
    [InlineData("1.9", "v1.10", true)]
    public async Task CheckForUpdateAsync_ComparesLikeSemanticVersioning(string current, string latest, bool newer)
    {
        FakeHandler handler = new(_ => Json(Release(latest)));

        Result<GitHubUpdateCheck> result = await GitHubReleases.CheckForUpdateAsync("owner/repo", current,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(newer, result.Value!.IsUpdateAvailable);
        Assert.Equal(current, result.Value.CurrentVersion);
        Assert.Equal(latest, result.Value.Latest.TagName);
    }

    [Fact]
    public async Task CheckForUpdateAsync_TakesAVersion()
    {
        FakeHandler handler = new(_ => Json(Release("v1.2.1")));

        Result<GitHubUpdateCheck> result = await GitHubReleases.CheckForUpdateAsync("owner/repo", new Version(1, 2, 0, 0),
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.True(result.Value!.IsUpdateAvailable);
        Assert.Equal("1.2.0.0", result.Value.CurrentVersion);
    }

    [Fact]
    public async Task CheckForUpdateAsync_TagWithoutAVersion_IsInvalidVersion()
    {
        FakeHandler handler = new(_ => Json(Release("latest")));

        Result<GitHubUpdateCheck> result = await GitHubReleases.CheckForUpdateAsync("owner/repo", "1.0",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.InvalidVersion, result.Error?.Code);
    }

    [Fact]
    public async Task CheckForUpdateAsync_CurrentVersionWithoutAVersion_ThrowsBeforeSending()
    {
        FakeHandler handler = new(_ => Json(Release("v1")));

        await Assert.ThrowsAsync<ArgumentException>(() => GitHubReleases.CheckForUpdateAsync("owner/repo", "dev",
            httpClient: new HttpClient(handler), cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => GitHubReleases.CheckForUpdateAsync("owner/repo",
            (Version)null!, httpClient: new HttpClient(handler), cancellationToken: Token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task CheckForUpdateAsync_FailedLookup_IsThatFailure()
    {
        FakeHandler handler = new(_ => Json(new JsonObject(), HttpStatusCode.NotFound));

        Result<GitHubUpdateCheck> result = await GitHubReleases.CheckForUpdateAsync("owner/repo", "1.0",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubReleases.ErrorCodes.NotFound, result.Error?.Code);
    }

    [Theory]
    [InlineData("v1.2.3", "1.2.3")]
    [InlineData("release-1.2", "1.2")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("v2-beta.1", "2.0")]
    [InlineData("v1.0rc1", "1.0")]
    [InlineData("nightly", null)]
    [InlineData("1.2.3.4.5", null)]
    public void Version_IsReadFromTheTag(string tag, string? expected) =>
        Assert.Equal(expected is null ? null : Version.Parse(expected), new GitHubRelease { TagName = tag, Name = tag }.Version);

    [Theory]
    [InlineData(OS.Linux, Architecture.X64, "app-1.2-linux-x64.tar.gz")]
    [InlineData(OS.Linux, Architecture.Arm64, "app-1.2-linux-arm64.tar.gz")]
    [InlineData(OS.Linux, Architecture.X86, null)]
    [InlineData(OS.MacOS, Architecture.Arm64, "app_Darwin_universal.zip")]
    [InlineData(OS.MacOS, Architecture.X64, "app_Darwin_universal.zip")]
    [InlineData(OS.MacCatalyst, Architecture.Arm64, "app_Darwin_universal.zip")]
    [InlineData(OS.Windows, Architecture.X64, "app-x86_64-pc-windows-msvc.zip")]
    [InlineData(OS.Windows, Architecture.Arm64, "app-aarch64-pc-windows-msvc.zip")]
    [InlineData(OS.Android, Architecture.Arm64, "app-1.2.apk")]
    [InlineData(OS.FreeBSD, Architecture.X64, null)]
    [InlineData(OS.iPadOS, Architecture.Arm64, null)]
    public void FindAssetForPlatform_PicksByTheWordsInTheName(OS os, Architecture architecture, string? expected)
    {
        GitHubRelease release = ReleaseWithAssets(
            "app-1.2-linux-x64-musl.tar.gz",
            "app-1.2-linux-x64.tar.gz",
            "app-1.2-linux-x64.tar.gz.sha256",
            "app-1.2-linux-arm64.tar.gz",
            "app_Darwin_universal.zip",
            "app-x86_64-pc-windows-msvc.zip",
            "app-aarch64-pc-windows-msvc.zip",
            "app-1.2.apk",
            "SHA256SUMS",
            "checksums-windows.txt");

        Assert.Equal(expected, release.FindAssetForPlatform(os, architecture)?.Name);
    }

    [Fact]
    public void FindAssetForPlatform_ArchitectureBeatsUniversalBeatsNone()
    {
        GitHubRelease release = ReleaseWithAssets("App-1.2.dmg", "app-macos-universal.zip", "app-aarch64-apple-darwin.tar.gz");

        Assert.Equal("app-aarch64-apple-darwin.tar.gz", release.FindAssetForPlatform(OS.MacOS, Architecture.Arm64)?.Name);
        Assert.Equal("app-macos-universal.zip", release.FindAssetForPlatform(OS.MacOS, Architecture.X64)?.Name);
        Assert.Equal("App-1.2.dmg", release.FindAssetForPlatform(OS.MacOS, Architecture.X86)?.Name);
    }

    [Fact]
    public void FindAssetForPlatform_SkipsAssetsNamingTwoSystemsAndHonorsTheFilter()
    {
        GitHubRelease release = ReleaseWithAssets("app-linux-windows-x64.zip", "app-linux-x64.AppImage", "app-linux-x64.deb");

        Assert.Equal("app-linux-x64.AppImage", release.FindAssetForPlatform(OS.Linux, Architecture.X64)?.Name);
        Assert.Equal("app-linux-x64.deb",
            release.FindAssetForPlatform(OS.Linux, Architecture.X64, a => a.Name.EndsWith(".deb"))?.Name);
        Assert.Null(release.FindAssetForPlatform(OS.Linux, Architecture.X64, a => a.Name.EndsWith(".zip")));
    }

    [Fact]
    public void FindAssetForPlatform_DefaultsToThisPlatform()
    {
        string os = Platform.Info.OS switch
        {
            OS.Windows => "windows",
            OS.MacOS => "macos",
            _ => "linux",
        };
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        GitHubRelease release = ReleaseWithAssets($"app-{os}-{architecture}.zip");

        Assert.Equal(Platform.Info.OS is OS.Windows or OS.MacOS or OS.Linux ? $"app-{os}-{architecture}.zip" : null,
            release.FindAssetForPlatform()?.Name);
    }

    [Fact]
    public async Task DownloadAssetAsync_ChecksGitHubsDigest()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("the app");
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", bytes, digest: true)));
        FakeHandler handler = new(_ => Bytes(bytes));
        string path = Path.Combine(this._dir, "app.zip");

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0], path,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(release.Assets[0].DownloadUrl, request.RequestUri);
        Assert.Null(request.Headers.Authorization);
    }

    [Fact]
    public async Task DownloadAssetAsync_DigestMismatch_SavesNothing()
    {
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", "expected"u8.ToArray(), digest: true)));
        string path = Path.Combine(this._dir, "app.zip");

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0], path,
            httpClient: new HttpClient(new FakeHandler(_ => Bytes("tampered"u8.ToArray()))), cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.ChecksumMismatch, result.Error?.Code);
        Assert.False(File.Exists(path));
    }

    [Theory]
    [InlineData("app.zip.sha256", "{0}")]
    [InlineData("app.zip.sha256", "{0}  app.zip\n")]
    [InlineData("app.zip.sha256sum", "{0} *dist/app.zip")]
    [InlineData("SHA256SUMS", "# release checksums\n{1}  other.zip\n{0}  app.zip\n")]
    [InlineData("app_1.0_checksums.txt", "{1} *other.zip\r\n{0} *app.zip\r\n")]
    [InlineData("checksums.txt", "SHA256 (other.zip) = {1}\nSHA256 (app.zip) = {0}\n")]
    public async Task DownloadAssetAsync_UsesAChecksumFileInTheRelease(string checksumName, string format)
    {
        byte[] bytes = "the app"u8.ToArray();
        string checksums = string.Format(format, Sha256(bytes).ToUpperInvariant(), Sha256("other"u8.ToArray()));
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", bytes), Asset(checksumName, [])));

        foreach ((byte[] served, bool saved) in new[] { (bytes, true), ("tampered"u8.ToArray(), false) })
        {
            FakeHandler handler = new(request => request.RequestUri!.AbsolutePath.EndsWith(checksumName)
                ? Bytes(Encoding.UTF8.GetBytes(checksums))
                : Bytes(served));
            string path = Path.Combine(this._dir, $"app-{saved}.zip");

            Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0], path,
                httpClient: new HttpClient(handler), cancellationToken: Token);

            Assert.Equal(saved, result.IsSuccess);
            Assert.Equal(saved ? null : WebContent.ErrorCodes.ChecksumMismatch, result.Error?.Code);
            Assert.Equal(2, handler.Requests.Count);
        }
    }

    [Fact]
    public async Task DownloadAssetAsync_ManifestThatDoesntListIt_SavesItUnchecked()
    {
        byte[] bytes = "the app"u8.ToArray();
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", bytes), Asset("SHA256SUMS", [])));
        string checksums = $"{Sha256(bytes)}  something-else.zip\n";
        FakeHandler handler = new(request => request.RequestUri!.AbsolutePath.EndsWith("SHA256SUMS")
            ? Bytes(Encoding.UTF8.GetBytes(checksums))
            : Bytes(bytes));

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0],
            Path.Combine(this._dir, "app.zip"), httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DownloadAssetAsync_SidecarWithoutAHash_FailsWithoutDownloading()
    {
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", [1]), Asset("app.zip.sha256", [])));
        FakeHandler handler = new(_ => Bytes("not a hash"u8.ToArray()));

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0],
            Path.Combine(this._dir, "app.zip"), httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.InvalidContent, result.Error?.Code);
        Assert.Single(handler.Requests);
        Assert.Empty(Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task DownloadAssetAsync_ChecksumFileMissing_IsThatFailure()
    {
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", [1]), Asset("SHA256SUMS", [])));
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0],
            Path.Combine(this._dir, "app.zip"), httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.NotFound, result.Error?.Code);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DownloadAssetAsync_GivenHash_WinsAndNoChecksumFileIsRead()
    {
        byte[] bytes = "the app"u8.ToArray();
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", bytes), Asset("SHA256SUMS", [])));
        FakeHandler handler = new(_ => Bytes(bytes));

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0],
            Path.Combine(this._dir, "app.zip"), new DownloadOptions { Sha256 = "sha256:" + Sha256(bytes) },
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.True(result.IsSuccess);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DownloadAssetAsync_WithAToken_DownloadsThroughTheApi()
    {
        byte[] bytes = "private app"u8.ToArray();
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", bytes), Asset("app.zip.sha256", [])));
        FakeHandler handler = new(request => request.RequestUri!.AbsolutePath.EndsWith(".sha256")
            ? Bytes(Encoding.UTF8.GetBytes(Sha256(bytes)))
            : Bytes(bytes));

        Result<FileInfo> result = await GitHubReleases.DownloadAssetAsync(release, release.Assets[0],
            Path.Combine(this._dir, "app.zip"), token: "secret", httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.All(handler.Requests, request =>
        {
            Assert.StartsWith(Api + "releases/assets/", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Bearer secret", request.Headers.Authorization!.ToString());
            Assert.Equal("application/octet-stream", Assert.Single(request.Headers.Accept).MediaType);
        });
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DownloadAssetAsync_AssetFromAnotherRelease_Throws()
    {
        GitHubRelease release = ReleaseFromJson(Release("v1", Asset("app.zip", [1])));
        GitHubReleaseAsset other = new() { Name = "other.zip", DownloadUrl = new Uri("https://example.com/other.zip") };

        await Assert.ThrowsAsync<ArgumentException>(() => GitHubReleases.DownloadAssetAsync(release, other,
            Path.Combine(this._dir, "other.zip"), cancellationToken: Token));
    }

    [Fact]
    public async Task Cache_FreshResponse_IsServedWithoutARequest()
    {
        FakeHandler handler = new(_ => Json(Release("v1")));
        HttpClient client = new(handler);
        GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(this._dir, "cache") };

        Result<GitHubRelease> first = await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);
        Result<GitHubRelease> second = await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);

        Assert.Equal("v1", first.Value!.TagName);
        Assert.Equal("v1", second.Value!.TagName);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Cache_IsKeptPerToken()
    {
        FakeHandler handler = new(_ => Json(Release("v1")));
        HttpClient client = new(handler);
        string cache = Path.Combine(this._dir, "cache");

        await GitHubReleases.GetLatestAsync("owner/repo", new GitHubReleaseOptions { CacheDirectory = cache }, client, Token);
        await GitHubReleases.GetLatestAsync("owner/repo", new GitHubReleaseOptions { CacheDirectory = cache, Token = "a" },
            client, Token);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Cache_StaleResponse_IsRevalidatedWithItsETag()
    {
        int calls = 0;
        FakeHandler handler = new(_ =>
        {
            if (++calls > 1)
            {
                return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            HttpResponseMessage response = Json(Release("v1"));
            response.Headers.ETag = new EntityTagHeaderValue("\"abc\"");
            return response;
        });
        HttpClient client = new(handler);
        GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(this._dir, "cache"), CacheDuration = TimeSpan.Zero };

        await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);
        Result<GitHubRelease> second = await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);

        Assert.Equal("v1", second.Value!.TagName);
        Assert.Equal("\"abc\"", Assert.Single(handler.Requests[1].Headers.IfNoneMatch).Tag);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task Cache_StaleResponse_IsServedWhenGitHubCantAnswer(int failure)
    {
        int calls = 0;
        FakeHandler handler = new(_ => ++calls == 1
            ? Json(Release("v1"))
            : failure == 0
                ? throw new HttpRequestException("No such host")
                : Json(new JsonObject(), (HttpStatusCode)failure));
        HttpClient client = new(handler);
        GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(this._dir, "cache"), CacheDuration = TimeSpan.Zero };

        await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);
        Result<GitHubRelease> second = await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);

        Assert.Equal("v1", second.Value!.TagName);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task Cache_StaleResponse_IsReplacedByANewOne()
    {
        int calls = 0;
        FakeHandler handler = new(_ => Json(Release(++calls == 1 ? "v1" : "v2")));
        HttpClient client = new(handler);
        GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(this._dir, "cache"), CacheDuration = TimeSpan.Zero };

        await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);
        Result<GitHubRelease> second = await GitHubReleases.GetLatestAsync("owner/repo", options, client, Token);

        Assert.Equal("v2", second.Value!.TagName);
    }

    [Fact]
    public async Task Cache_NotFound_IsntCachedOrHidden()
    {
        FakeHandler handler = new(_ => Json(new JsonObject(), HttpStatusCode.NotFound));
        GitHubReleaseOptions options = new() { CacheDirectory = Path.Combine(this._dir, "cache") };

        Result<GitHubRelease> result = await GitHubReleases.GetLatestAsync("owner/repo", options, new HttpClient(handler), Token);

        Assert.Equal(GitHubReleases.ErrorCodes.NotFound, result.Error?.Code);
        Assert.False(Directory.Exists(options.CacheDirectory) && Directory.EnumerateFiles(options.CacheDirectory).Any());
    }

    [Fact]
    public void SyncVersions_SendSynchronously()
    {
        byte[] bytes = "the app"u8.ToArray();
        JsonObject latest = Release("v2.0", Asset("app.zip", bytes, digest: true));
        FakeHandler handler = new(request => request.RequestUri!.AbsolutePath switch
        {
            string path when path.EndsWith("/releases") => Json(new JsonArray(Release("v2.0"), Release("v1.0"))),
            string path when path.EndsWith("/app.zip") => Bytes(bytes),
            _ => Json(latest),
        });
        HttpClient client = new(handler);

        GitHubRelease release = GitHubReleases.GetLatest("owner/repo", httpClient: client, cancellationToken: Token).Value!;
        Assert.Equal("v2.0", GitHubReleases.GetByTag("owner/repo", "v2.0", httpClient: client, cancellationToken: Token).Value!.TagName);
        Assert.Equal(2, GitHubReleases.List("owner/repo", httpClient: client, cancellationToken: Token).Value!.Count);
        Assert.True(GitHubReleases.CheckForUpdate("owner/repo", "1.0", httpClient: client, cancellationToken: Token)
            .Value!.IsUpdateAvailable);
        Assert.False(GitHubReleases.CheckForUpdate("owner/repo", new Version(2, 0), httpClient: client, cancellationToken: Token)
            .Value!.IsUpdateAvailable);
        Result<FileInfo> saved = GitHubReleases.DownloadAsset(release, release.Assets[0], Path.Combine(this._dir, "app.zip"),
            httpClient: client, cancellationToken: Token);

        Assert.True(saved.IsSuccess, saved.Error?.Message);
        Assert.Equal(6, handler.SyncSends);
        Assert.Equal(6, handler.Requests.Count);
    }

    [Theory]
    [InlineData(null, null, "https://github.com/owner/repo/releases/latest/download/app.zip")]
    [InlineData("v1.2.0", null, "https://github.com/owner/repo/releases/download/v1.2.0/app.zip")]
    [InlineData("v1.0+b 1", null, "https://github.com/owner/repo/releases/download/v1.0%2Bb%201/app.zip")]
    [InlineData(null, "https://github.example.com", "https://github.example.com/owner/repo/releases/latest/download/app.zip")]
    public void GetAssetUrl_BuildsTheDownloadUrl(string? tag, string? baseAddress, string expected) =>
        Assert.Equal(expected, GitHubReleases.GetAssetUrl("owner/repo", "app.zip", tag,
            baseAddress is null ? null : new Uri(baseAddress)));

    [Fact]
    public void GetAssetUrl_EscapesTheAssetName()
    {
        Assert.Equal("https://github.com/owner/repo/releases/latest/download/My%20App%20%231.zip",
            GitHubReleases.GetAssetUrl("owner/repo", "My App #1.zip"));
        Assert.ThrowsAny<ArgumentException>(() => GitHubReleases.GetAssetUrl("owner/repo", " "));
    }

    [Theory]
    [InlineData("SHA256SUMS", true)]
    [InlineData("sha256sums.txt", true)]
    [InlineData("app_1.0_checksums.txt", true)]
    [InlineData("SHA256SUMS.asc", false)]
    [InlineData("checksums.txt.sig", false)]
    [InlineData("app.zip", false)]
    public void ChecksumManifest_RecognizesChecksumLists(string name, bool expected) =>
        Assert.Equal(expected, ChecksumManifest.IsManifestName(name));

    [Fact]
    public void ChecksumManifest_MatchesWholeFileNamesOnly()
    {
        string hash = new('a', 64);
        string content = $"{hash}  app.zip.sig\n{new string('b', 64)}  ./dist/app.zip\n";

        Assert.Equal(new string('b', 64), ChecksumManifest.Find(content, "app.zip"));
        Assert.Equal(hash, ChecksumManifest.Find(content, "app.zip.sig"));
        Assert.Null(ChecksumManifest.Find(content, "APP.ZIP"));
        Assert.Null(ChecksumManifest.FindSingle("\n# nothing here\n"));
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static JsonObject Release(string tag, params JsonObject[] assets) => Release(tag, false, false, assets);

    private static JsonObject Release(string tag, bool prerelease = false, bool draft = false, params JsonObject[] assets)
    {
        foreach (JsonObject asset in assets)
        {
            asset["browser_download_url"] = $"https://github.com/owner/repo/releases/download/{tag}/{asset["name"]}";
        }

        return new JsonObject
        {
            ["tag_name"] = tag,
            ["name"] = tag,
            ["html_url"] = $"https://github.com/owner/repo/releases/tag/{tag}",
            ["prerelease"] = prerelease,
            ["draft"] = draft,
            ["assets"] = new JsonArray([.. assets]),
        };
    }

    private static JsonObject Asset(string name, byte[] bytes, bool digest = false) => new()
    {
        ["name"] = name,
        ["url"] = $"{Api}releases/assets/{name}",
        ["size"] = (long)bytes.Length,
        ["content_type"] = "application/zip",
        ["digest"] = digest ? "sha256:" + Sha256(bytes) : null,
        ["download_count"] = 7L,
    };

    private static GitHubRelease ReleaseFromJson(JsonObject json) => GitHubRelease.FromJson(json);

    private static GitHubRelease ReleaseWithAssets(params string[] names) =>
        ReleaseFromJson(Release("v1.2", [.. names.Select(name => Asset(name, []))]));

    private static HttpResponseMessage Json(JsonNode json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Bytes(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public int SyncSends { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(this.Respond(request, cancellationToken));

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.SyncSends++;
            return this.Respond(request, cancellationToken);
        }

        private HttpResponseMessage Respond(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Requests.Add(request);
            HttpResponseMessage response = respond(request);
            response.RequestMessage ??= request;
            return response;
        }
    }
}
