# WebContent

`Niddy.Core.Web` · Niddy.Core · [source](../../../src/Niddy.Core/Web/WebContent.cs)

Downloads files and reads pages over HTTP, in one call each. You can save a URL to a file (with progress, a size limit, a checksum and resuming), read it into memory as text, bytes or JSON, or stream its text line by line. Each method has a synchronous version. Failures you should expect, such as a 404, no network, a full disk or a bad checksum, come back as a [`Result`](../Results/Result.md) instead of an exception.

## API

| Member | Description |
|---|---|
| `static Task<Result<FileInfo>> DownloadFileAsync(string url, string path, DownloadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Saves `url` to `path` and returns the file. See [Downloading](#downloading). |
| `static Task<Result<FileInfo>> DownloadToFolderAsync(string url, string folder, DownloadOptions? options = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Saves `url` into `folder`, under the file name the server gives. See [Downloading to a folder](#downloading-to-a-folder). |
| `static Task<Result<string>> ReadTextAsync(string url, long? maxBytes = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Reads `url` as text: a page, a CSV file, release notes. See [Reading into memory](#reading-into-memory). |
| `static Task<Result<byte[]>> ReadBytesAsync(string url, long? maxBytes = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Reads `url` into a byte array: an image, another small binary file. |
| `static Task<Result<T>> ReadJsonAsync<T>(string url, JsonSerializerOptions? options = null, long? maxBytes = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Reads `url` and deserializes its JSON. Null `options` is [`JsonSerializerOptions.Niddy`](../Serialization/JsonExtensions.md). |
| `static Task<Result<T>> ReadJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, long? maxBytes = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | The same, with a source-generated `JsonTypeInfo<T>`, for trimmed and native AOT apps. |
| `static IAsyncEnumerable<string> ReadLinesAsync(string url, HttpClient? httpClient = null, CancellationToken cancellationToken = default)` | Streams `url`'s text line by line as it arrives, without holding it all in memory. See [Streaming lines](#streaming-lines). |
| `DownloadFile`, `DownloadToFolder`, `ReadText`, `ReadBytes`, `ReadJson<T>` (both overloads), `ReadLines` | Synchronous versions with the same parameters. `ReadLines` returns an `IEnumerable<string>`. See [Synchronous versions](#synchronous-versions). |

All of them send a `GET`.

- **`url`** is an `http` or `https` URL, or a URL relative to the client's `BaseAddress`. Anything else throws `ArgumentException` right away, including `file:` URLs, other schemes and bare host names.
- **`httpClient`** is the client to send with. Null uses a client shared across Niddy.Core. Pass your own for authorization or other headers, a proxy, a `BaseAddress` or a different `Timeout`.
- **User-Agent:** a request without one of its own sends `Niddy.Core/{version}`, because some servers refuse requests without one. A client with its own `DefaultRequestHeaders.UserAgent` keeps it.
- **`maxBytes`** (and `DownloadOptions.MaxBytes`) is the most the response may hold.
  - A larger `Content-Length` fails before anything is read.
  - A body that runs past the limit (the header can be missing or wrong) fails as soon as it does.
  - Either way the result is `TooLarge` and nothing is kept.
  - Null is no limit; a negative value throws `ArgumentOutOfRangeException`.
  - Set it for URLs you don't control.
- **Cancelling** `cancellationToken` throws `OperationCanceledException`. A timeout that you didn't cancel yourself is a `Network` failure.
- **Error messages** name the URL without its query string, because queries can hold keys or tokens.

| Platform | Notes |
|---|---|
| Desktop, iOS, Android | Work as they are. |
| Browser | Requests are subject to CORS: the server has to allow your app's origin. Downloads write to the WebAssembly virtual file system, not the user's Downloads folder. The synchronous versions aren't available. |

### Downloading

- **Atomic:** the download is written to a temporary file next to `path`, and replaces `path` only once it has fully arrived and matched `Sha256`, if one was given. A failed or cancelled download leaves no partial file (unless `Resume` is on), and an existing file stays as it was. See [`AtomicFile`](../IO/AtomicFile.md).
- **Folders:** missing folders in `path` are created.
- **Existing files:** without `Overwrite`, an existing file is a `FileExists` failure, checked before anything is sent.
- **Stalls:** the client's `Timeout` (100 seconds by default) only covers waiting for the response to start. A download that stalls partway runs until `cancellationToken` is cancelled, so pass a token with a timeout, or a Cancel button's token, for big files.

#### DownloadOptions

`sealed record DownloadOptions`: everything optional about a download. Null uses the defaults (all off).

| Property | Description |
|---|---|
| `bool Overwrite` | Replace a file that's already there. Without it, an existing file is a `FileExists` failure. |
| `IProgress<DownloadProgress>? Progress` | Told how far the download has got: once when it starts, about every 100 ms while it runs, and once at the end. A `Progress<T>` created on the UI thread reports on that thread. |
| `long? MaxBytes` | The most bytes to accept, as described above. |
| `string? Sha256` | The file's expected SHA-256 hash: 64 hex characters in either case, optionally after `sha256:` (the form GitHub's API uses). It's computed as the file downloads, and a mismatch is a `ChecksumMismatch` failure that saves nothing. Anything that isn't a SHA-256 hash throws `ArgumentException`. |
| `bool Resume` | Keep what arrived when a download fails partway, and carry on from there next time. See [Resuming](#resuming). |

#### Resuming

With `Resume`, a download that fails partway (a dropped connection, a cancel) keeps what it got. The next download of the same URL to the same folder asks only for the rest.

- **What's kept:** the partial data, in a hidden `.{id}.download` file in the target folder, named after the URL. Beside it is a `.validator` file holding the server's `ETag` or `Last-Modified`.
- **The next request** is an HTTP `Range` request with `If-Range`, so the server sends the rest only if the file hasn't changed.
- **When it starts over:** the server doesn't support ranges, sent neither `ETag` nor `Last-Modified`, or the file has changed since.
- **Clean-up:** both files are removed once the download finishes. They're also removed after a `Sha256` mismatch or a `MaxBytes` failure, where the partial data can't be trusted.
- **The finished file** still only appears once the whole download has arrived.

It's worth it for large files. For small ones, it's simpler to retry from the start.

#### DownloadProgress

`readonly record struct DownloadProgress(long BytesReceived, long? TotalBytes)`: how far a download has got.

| Member | Description |
|---|---|
| `long BytesReceived` | The bytes written so far, including any resumed from an earlier attempt. |
| `long? TotalBytes` | The full size, from the `Content-Length` header, or null if the server didn't send one. |
| `double? Fraction` | `BytesReceived / TotalBytes`, from 0 to 1, or null when the size isn't known (show an indeterminate progress bar). |

### Downloading to a folder

`DownloadToFolderAsync` works like `DownloadFileAsync`, but takes the file name from the server.

- **Where the name comes from:** the response's `Content-Disposition` header. Without one, the last segment of the URL (after any redirects). Without that, `download`.
- **Safety:** folders in the name are dropped, and characters that aren't allowed in file names are replaced. A server can't make it write outside `folder`.
- **Existing files:** the name is only known once the response starts, so an existing file without `Overwrite` fails after the request is sent, though before anything is read.

### Reading into memory

`ReadTextAsync`, `ReadBytesAsync` and `ReadJsonAsync<T>` hold the whole response in memory. Use `maxBytes` to bound it, or [`ReadLinesAsync`](#streaming-lines) or a download for large content.

- **Text** is decoded by its byte order mark, if it has one. Otherwise it uses the charset in the response's `Content-Type`, and otherwise UTF-8.
- **JSON** is read as UTF-8 unless the `Content-Type` names another charset. Content that isn't valid JSON for `T`, or is JSON `null`, is an `InvalidContent` failure.

### Streaming lines

```csharp
await foreach (string line in WebContent.ReadLinesAsync(url, cancellationToken: ct))
{
    …
}
```

- **Nothing is sent until enumeration starts.** Enumerating twice sends two requests.
- **Line breaks:** lines are split on `\n`, `\r` or `\r\n`, with the break removed. Text is decoded like `ReadTextAsync`.
- **Stopping early:** `break` or `return` closes the connection, so you can stop once you've found what you need. `.WithCancellation(ct)` works as well as the parameter.
- **Errors throw**, unlike the other methods, because a stream can fail after it has already handed out lines.
  - Before the first line, it throws `HttpRequestException`. The cause is a status that isn't a success (`StatusCode` says which), no connection, or a timeout.
  - Partway through, it throws `IOException`: the connection broke off.

### Synchronous versions

`DownloadFile`, `DownloadToFolder`, `ReadText`, `ReadBytes`, `ReadJson<T>` and `ReadLines` take the same parameters as the async methods and behave the same. They're for console tools, scripts and startup code.

- **Real synchronous calls.** They use `HttpClient.Send`, not the async methods blocked on, so they can't deadlock. They do block the calling thread, so keep them off the UI thread.
- **Not in the browser.** They're marked `[UnsupportedOSPlatform("browser")]`.
- **Your own client needs a handler that supports synchronous sends.** `SocketsHttpHandler` does, and it's the default on desktop. The native Android and iOS handlers don't, and they throw `NotSupportedException`.
- **Without a client**, they use a shared `SocketsHttpHandler` client, so they work on mobile too.

### ErrorCodes

`WebContent.ErrorCodes` holds the `Error.Code` values the methods that return a `Result` fail with.

| Code | When |
|---|---|
| `NotFound` | 404 Not Found or 410 Gone. |
| `HttpError` | Any other status that isn't a success. `Error.Exception` is an `HttpRequestException` whose `StatusCode` says which; `NotFound` failures carry one too. |
| `Network` | No response, or the connection broke off: no network, a DNS failure, a timeout, a dropped download. |
| `TooLarge` | The response was bigger than `maxBytes` or `DownloadOptions.MaxBytes`. Nothing was saved. |
| `ChecksumMismatch` | The download didn't match `DownloadOptions.Sha256`. Nothing was saved. |
| `InvalidContent` | `ReadJsonAsync` got something that isn't valid JSON for the type, or JSON `null`. |
| `FileExists` | Downloads only: the file exists and `Overwrite` is false. |
| `FileSystem` | Downloads only: the file couldn't be written (no permission, a full disk, the path is a folder). |

## Examples

```csharp
using Niddy.Core.Results;
using Niddy.Core.Web;

// A large download with a progress bar, a Cancel button, a checksum and resuming
Progress<DownloadProgress> progress = new(p => bar.Value = (p.Fraction ?? 0) * 100);
Result<FileInfo> saved = await WebContent.DownloadFileAsync(
    "https://example.com/releases/app-2.0.zip", Path.Combine(paths.Cache, "app-2.0.zip"),
    new DownloadOptions
    {
        Overwrite = true,
        Progress = progress,
        Sha256 = "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08",
        Resume = true,
    },
    cancellationToken: cancel.Token);
if (saved.IsFailure)
{
    Toast.Show(this, saved.Error!.Message, ToastType.Error);
}

// Whatever the server calls it, into the Downloads folder
Result<FileInfo> report = await WebContent.DownloadToFolderAsync("https://example.com/reports/latest", downloads);

// A small text file, with a size limit
Result<string> notes = await WebContent.ReadTextAsync("https://example.com/releases/notes.md", maxBytes: 1_000_000);
string text = notes.GetValueOrDefault("No release notes.");

// JSON, with a source-generated context in an AOT app
Result<UpdateManifest> manifest = await WebContent.ReadJsonAsync(
    "https://example.com/manifest.json", AppJsonContext.Default.UpdateManifest);

// The first matching line of a large file, without downloading the rest
string? match = null;
await foreach (string line in WebContent.ReadLinesAsync("https://example.com/data/big.csv"))
{
    if (line.StartsWith("2.0,", StringComparison.Ordinal))
    {
        match = line;
        break;
    }
}

// An API that needs a token: relative URLs resolve against BaseAddress
HttpClient api = new() { BaseAddress = new Uri("https://api.example.com/v1/") };
api.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
Result<Item[]> items = await WebContent.ReadJsonAsync<Item[]>("items?page=2", httpClient: api);

// A console tool, synchronously
Result<FileInfo> data = WebContent.DownloadFile("https://example.com/data.csv", "data.csv",
    new DownloadOptions { Overwrite = true });
```

To retry network failures with [`Retry`](../Resilience/Retry.md), read `.Value` inside the retried call: on a failure it throws a `ResultException` carrying the `Error`. With `Resume` on, each retry continues where the last one stopped.

```csharp
using Niddy.Core.Resilience;

FileInfo file = await Retry.ExecuteAsync(
    async () => (await WebContent.DownloadFileAsync(url, path, new DownloadOptions { Overwrite = true, Resume = true })).Value,
    new RetryOptions
    {
        MaxAttempts = 3,
        ShouldRetry = ex => ex is ResultException { Error.Code: WebContent.ErrorCodes.Network },
    });
```

## See also

- [GitHubReleases](../GitHub/GitHubReleases.md): download a release asset, checked against GitHub's checksums
- [UrlLauncher](UrlLauncher.md): open a URL in the browser instead
- [AtomicFile](../IO/AtomicFile.md)
- [Archive](../IO/Archives/Archive.md), to extract a downloaded zip safely
- [Result](../Results/Result.md)
