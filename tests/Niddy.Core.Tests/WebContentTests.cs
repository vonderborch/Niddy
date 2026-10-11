using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.Tests;

// Nothing here goes on the network: every request goes to a fake handler.
public sealed class WebContentTests : IDisposable
{
    private const string Url = "https://example.com/files/data.bin";

    private static readonly DownloadOptions Overwrite = new() { Overwrite = true };

    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-web-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(this._dir, recursive: true);

    [Fact]
    public async Task DownloadFileAsync_SavesTheFileAndCreatesFolders()
    {
        byte[] bytes = Bytes(200_000);
        FakeHandler handler = new(_ => Response(bytes));
        string path = Path.Combine(this._dir, "sub", "data.bin");

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, path, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(Path.GetFullPath(path), result.Value.FullName);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(new Uri(Url), request.RequestUri);
        Assert.Equal("Niddy.Core", request.Headers.UserAgent.Single().Product!.Name);
    }

    [Fact]
    public async Task DownloadFileAsync_ExistingFile_FailsWithoutDownloadingUnlessOverwriting()
    {
        string path = Path.Combine(this._dir, "data.bin");
        await File.WriteAllTextAsync(path, "old", Token);
        FakeHandler handler = new(_ => Response("new"u8.ToArray()));
        HttpClient client = new(handler);

        Result<FileInfo> kept = await WebContent.DownloadFileAsync(Url, path, httpClient: client, cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.FileExists, kept.Error?.Code);
        Assert.Empty(handler.Requests);
        Assert.Equal("old", await File.ReadAllTextAsync(path, Token));

        Result<FileInfo> replaced = await WebContent.DownloadFileAsync(Url, path, Overwrite, httpClient: client,
            cancellationToken: Token);

        Assert.True(replaced.IsSuccess);
        Assert.Equal("new", await File.ReadAllTextAsync(path, Token));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, WebContent.ErrorCodes.NotFound)]
    [InlineData(HttpStatusCode.Gone, WebContent.ErrorCodes.NotFound)]
    [InlineData(HttpStatusCode.Forbidden, WebContent.ErrorCodes.HttpError)]
    [InlineData(HttpStatusCode.InternalServerError, WebContent.ErrorCodes.HttpError)]
    public async Task DownloadFileAsync_FailedStatus_LeavesTheExistingFile(HttpStatusCode status, string code)
    {
        string path = Path.Combine(this._dir, "data.bin");
        await File.WriteAllTextAsync(path, "old", Token);
        FakeHandler handler = new(_ => new HttpResponseMessage(status) { Content = new StringContent("nope") });

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url + "?key=secret", path, Overwrite,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(code, result.Error?.Code);
        Assert.Equal(status, Assert.IsType<HttpRequestException>(result.Error?.Exception).StatusCode);
        Assert.StartsWith($"{Url} returned {(int)status}", result.Error?.Message);
        Assert.DoesNotContain("secret", result.Error?.Message);
        Assert.Equal("old", await File.ReadAllTextAsync(path, Token));
        Assert.Single(Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task DownloadFileAsync_ConnectionBreaksOff_LeavesNoFile()
    {
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(100_000)) };
            response.Content.Headers.ContentLength = 1_000_000;
            return response;
        });
        string path = Path.Combine(this._dir, "data.bin");

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, path, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.Network, result.Error?.Code);
        Assert.Contains("broke off", result.Error?.Message);
        Assert.Empty(Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task DownloadFileAsync_ReportsProgressFromStartToEnd()
    {
        byte[] bytes = Bytes(300_000);
        List<DownloadProgress> reports = [];

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, Path.Combine(this._dir, "data.bin"),
            new DownloadOptions { Progress = new SyncProgress(reports) },
            httpClient: new HttpClient(new FakeHandler(_ => Response(bytes))),
            cancellationToken: Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DownloadProgress(0, bytes.Length), reports[0]);
        Assert.Equal(new DownloadProgress(bytes.Length, bytes.Length), reports[^1]);
        Assert.Equal(1, reports[^1].Fraction);
    }

    [Fact]
    public async Task DownloadFileAsync_UnknownSize_ReportsNullTotal()
    {
        List<DownloadProgress> reports = [];
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new NonSeekableStream(Bytes(10))),
        });

        await WebContent.DownloadFileAsync(Url, Path.Combine(this._dir, "data.bin"),
            new DownloadOptions { Progress = new SyncProgress(reports) },
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(new DownloadProgress(10, null), reports[^1]);
        Assert.Null(reports[^1].Fraction);
    }

    [Fact]
    public async Task DownloadFileAsync_PathIsAFolder_FailsWithFileSystem()
    {
        FakeHandler handler = new(_ => Response([1]));

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, this._dir, Overwrite,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.FileSystem, result.Error?.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DownloadFileAsync_NoConnectionOrTimeout_FailsWithNetwork()
    {
        HttpClient unreachable = new(new FakeHandler(_ => throw new HttpRequestException("No such host")));
        HttpClient slow = new(new FakeHandler(_ => throw new TaskCanceledException("Timed out")));

        Result<FileInfo> noConnection = await WebContent.DownloadFileAsync(Url, Path.Combine(this._dir, "a"),
            httpClient: unreachable, cancellationToken: Token);
        Result<FileInfo> timedOut = await WebContent.DownloadFileAsync(Url, Path.Combine(this._dir, "b"),
            httpClient: slow, cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.Network, noConnection.Error?.Code);
        Assert.Equal("Couldn't reach example.com: No such host", noConnection.Error?.Message);
        Assert.Equal(WebContent.ErrorCodes.Network, timedOut.Error?.Code);
        Assert.Equal("example.com didn't respond in time.", timedOut.Error?.Message);
    }

    [Fact]
    public async Task DownloadFileAsync_Cancelled_Throws()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebContent.DownloadFileAsync(Url,
            Path.Combine(this._dir, "data.bin"), httpClient: new HttpClient(new FakeHandler(_ => Response([1]))),
            cancellationToken: cts.Token));
        Assert.Empty(Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task ReadTextAsync_ReadsTheText()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<h1>Hi</h1>", Encoding.UTF8, "text/html"),
        });

        Result<string> result = await WebContent.ReadTextAsync(Url, httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal("<h1>Hi</h1>", result.Value);
    }

    [Fact]
    public async Task ReadTextAsync_FailedStatusOrNetwork_IsAFailure()
    {
        HttpClient missing = new(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        HttpClient unreachable = new(new FakeHandler(_ => throw new HttpRequestException("No such host")));

        Assert.Equal(WebContent.ErrorCodes.NotFound,
            (await WebContent.ReadTextAsync(Url, httpClient: missing, cancellationToken: Token)).Error?.Code);
        Assert.Equal(WebContent.ErrorCodes.Network,
            (await WebContent.ReadTextAsync(Url, httpClient: unreachable, cancellationToken: Token)).Error?.Code);
    }

    [Fact]
    public async Task ReadLinesAsync_StreamsTheLinesInTheResponsesCharset()
    {
        Encoding latin1 = Encoding.Latin1;
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response(latin1.GetBytes("café\r\nnaïve\nlast"));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "iso-8859-1" };
            return response;
        });

        List<string> lines = [];
        await foreach (string line in WebContent.ReadLinesAsync(Url, new HttpClient(handler), Token))
        {
            lines.Add(line);
        }

        Assert.Equal(["café", "naïve", "last"], lines);
    }

    [Fact]
    public async Task ReadLinesAsync_SendsNothingUntilEnumeratedAndClosesWhenStoppedEarly()
    {
        TrackingStream body = new(Encoding.UTF8.GetBytes("a\nb\nc\n"));
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(body) });

        IAsyncEnumerable<string> lines = WebContent.ReadLinesAsync(Url, new HttpClient(handler), Token);
        Assert.Empty(handler.Requests);

        await foreach (string line in lines)
        {
            Assert.Equal("a", line);
            break;
        }

        Assert.Single(handler.Requests);
        Assert.True(body.Disposed);
    }

    [Fact]
    public async Task ReadLinesAsync_FailedStatus_ThrowsWithTheStatusCode()
    {
        HttpClient client = new(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (string _ in WebContent.ReadLinesAsync(Url, client, Token))
            {
            }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
    }

    [Fact]
    public async Task ReadLinesAsync_Timeout_ThrowsHttpRequestException()
    {
        HttpClient client = new(new FakeHandler(_ => throw new TaskCanceledException("Timed out")));

        HttpRequestException ex = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (string _ in WebContent.ReadLinesAsync(Url, client, Token))
            {
            }
        });

        Assert.Equal("example.com didn't respond in time.", ex.Message);
    }

    [Fact]
    public async Task RelativeUrls_ResolveAgainstTheClientsBaseAddress()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        HttpClient client = new(handler) { BaseAddress = new Uri("https://api.example.com/v1/") };

        await WebContent.ReadTextAsync("items/1", httpClient: client, cancellationToken: Token);
        await WebContent.ReadTextAsync("/status", httpClient: client, cancellationToken: Token);

        Assert.Equal(new Uri("https://api.example.com/v1/items/1"), handler.Requests[0].RequestUri);
        Assert.Equal(new Uri("https://api.example.com/status"), handler.Requests[1].RequestUri);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("example.com")]
    [InlineData("/status")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/hosts")]
    public async Task BadUrls_ThrowRightAway(string url)
    {
        HttpClient client = new(new FakeHandler(_ => Response([])));

        Assert.ThrowsAny<ArgumentException>(() => WebContent.ReadLinesAsync(url, client, Token));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            WebContent.ReadTextAsync(url, httpClient: client, cancellationToken: Token));
    }

    [Fact]
    public async Task BlankPath_Throws()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => WebContent.DownloadFileAsync(Url, " ",
            httpClient: new HttpClient(new FakeHandler(_ => Response([]))), cancellationToken: Token));
    }

    [Fact]
    public async Task ClientsOwnUserAgent_IsKept()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        HttpClient client = new(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("MyApp/2.0");

        await WebContent.ReadTextAsync(Url, httpClient: client, cancellationToken: Token);

        Assert.Equal("MyApp/2.0", handler.Requests[0].Headers.UserAgent.ToString());
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(50, 200L, 0.25)]
    [InlineData(10, 0L, 1.0)]
    [InlineData(300, 200L, 1.0)]
    public void DownloadProgress_Fraction(long received, long? total, double? expected) =>
        Assert.Equal(expected, new DownloadProgress(received, total).Fraction);

    [Fact]
    public void DownloadFile_SendsSynchronously()
    {
        byte[] bytes = Bytes(100_000);
        FakeHandler handler = new(_ => Response(bytes));
        string path = Path.Combine(this._dir, "data.bin");

        Result<FileInfo> result = WebContent.DownloadFile(Url, path, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(1, handler.SyncSends);
    }

    [Fact]
    public void SyncReads_SendSynchronously()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"name":"a","count":2}""", Encoding.UTF8, "application/json"),
        });
        HttpClient client = new(handler);

        Assert.Equal("""{"name":"a","count":2}""", WebContent.ReadText(Url, httpClient: client, cancellationToken: Token).Value);
        Assert.Equal(22, WebContent.ReadBytes(Url, httpClient: client, cancellationToken: Token).Value!.Length);
        Assert.Equal(new Sample("a", 2), WebContent.ReadJson<Sample>(Url, httpClient: client, cancellationToken: Token).Value);
        Assert.Equal(new Sample("a", 2),
            WebContent.ReadJson(Url, SampleTypeInfo, httpClient: client, cancellationToken: Token).Value);
        Assert.Equal(4, handler.SyncSends);
    }

    [Fact]
    public void ReadLines_ReadsSynchronouslyAndOnlyWhenEnumerated()
    {
        FakeHandler handler = new(_ => Response(Encoding.UTF8.GetBytes("a\r\nb\nc")));

        IEnumerable<string> lines = WebContent.ReadLines(Url, new HttpClient(handler), Token);
        Assert.Empty(handler.Requests);

        Assert.Equal(["a", "b", "c"], lines.ToList());
        Assert.Equal(1, handler.SyncSends);
    }

    [Fact]
    public void SyncFailures_AreResultsLikeTheAsyncOnes()
    {
        HttpClient missing = new(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)));
        HttpClient unreachable = new(new FakeHandler(_ => throw new HttpRequestException("No such host")));

        Assert.Equal(WebContent.ErrorCodes.NotFound, WebContent.ReadText(Url, httpClient: missing, cancellationToken: Token).Error?.Code);
        Assert.Equal(WebContent.ErrorCodes.Network,
            WebContent.DownloadFile(Url, Path.Combine(this._dir, "a"), httpClient: unreachable, cancellationToken: Token).Error?.Code);
    }

    [Fact]
    public async Task MaxBytes_DeclaredLengthTooLarge_FailsBeforeReading()
    {
        TrackingStream body = new(Bytes(1000));
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StreamContent(body) };
            response.Content.Headers.ContentLength = 1000;
            return response;
        });
        string path = Path.Combine(this._dir, "data.bin");

        Result<string> text = await WebContent.ReadTextAsync(Url, 999, new HttpClient(handler), Token);
        Result<FileInfo> file = await WebContent.DownloadFileAsync(Url, path, new DownloadOptions { MaxBytes = 999 },
            new HttpClient(handler), Token);

        Assert.Equal(WebContent.ErrorCodes.TooLarge, text.Error?.Code);
        Assert.Equal(WebContent.ErrorCodes.TooLarge, file.Error?.Code);
        Assert.Contains("limit", text.Error?.Message);
        Assert.Equal(0, body.PositionWhenDisposed);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task MaxBytes_UndeclaredBodyRunsOver_FailsWhileReading()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new NonSeekableStream(Bytes(500_000))),
        });
        string path = Path.Combine(this._dir, "data.bin");

        Result<byte[]> bytes = await WebContent.ReadBytesAsync(Url, 100_000, new HttpClient(handler), Token);
        Result<FileInfo> file = await WebContent.DownloadFileAsync(Url, path, new DownloadOptions { MaxBytes = 100_000 },
            new HttpClient(handler), Token);

        Assert.Equal(WebContent.ErrorCodes.TooLarge, bytes.Error?.Code);
        Assert.Equal(WebContent.ErrorCodes.TooLarge, file.Error?.Code);
        Assert.Empty(Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task MaxBytes_ExactlyAtTheLimit_Succeeds()
    {
        FakeHandler handler = new(_ => Response(Bytes(1000)));

        Result<byte[]> bytes = await WebContent.ReadBytesAsync(Url, 1000, new HttpClient(handler), Token);

        Assert.Equal(1000, bytes.Value!.Length);
    }

    [Fact]
    public async Task MaxBytes_Negative_Throws()
    {
        HttpClient client = new(new FakeHandler(_ => Response([])));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => WebContent.ReadTextAsync(Url, -1, client, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => WebContent.DownloadFileAsync(Url,
            Path.Combine(this._dir, "a"), new DownloadOptions { MaxBytes = -1 }, client, Token));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Sha256_Matching_SavesTheFile(bool upperCase, bool prefixed)
    {
        byte[] bytes = Bytes(50_000);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        hash = upperCase ? hash.ToUpperInvariant() : hash;
        hash = prefixed ? "sha256:" + hash : hash;
        string path = Path.Combine(this._dir, "data.bin");

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, path, new DownloadOptions { Sha256 = hash },
            new HttpClient(new FakeHandler(_ => Response(bytes))), Token);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
    }

    [Fact]
    public async Task Sha256_Mismatch_SavesNothingAndKeepsTheOldFile()
    {
        string path = Path.Combine(this._dir, "data.bin");
        await File.WriteAllTextAsync(path, "old", Token);
        string wrong = Convert.ToHexStringLower(SHA256.HashData("something else"u8));

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, path,
            new DownloadOptions { Overwrite = true, Sha256 = wrong },
            new HttpClient(new FakeHandler(_ => Response(Bytes(1000)))), Token);

        Assert.Equal(WebContent.ErrorCodes.ChecksumMismatch, result.Error?.Code);
        Assert.Contains(wrong, result.Error?.Message);
        Assert.Equal("old", await File.ReadAllTextAsync(path, Token));
        Assert.Single(Directory.GetFiles(this._dir));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("sha1:0123456789abcdef0123456789abcdef01234567")]
    [InlineData("zz23456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public async Task Sha256_NotAHash_ThrowsBeforeSending(string hash)
    {
        FakeHandler handler = new(_ => Response([1]));

        await Assert.ThrowsAsync<ArgumentException>(() => WebContent.DownloadFileAsync(Url, Path.Combine(this._dir, "a"),
            new DownloadOptions { Sha256 = hash }, new HttpClient(handler), Token));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ReadJsonAsync_DeserializesWithNiddysOptionsByDefault()
    {
        FakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"name":"a","count":2}""", Encoding.UTF8, "application/json"),
        });

        Result<Sample> result = await WebContent.ReadJsonAsync<Sample>(Url, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal(new Sample("a", 2), result.Value);
    }

    [Fact]
    public async Task ReadJsonAsync_WithTypeInfo_Deserializes()
    {
        FakeHandler handler = new(_ => Response("""{"name":"b","count":3}"""u8.ToArray()));

        Result<Sample> result = await WebContent.ReadJsonAsync(Url, SampleTypeInfo, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal(new Sample("b", 3), result.Value);
    }

    [Fact]
    public async Task ReadJsonAsync_DecodesADeclaredCharset()
    {
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response(Encoding.Latin1.GetBytes("""{"name":"café","count":1}"""));
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "iso-8859-1" };
            return response;
        });

        Result<Sample> result = await WebContent.ReadJsonAsync<Sample>(Url, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal("café", result.Value!.Name);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"name":"a","count":"many"}""")]
    [InlineData("null")]
    public async Task ReadJsonAsync_InvalidOrNull_FailsWithInvalidContent(string json)
    {
        FakeHandler handler = new(_ => Response(Encoding.UTF8.GetBytes(json)));

        Result<Sample> result = await WebContent.ReadJsonAsync<Sample>(Url + "?token=secret",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(WebContent.ErrorCodes.InvalidContent, result.Error?.Code);
        Assert.DoesNotContain("secret", result.Error?.Message);
    }

    [Fact]
    public async Task ReadBytesAsync_ReturnsTheBody()
    {
        byte[] bytes = Bytes(70_000);

        Result<byte[]> result = await WebContent.ReadBytesAsync(Url, httpClient: new HttpClient(new FakeHandler(_ => Response(bytes))),
            cancellationToken: Token);

        Assert.Equal(bytes, result.Value);
    }

    [Fact]
    public async Task ReadTextAsync_ByteOrderMarkBeatsTheDeclaredCharset()
    {
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response([.. Encoding.UTF8.Preamble, .. Encoding.UTF8.GetBytes("café")]);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "iso-8859-1" };
            return response;
        });

        Result<string> result = await WebContent.ReadTextAsync(Url, httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal("café", result.Value);
    }

    [Fact]
    public async Task DownloadToFolderAsync_UsesTheContentDispositionName()
    {
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response([1, 2, 3]);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "\"report.pdf\"" };
            return response;
        });
        string folder = Path.Combine(this._dir, "downloads");

        Result<FileInfo> result = await WebContent.DownloadToFolderAsync(Url, folder, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal(Path.Combine(folder, "report.pdf"), result.Value!.FullName);
        Assert.Equal([1, 2, 3], await File.ReadAllBytesAsync(result.Value.FullName, Token));
    }

    [Fact]
    public async Task DownloadToFolderAsync_UsesTheFinalUrlAfterRedirects()
    {
        FakeHandler handler = new(request =>
        {
            HttpResponseMessage response = Response([1]);
            // What HttpClient reports after following a redirect.
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://cdn.example.com/blobs/app%20v2.zip?sig=x");
            return response;
        });

        Result<FileInfo> result = await WebContent.DownloadToFolderAsync("https://example.com/latest", this._dir,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal("app v2.zip", result.Value!.Name);
    }

    [Theory]
    [InlineData("../../evil.sh", "evil.sh")]
    [InlineData("..\\..\\evil.bat", "evil.bat")]
    [InlineData("/etc/passwd", "passwd")]
    [InlineData("..", "download")]
    public async Task DownloadToFolderAsync_CantWriteOutsideTheFolder(string serverName, string saved)
    {
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response([1]);
            response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = serverName };
            return response;
        });
        string folder = Path.Combine(this._dir, "downloads");

        Result<FileInfo> result = await WebContent.DownloadToFolderAsync(Url, folder, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal(Path.Combine(folder, saved), result.Value!.FullName);
    }

    [Fact]
    public async Task DownloadToFolderAsync_NoNameInTheUrl_UsesDownload()
    {
        Result<FileInfo> result = await WebContent.DownloadToFolderAsync("https://example.com/", this._dir,
            httpClient: new HttpClient(new FakeHandler(_ => Response([1]))), cancellationToken: Token);

        Assert.Equal("download", result.Value!.Name);
    }

    [Fact]
    public async Task DownloadToFolderAsync_ExistingFile_FailsUnlessOverwriting()
    {
        await File.WriteAllTextAsync(Path.Combine(this._dir, "data.bin"), "old", Token);
        HttpClient client = new(new FakeHandler(_ => Response([1])));

        Result<FileInfo> kept = await WebContent.DownloadToFolderAsync(Url, this._dir, httpClient: client, cancellationToken: Token);
        Result<FileInfo> replaced = await WebContent.DownloadToFolderAsync(Url, this._dir, Overwrite, client, Token);

        Assert.Equal(WebContent.ErrorCodes.FileExists, kept.Error?.Code);
        Assert.True(replaced.IsSuccess);
        Assert.Equal([1], await File.ReadAllBytesAsync(replaced.Value!.FullName, Token));
    }

    [Fact]
    public async Task Resume_AfterABreak_AsksForTheRestAndAppendsIt()
    {
        byte[] bytes = Bytes(300_000);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        int calls = 0;
        FakeHandler handler = new(request =>
        {
            calls++;
            if (calls == 1)
            {
                // The first attempt delivers 100,000 bytes and then drops.
                HttpResponseMessage broken = new(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new BrokenStream(bytes, 100_000)),
                };
                broken.Content.Headers.ContentLength = bytes.Length;
                broken.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return broken;
            }

            long from = request.Headers.Range!.Ranges.Single().From!.Value;
            HttpResponseMessage partial = new(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes[(int)from..]) };
            partial.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, bytes.Length - 1, bytes.Length);
            return partial;
        });
        HttpClient client = new(handler);
        string path = Path.Combine(this._dir, "data.bin");
        List<DownloadProgress> reports = [];
        DownloadOptions options = new() { Resume = true, Sha256 = hash, Progress = new SyncProgress(reports) };

        Result<FileInfo> first = await WebContent.DownloadFileAsync(Url, path, options, client, Token);

        Assert.Equal(WebContent.ErrorCodes.Network, first.Error?.Code);
        Assert.False(File.Exists(path));
        Assert.Equal(2, Directory.GetFiles(this._dir).Length);

        reports.Clear();
        Result<FileInfo> second = await WebContent.DownloadFileAsync(Url, path, options, client, Token);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
        RangeItemHeaderValue range = handler.Requests[1].Headers.Range!.Ranges.Single();
        Assert.Equal(100_000, range.From);
        Assert.Null(range.To);
        Assert.Equal("\"v1\"", handler.Requests[1].Headers.IfRange!.EntityTag!.Tag);
        Assert.Equal(new DownloadProgress(100_000, bytes.Length), reports[0]);
        Assert.Equal([path], Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task Resume_ServerSendsTheWholeFile_StartsOver()
    {
        byte[] bytes = Bytes(200_000);
        int calls = 0;
        FakeHandler handler = new(_ =>
        {
            calls++;
            HttpResponseMessage response = calls == 1
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(bytes, 50_000)) }
                : Response(bytes);
            response.Content.Headers.LastModified = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
            return response;
        });
        HttpClient client = new(handler);
        string path = Path.Combine(this._dir, "data.bin");
        DownloadOptions options = new() { Resume = true };

        await WebContent.DownloadFileAsync(Url, path, options, client, Token);
        Result<FileInfo> second = await WebContent.DownloadFileAsync(Url, path, options, client, Token);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), handler.Requests[1].Headers.IfRange!.Date);
        Assert.Equal([path], Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task Resume_RangeNotSatisfiable_StartsOver()
    {
        byte[] bytes = Bytes(80_000);
        int calls = 0;
        FakeHandler handler = new(request =>
        {
            calls++;
            if (calls == 1)
            {
                HttpResponseMessage broken = new(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(bytes, 40_000)) };
                broken.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return broken;
            }

            return request.Headers.Range is not null
                ? new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable)
                : Response(bytes);
        });
        HttpClient client = new(handler);
        string path = Path.Combine(this._dir, "data.bin");
        DownloadOptions options = new() { Resume = true };

        await WebContent.DownloadFileAsync(Url, path, options, client, Token);
        Result<FileInfo> second = await WebContent.DownloadFileAsync(Url, path, options, client, Token);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(3, handler.Requests.Count);
        Assert.Null(handler.Requests[2].Headers.Range);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
        Assert.Equal([path], Directory.GetFiles(this._dir));
    }

    [Fact]
    public async Task Resume_WithoutAValidator_DoesntResume()
    {
        byte[] bytes = Bytes(80_000);
        int calls = 0;
        FakeHandler handler = new(_ => ++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(bytes, 40_000)) }
            : Response(bytes));
        HttpClient client = new(handler);
        string path = Path.Combine(this._dir, "data.bin");

        await WebContent.DownloadFileAsync(Url, path, new DownloadOptions { Resume = true }, client, Token);
        Result<FileInfo> second = await WebContent.DownloadFileAsync(Url, path, new DownloadOptions { Resume = true }, client, Token);

        Assert.True(second.IsSuccess);
        Assert.Null(handler.Requests[1].Headers.Range);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path, Token));
    }

    [Fact]
    public async Task Resume_ChecksumMismatch_DeletesThePartialDownload()
    {
        byte[] bytes = Bytes(10_000);
        FakeHandler handler = new(_ =>
        {
            HttpResponseMessage response = Response(bytes);
            response.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
            return response;
        });
        DownloadOptions options = new() { Resume = true, Sha256 = new string('0', 64) };

        Result<FileInfo> result = await WebContent.DownloadFileAsync(Url, Path.Combine(this._dir, "data.bin"), options,
            new HttpClient(handler), Token);

        Assert.Equal(WebContent.ErrorCodes.ChecksumMismatch, result.Error?.Code);
        Assert.Empty(Directory.GetFiles(this._dir));
    }

    [Fact]
    public void Resume_WorksSynchronously()
    {
        byte[] bytes = Bytes(120_000);
        int calls = 0;
        FakeHandler handler = new(request =>
        {
            if (++calls == 1)
            {
                HttpResponseMessage broken = new(HttpStatusCode.OK) { Content = new StreamContent(new BrokenStream(bytes, 60_000)) };
                broken.Headers.ETag = new EntityTagHeaderValue("\"v1\"");
                return broken;
            }

            long from = request.Headers.Range!.Ranges.Single().From!.Value;
            HttpResponseMessage partial = new(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(bytes[(int)from..]) };
            partial.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, bytes.Length - 1, bytes.Length);
            return partial;
        });
        HttpClient client = new(handler);
        string path = Path.Combine(this._dir, "data.bin");
        DownloadOptions options = new() { Resume = true, Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)) };

        WebContent.DownloadFile(Url, path, options, client, Token);
        Result<FileInfo> second = WebContent.DownloadFile(Url, path, options, client, Token);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Equal(2, handler.SyncSends);
    }

    private static byte[] Bytes(int length)
    {
        byte[] bytes = new byte[length];
        new Random(42).NextBytes(bytes);
        return bytes;
    }

    private static JsonTypeInfo<Sample> SampleTypeInfo =>
        (JsonTypeInfo<Sample>)JsonSerializerOptions.Web.GetTypeInfo(typeof(Sample));

    private static HttpResponseMessage Response(byte[] bytes) => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

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

    // Reports synchronously, unlike Progress<T>, which posts to the thread pool.
    private sealed class SyncProgress(List<DownloadProgress> reports) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => reports.Add(value);
    }

    // Hands out the first bytes (zeros if none are given), then fails the way a dropped connection does.
    private sealed class BrokenStream(byte[]? bytes, int bytesBeforeBreaking) : Stream
    {
        private int _position;

        public BrokenStream(int bytesBeforeBreaking)
            : this(null, bytesBeforeBreaking)
        {
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => this.Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            int remaining = bytesBeforeBreaking - this._position;
            if (remaining == 0)
            {
                throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended early.");
            }

            int read = Math.Min(buffer.Length, remaining);
            if (bytes is null)
            {
                buffer[..read].Clear();
            }
            else
            {
                bytes.AsSpan(this._position, read).CopyTo(buffer);
            }

            this._position += read;
            return read;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(this.Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class TrackingStream(byte[] bytes) : NonSeekableStream(bytes)
    {
        public bool Disposed { get; private set; }

        public long PositionWhenDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            if (!this.Disposed)
            {
                this.PositionWhenDisposed = this.Position;
            }

            this.Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed record Sample(string Name, int Count);
}
