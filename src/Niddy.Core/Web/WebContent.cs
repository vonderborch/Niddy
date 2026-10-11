using System.Buffers;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Niddy.Core.IO;
using Niddy.Core.Results;
using Niddy.Core.Serialization;
using Niddy.Core.Text;

namespace Niddy.Core.Web;

/// <summary>
/// Downloads files and reads pages over HTTP: <see cref="DownloadFileAsync"/> saves a URL to a file,
/// <see cref="DownloadToFolderAsync"/> saves it under the server's file name, <see cref="ReadTextAsync"/>,
/// <see cref="ReadBytesAsync"/> and <see cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)"/>
/// read it into memory, and <see cref="ReadLinesAsync"/> streams its text line by line. Each has a synchronous version.
/// </summary>
/// <remarks>
/// <para>
/// They send a <c>GET</c> with the given client, or a shared one. Pass your own <see cref="HttpClient"/> for headers
/// (authorization, a User-Agent of your own), a proxy, or a <see cref="HttpClient.BaseAddress"/> that relative URLs
/// resolve against. Without a User-Agent of its own, a request sends <c>Niddy.Core/{version}</c>, since some servers
/// refuse requests without one. In the browser, requests are subject to CORS.
/// </para>
/// <para>
/// The synchronous versions are real synchronous calls (<see cref="HttpClient.Send(HttpRequestMessage)"/>), not the
/// async ones blocked on, so they can't deadlock. They're for console tools, scripts and startup code; they block the
/// calling thread, so keep them off the UI thread. They don't work in the browser, and a client you pass needs a
/// handler that supports synchronous sends: <see cref="SocketsHttpHandler"/> does (it's the default on desktop), the
/// native Android and iOS handlers don't. Without a client they use a shared <see cref="SocketsHttpHandler"/> client,
/// so they work on mobile too.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Result&lt;FileInfo&gt; saved = await WebContent.DownloadFileAsync("https://example.com/data.zip", path);
/// Result&lt;string&gt; page = await WebContent.ReadTextAsync("https://example.com/");
/// </code>
/// </example>
public static class WebContent
{
    /// <summary>The <see cref="Error.Code"/> values the methods that return a <see cref="Result{T}"/> fail with.</summary>
    public static class ErrorCodes
    {
        /// <summary>The server answered 404 Not Found or 410 Gone.</summary>
        public const string NotFound = "NotFound";

        /// <summary>
        /// The server answered with another status that isn't a success. <see cref="Error.Exception"/> is an
        /// <see cref="HttpRequestException"/> whose <see cref="HttpRequestException.StatusCode"/> says which.
        /// </summary>
        public const string HttpError = "HttpError";

        /// <summary>No response, or the connection broke off: no network, a DNS failure, a timeout, a dropped download.</summary>
        public const string Network = "Network";

        /// <summary>The response was bigger than the <c>maxBytes</c> or <see cref="DownloadOptions.MaxBytes"/> limit. Nothing was saved.</summary>
        public const string TooLarge = "TooLarge";

        /// <summary>The download didn't match <see cref="DownloadOptions.Sha256"/>. Nothing was saved.</summary>
        public const string ChecksumMismatch = "ChecksumMismatch";

        /// <summary>
        /// <see cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)"/> got
        /// something that isn't valid JSON for the type, or JSON <c>null</c>.
        /// </summary>
        public const string InvalidContent = "InvalidContent";

        /// <summary>The file was already there and <see cref="DownloadOptions.Overwrite"/> was false. Nothing was saved.</summary>
        public const string FileExists = "FileExists";

        /// <summary>The file couldn't be written: no permission, a full disk, the path is a folder, and so on.</summary>
        public const string FileSystem = "FileSystem";
    }

    private const int BufferSize = 81_920;

    private const long ProgressIntervalMs = 100;

    // The most memory a read reserves up front from the response's Content-Length, which the server controls.
    private const int MaxInitialCapacity = 16 * 1024 * 1024;

    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="path"/>. The file is written atomically: it appears only once
    /// the whole download has arrived (and matched <see cref="DownloadOptions.Sha256"/>, if set), so a failed or
    /// cancelled download leaves no partial file and any existing file untouched.
    /// </summary>
    /// <param name="url">An <c>http</c> or <c>https</c> URL, or one relative to the client's <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="path">The file to save to. Missing folders are created.</param>
    /// <param name="options">Overwriting, progress, a size limit, a checksum, resuming. Null uses the defaults.</param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">
    /// Cancels the download. The client's <see cref="HttpClient.Timeout"/> only covers waiting for the response to
    /// start, so a download that stalls partway runs until this is cancelled.
    /// </param>
    /// <returns>The saved file, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> isn't an <c>http</c> or <c>https</c> URL, <paramref name="path"/> is blank, or
    /// <see cref="DownloadOptions.Sha256"/> isn't a SHA-256 hash.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="DownloadOptions.MaxBytes"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<FileInfo>> DownloadFileAsync(string url, string path, DownloadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        DownloadCoreAsync(url, path, null, options, httpClient, null, false, cancellationToken).AsTask();

    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="path"/>, blocking until it's done. Otherwise the same as
    /// <see cref="DownloadFileAsync"/>; see <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="DownloadFileAsync" path="/param"/>
    /// <inheritdoc cref="DownloadFileAsync" path="/returns"/>
    /// <inheritdoc cref="DownloadFileAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<FileInfo> DownloadFile(string url, string path, DownloadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(DownloadCoreAsync(url, path, null, options, httpClient, null, true, cancellationToken));

    /// <summary>
    /// Downloads <paramref name="url"/> into <paramref name="folder"/>, under the file name the server gives. Otherwise
    /// the same as <see cref="DownloadFileAsync"/>.
    /// </summary>
    /// <remarks>
    /// The name comes from the response's <c>Content-Disposition</c> header, or else the last segment of the URL (after
    /// any redirects), or else <c>download</c>. Folders in it are dropped and characters that aren't allowed in file
    /// names are replaced, so a server can't make it write outside <paramref name="folder"/>. Since the name is only
    /// known once the response starts, an existing file without <see cref="DownloadOptions.Overwrite"/> fails after the
    /// request is sent, though before anything is read.
    /// </remarks>
    /// <param name="url">An <c>http</c> or <c>https</c> URL, or one relative to the client's <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="folder">The folder to save into. It's created if it's missing.</param>
    /// <param name="options">Overwriting, progress, a size limit, a checksum, resuming. Null uses the defaults.</param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">Cancels the download, as for <see cref="DownloadFileAsync"/>.</param>
    /// <returns>The saved file, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> isn't an <c>http</c> or <c>https</c> URL, <paramref name="folder"/> is blank, or
    /// <see cref="DownloadOptions.Sha256"/> isn't a SHA-256 hash.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"><see cref="DownloadOptions.MaxBytes"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<FileInfo>> DownloadToFolderAsync(string url, string folder, DownloadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        DownloadCoreAsync(url, null, folder, options, httpClient, null, false, cancellationToken).AsTask();

    /// <summary>
    /// Downloads <paramref name="url"/> into <paramref name="folder"/>, blocking until it's done. Otherwise the same as
    /// <see cref="DownloadToFolderAsync"/>; see <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="DownloadToFolderAsync" path="/param"/>
    /// <inheritdoc cref="DownloadToFolderAsync" path="/returns"/>
    /// <inheritdoc cref="DownloadToFolderAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<FileInfo> DownloadToFolder(string url, string folder, DownloadOptions? options = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(DownloadCoreAsync(url, null, folder, options, httpClient, null, true, cancellationToken));

    /// <summary>
    /// Reads <paramref name="url"/> as text, e.g. a web page, a CSV file, or a plain-text release note.
    /// </summary>
    /// <remarks>
    /// The text is decoded by its byte order mark, or else the charset in the response's <c>Content-Type</c>, or else
    /// as UTF-8. The whole response is held in memory; for large text, use <see cref="ReadLinesAsync"/>.
    /// </remarks>
    /// <param name="url">An <c>http</c> or <c>https</c> URL, or one relative to the client's <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="maxBytes">
    /// The most bytes to accept; a bigger response is a <see cref="ErrorCodes.TooLarge"/> failure, caught before or
    /// while reading. Null is no limit.
    /// </param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The text, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="url"/> isn't an <c>http</c> or <c>https</c> URL.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<string>> ReadTextAsync(string url, long? maxBytes = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ReadTextCoreAsync(url, maxBytes, httpClient, null, false, cancellationToken).AsTask();

    /// <summary>
    /// Reads <paramref name="url"/> as text, blocking until it's done. Otherwise the same as
    /// <see cref="ReadTextAsync"/>; see <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ReadTextAsync" path="/param"/>
    /// <inheritdoc cref="ReadTextAsync" path="/returns"/>
    /// <inheritdoc cref="ReadTextAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<string> ReadText(string url, long? maxBytes = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ReadTextCoreAsync(url, maxBytes, httpClient, null, true, cancellationToken));

    /// <summary>Reads <paramref name="url"/> into a byte array, e.g. an image or another small binary file.</summary>
    /// <remarks>The whole response is held in memory; to save a large file, use <see cref="DownloadFileAsync"/>.</remarks>
    /// <inheritdoc cref="ReadTextAsync" path="/param"/>
    /// <returns>The bytes, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <inheritdoc cref="ReadTextAsync" path="/exception"/>
    public static Task<Result<byte[]>> ReadBytesAsync(string url, long? maxBytes = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ReadCoreAsync(url, maxBytes, httpClient, null, ToBytes, false, cancellationToken).AsTask();

    /// <summary>
    /// Reads <paramref name="url"/> into a byte array, blocking until it's done. Otherwise the same as
    /// <see cref="ReadBytesAsync"/>; see <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ReadBytesAsync" path="/param"/>
    /// <inheritdoc cref="ReadBytesAsync" path="/returns"/>
    /// <inheritdoc cref="ReadBytesAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<byte[]> ReadBytes(string url, long? maxBytes = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ReadCoreAsync(url, maxBytes, httpClient, null, ToBytes, true, cancellationToken));

    /// <summary>Reads <paramref name="url"/> and deserializes its JSON as <typeparamref name="T"/>.</summary>
    /// <remarks>
    /// JSON is read as UTF-8 unless the response's <c>Content-Type</c> names another charset. For trimmed or
    /// native AOT apps, use the overload that takes a <see cref="JsonTypeInfo{T}"/> from a source-generated context.
    /// </remarks>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="url">An <c>http</c> or <c>https</c> URL, or one relative to the client's <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="options">The serializer options. Null is <c>JsonSerializerOptions.Niddy</c>.</param>
    /// <param name="maxBytes">
    /// The most bytes to accept; a bigger response is a <see cref="ErrorCodes.TooLarge"/> failure, caught before or
    /// while reading. Null is no limit.
    /// </param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The value, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: JSON that doesn't
    /// fit <typeparamref name="T"/>, or is <c>null</c>, is <see cref="ErrorCodes.InvalidContent"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="url"/> isn't an <c>http</c> or <c>https</c> URL.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxBytes"/> is negative.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<T>> ReadJsonAsync<T>(string url, JsonSerializerOptions? options = null,
        long? maxBytes = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ReadCoreAsync(url, maxBytes, httpClient, null, JsonConverter<T>(options), false, cancellationToken).AsTask();

    /// <summary>
    /// Reads <paramref name="url"/> and deserializes its JSON with <paramref name="typeInfo"/>, from a
    /// source-generated <c>JsonSerializerContext</c>, which works in trimmed and native AOT apps.
    /// </summary>
    /// <typeparam name="T">The type to deserialize.</typeparam>
    /// <param name="url">An <c>http</c> or <c>https</c> URL, or one relative to the client's <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="typeInfo">The type's metadata, e.g. <c>AppJsonContext.Default.Settings</c>.</param>
    /// <param name="maxBytes">
    /// The most bytes to accept; a bigger response is a <see cref="ErrorCodes.TooLarge"/> failure. Null is no limit.
    /// </param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/exception"/>
    public static Task<Result<T>> ReadJsonAsync<T>(string url, JsonTypeInfo<T> typeInfo, long? maxBytes = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ReadCoreAsync(url, maxBytes, httpClient, null, JsonConverter(typeInfo), false, cancellationToken).AsTask();

    /// <summary>
    /// Reads <paramref name="url"/> as JSON, blocking until it's done. Otherwise the same as
    /// <see cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)"/>; see
    /// <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/typeparam"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/param"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<T> ReadJson<T>(string url, JsonSerializerOptions? options = null, long? maxBytes = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ReadCoreAsync(url, maxBytes, httpClient, null, JsonConverter<T>(options), true, cancellationToken));

    /// <summary>
    /// Reads <paramref name="url"/> as JSON with <paramref name="typeInfo"/>, blocking until it's done. Otherwise the
    /// same as <see cref="ReadJsonAsync{T}(string, JsonTypeInfo{T}, long?, HttpClient?, CancellationToken)"/>; see
    /// <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonTypeInfo{T}, long?, HttpClient?, CancellationToken)" path="/typeparam"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonTypeInfo{T}, long?, HttpClient?, CancellationToken)" path="/param"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="ReadJsonAsync{T}(string, JsonSerializerOptions?, long?, HttpClient?, CancellationToken)" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<T> ReadJson<T>(string url, JsonTypeInfo<T> typeInfo, long? maxBytes = null,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ReadCoreAsync(url, maxBytes, httpClient, null, JsonConverter(typeInfo), true, cancellationToken));

    /// <summary>
    /// Streams <paramref name="url"/>'s text line by line as it arrives, without holding it all in memory, e.g. for a
    /// large log or CSV file, or to stop reading once you've found what you need.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nothing is sent until enumeration starts. Lines are split on <c>\n</c>, <c>\r</c> or <c>\r\n</c>, without the
    /// line break, and decoded like <see cref="ReadTextAsync"/>. Stopping early (e.g. <c>break</c>) closes the connection.
    /// </para>
    /// <para>
    /// Unlike the other methods it throws rather than returning a <see cref="Result{T}"/>, because it can fail after
    /// it has already handed out lines.
    /// </para>
    /// </remarks>
    /// <param name="url">An <c>http</c> or <c>https</c> URL, or one relative to the client's <see cref="HttpClient.BaseAddress"/>.</param>
    /// <param name="httpClient">The client to send with. Null uses a shared client.</param>
    /// <param name="cancellationToken">Cancels the request and the reading. <c>WithCancellation</c> works too.</param>
    /// <returns>The lines, read as they arrive.</returns>
    /// <exception cref="ArgumentException"><paramref name="url"/> isn't an <c>http</c> or <c>https</c> URL. Thrown right away.</exception>
    /// <exception cref="HttpRequestException">
    /// Thrown by the enumeration before the first line: the server answered with a status that isn't a success
    /// (<see cref="HttpRequestException.StatusCode"/> says which), or couldn't be reached or didn't respond in time.
    /// </exception>
    /// <exception cref="IOException">Thrown by the enumeration if the connection breaks off partway.</exception>
    /// <exception cref="OperationCanceledException">The enumeration was canceled.</exception>
    public static IAsyncEnumerable<string> ReadLinesAsync(string url, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = SharedHttp.ClientFor(httpClient, false);
        return ReadLinesCoreAsync(client, ResolveUrl(url, client), cancellationToken);
    }

    /// <summary>
    /// Streams <paramref name="url"/>'s text line by line as it arrives, reading synchronously. Otherwise the same as
    /// <see cref="ReadLinesAsync"/>; see <see cref="WebContent"/> for where the synchronous versions work.
    /// </summary>
    /// <remarks>
    /// Nothing is sent until enumeration starts, and stopping early (e.g. <c>break</c>, or LINQ's <c>First</c>) closes
    /// the connection. It throws rather than returning a <see cref="Result{T}"/>, like <see cref="ReadLinesAsync"/>.
    /// </remarks>
    /// <inheritdoc cref="ReadLinesAsync" path="/param"/>
    /// <inheritdoc cref="ReadLinesAsync" path="/returns"/>
    /// <inheritdoc cref="ReadLinesAsync" path="/exception"/>
    /// <exception cref="NotSupportedException">
    /// Thrown by the enumeration: <paramref name="httpClient"/>'s handler doesn't support synchronous sends.
    /// </exception>
    [UnsupportedOSPlatform("browser")]
    public static IEnumerable<string> ReadLines(string url, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        HttpClient client = SharedHttp.ClientFor(httpClient, true);
        return ReadLinesCore(client, ResolveUrl(url, client), cancellationToken);
    }

    internal static ValueTask<Result<string>> ReadTextCoreAsync(string url, long? maxBytes, HttpClient? httpClient,
        Action<HttpRequestMessage>? configure, bool sync, CancellationToken cancellationToken) =>
        ReadCoreAsync(url, maxBytes, httpClient, configure, ToText, sync, cancellationToken);

    /// <summary>
    /// The download behind every download method. <paramref name="path"/> or <paramref name="folder"/> says where it
    /// goes; <paramref name="configure"/> adds headers (GitHub's API needs some); <paramref name="sync"/> uses only
    /// synchronous calls, so the returned task has completed.
    /// </summary>
    internal static async ValueTask<Result<FileInfo>> DownloadCoreAsync(string url, string? path, string? folder,
        DownloadOptions? options, HttpClient? httpClient, Action<HttpRequestMessage>? configure, bool sync,
        CancellationToken cancellationToken)
    {
        options ??= DownloadOptions.Default;
        HttpClient client = SharedHttp.ClientFor(httpClient, sync);
        Uri address = ResolveUrl(url, client);
        byte[]? expectedHash = ParseSha256(options.Sha256, nameof(options));
        ValidateMaxBytes(options.MaxBytes, nameof(options));

        string directory;
        string? fullPath = null;
        if (path is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);
            fullPath = Path.GetFullPath(path);
            if (CheckTarget(fullPath, options.Overwrite) is { } error)
            {
                return error;
            }

            directory = Path.GetDirectoryName(fullPath)!;
        }
        else
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(folder);
            directory = Path.GetFullPath(folder);
            if (File.Exists(directory))
            {
                return new Error($"Couldn't save to {directory}: it's a file, not a folder.", ErrorCodes.FileSystem);
            }
        }

        PartialDownload? partial = options.Resume ? PartialDownload.For(directory, address) : null;
        try
        {
            while (true)
            {
                (long offset, RangeConditionHeaderValue? condition) = partial?.Load() ?? (0, null);
                using HttpRequestMessage request = CreateRequest(address, client, configure);
                if (offset > 0)
                {
                    request.Headers.Range = new RangeHeaderValue(offset, null);
                    request.Headers.IfRange = condition;
                }

                using HttpResponseMessage response = await SharedHttp
                    .SendAsync(client, request, HttpCompletionOption.ResponseHeadersRead, sync, cancellationToken)
                    .ConfigureAwait(false);
                bool resumed = offset > 0
                    && response.StatusCode == HttpStatusCode.PartialContent
                    && response.Content.Headers.ContentRange?.From == offset;
                if (offset > 0 && !resumed
                    && response.StatusCode is HttpStatusCode.PartialContent or HttpStatusCode.RequestedRangeNotSatisfiable)
                {
                    // What was kept doesn't fit what the server has now: start over.
                    partial!.Delete();
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return ToError(response, address);
                }

                long start = resumed ? offset : 0;
                long? total = resumed
                    ? response.Content.Headers.ContentRange!.Length ?? offset + response.Content.Headers.ContentLength
                    : response.Content.Headers.ContentLength;
                if (options.MaxBytes is long max && total > max)
                {
                    partial?.Delete();
                    return TooLarge(address, max);
                }

                string target = fullPath ?? Path.Combine(directory, FileNameFrom(response, address));
                if (fullPath is null && CheckTarget(target, options.Overwrite) is { } targetError)
                {
                    return targetError;
                }

                using IncrementalHash? hash = expectedHash is null ? null : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                Body body = new(response, address, start, total, options, hash, expectedHash);
                if (partial is null)
                {
                    if (sync)
                    {
                        AtomicFile.Write(target, file => SharedHttp.RunSync(body.SaveAsync(file, true, cancellationToken)));
                    }
                    else
                    {
                        await AtomicFile.WriteAsync(target, (file, ct) => body.SaveAsync(file, false, ct).AsTask(),
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    await partial.SaveAsync(body, resumed, sync, cancellationToken).ConfigureAwait(false);
                    AtomicFile.Replace(partial.Path, target);
                    partial.DeleteValidator();
                }

                return new FileInfo(target);
            }
        }
        catch (ContentTooLargeException ex)
        {
            partial?.Delete();
            return TooLarge(address, ex.MaxBytes);
        }
        catch (ChecksumMismatchException ex)
        {
            partial?.Delete();
            return new Error(ex.Message, ErrorCodes.ChecksumMismatch);
        }
        catch (Exception ex) when (ToError(ex, address, fullPath ?? directory, cancellationToken) is { } error)
        {
            return error;
        }
    }

    private static async ValueTask<Result<T>> ReadCoreAsync<T>(string url, long? maxBytes, HttpClient? httpClient,
        Action<HttpRequestMessage>? configure, Func<byte[], HttpContent, Uri, Result<T>> convert, bool sync,
        CancellationToken cancellationToken)
    {
        HttpClient client = SharedHttp.ClientFor(httpClient, sync);
        Uri address = ResolveUrl(url, client);
        ValidateMaxBytes(maxBytes, nameof(maxBytes));

        try
        {
            using HttpRequestMessage request = CreateRequest(address, client, configure);
            using HttpResponseMessage response = await SharedHttp
                .SendAsync(client, request, HttpCompletionOption.ResponseHeadersRead, sync, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return ToError(response, address);
            }

            long? length = response.Content.Headers.ContentLength;
            if (maxBytes is long max && length > max)
            {
                return TooLarge(address, max);
            }

            using MemoryStream buffer = new((int)Math.Min(length ?? 0, MaxInitialCapacity));
            using (Stream body = await SharedHttp.ReadStreamAsync(response.Content, sync, cancellationToken).ConfigureAwait(false))
            {
                await CopyAsync(body, buffer, 0, length, maxBytes, null, null, sync, cancellationToken).ConfigureAwait(false);
            }

            return convert(buffer.ToArray(), response.Content, address);
        }
        catch (ContentTooLargeException ex)
        {
            return TooLarge(address, ex.MaxBytes);
        }
        catch (Exception ex) when (ToError(ex, address, null, cancellationToken) is { } error)
        {
            return error;
        }
    }

    private static async IAsyncEnumerable<string> ReadLinesCoreAsync(HttpClient client, Uri address,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(address, client, null);
        using HttpResponseMessage response = await OpenAsync(client, request, false, cancellationToken).ConfigureAwait(false);
        await using Stream body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using StreamReader reader = new(body, SharedHttp.GetEncoding(response.Content), detectEncodingFromByteOrderMarks: true);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<string> ReadLinesCore(HttpClient client, Uri address, CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = CreateRequest(address, client, null);
        using HttpResponseMessage response = SharedHttp.RunSync(OpenAsync(client, request, true, cancellationToken));
        using Stream body = response.Content.ReadAsStream(cancellationToken);
        using StreamReader reader = new(body, SharedHttp.GetEncoding(response.Content), detectEncodingFromByteOrderMarks: true);
        while (reader.ReadLine() is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return line;
        }
    }

    // Sends for ReadLines(Async), turning every failure to get a response into an HttpRequestException.
    private static async ValueTask<HttpResponseMessage> OpenAsync(HttpClient client, HttpRequestMessage request,
        bool sync, CancellationToken cancellationToken)
    {
        Uri address = request.RequestUri!;
        HttpResponseMessage response;
        try
        {
            response = await SharedHttp
                .SendAsync(client, request, HttpCompletionOption.ResponseHeadersRead, sync, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException($"{address.Host} didn't respond in time.", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                throw (HttpRequestException)ToError(response, address).Exception!;
            }
        }

        return response;
    }

    internal static Uri ResolveUrl(string url, HttpClient client)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        // An absolute check comes first: on Unix, Uri also reads "/path" as an absolute file: URL.
        if ((Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) && IsHttp(uri))
            || (client.BaseAddress is not null && Uri.TryCreate(client.BaseAddress, url, out uri) && IsHttp(uri)))
        {
            return uri;
        }

        throw new ArgumentException(
            client.BaseAddress is null
                ? $"'{url}' isn't an http or https URL."
                : $"'{url}' isn't an http or https URL, or one relative to {client.BaseAddress}.",
            nameof(url));
    }

    /// <summary>
    /// The file name to save a response under: its <c>Content-Disposition</c> name, or else the last segment of the
    /// final URL, or else <c>download</c>, with any folders dropped and characters files can't have replaced.
    /// </summary>
    internal static string FileNameFrom(HttpResponseMessage response, Uri address)
    {
        ContentDispositionHeaderValue? disposition = response.Content.Headers.ContentDisposition;
        string? name = (disposition?.FileNameStar ?? disposition?.FileName)?.Trim('"', '\'', ' ');
        if (string.IsNullOrWhiteSpace(name))
        {
            Uri final = response.RequestMessage?.RequestUri ?? address;
            name = Uri.UnescapeDataString(final.AbsolutePath.TrimEnd('/').Split('/')[^1]);
        }

        name = name.Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        return string.IsNullOrWhiteSpace(name) || name.Trim('.', ' ').Length == 0
            ? "download"
            : Path.SanitizeFileName(name);
    }

    /// <summary>The hash's bytes, or null for no hash. Takes 64 hex characters, optionally after <c>sha256:</c>.</summary>
    internal static byte[]? ParseSha256(string? sha256, string paramName)
    {
        if (sha256 is null)
        {
            return null;
        }

        string hex = sha256.Trim();
        if (hex.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            hex = hex["sha256:".Length..];
        }

        if (hex.Length != 64 || !hex.All(char.IsAsciiHexDigit))
        {
            throw new ArgumentException(
                $"\"{sha256}\" isn't a SHA-256 hash: 64 hex characters, optionally after \"sha256:\".", paramName);
        }

        return Convert.FromHexString(hex);
    }

    private static bool IsHttp(Uri uri) => uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

    private static void ValidateMaxBytes(long? maxBytes, string paramName)
    {
        if (maxBytes is < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, maxBytes, "The byte limit can't be negative.");
        }
    }

    private static Error? CheckTarget(string fullPath, bool overwrite)
    {
        if (Directory.Exists(fullPath))
        {
            return new Error($"Couldn't save {fullPath}: it's a folder.", ErrorCodes.FileSystem);
        }

        if (!overwrite && File.Exists(fullPath))
        {
            return new Error($"{fullPath} already exists.", ErrorCodes.FileExists);
        }

        return null;
    }

    private static HttpRequestMessage CreateRequest(Uri address, HttpClient client, Action<HttpRequestMessage>? configure)
    {
        HttpRequestMessage request = new(HttpMethod.Get, address);
        configure?.Invoke(request);
        SharedHttp.AddUserAgent(request, client);
        return request;
    }

    // Copies source to destination, counting from start, reporting progress, enforcing maxBytes and feeding the hash.
    private static async ValueTask CopyAsync(Stream source, Stream destination, long start, long? total, long? maxBytes,
        IProgress<DownloadProgress>? progress, IncrementalHash? hash, bool sync, CancellationToken cancellationToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long received = start;
            long lastReport = Environment.TickCount64;
            progress?.Report(new DownloadProgress(received, total));
            while (true)
            {
                int read;
                if (sync)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    read = source.Read(buffer, 0, BufferSize);
                }
                else
                {
                    read = await source.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false);
                }

                if (read == 0)
                {
                    break;
                }

                received += read;
                if (maxBytes is long max && received > max)
                {
                    throw new ContentTooLargeException(max);
                }

                hash?.AppendData(buffer, 0, read);
                if (sync)
                {
                    destination.Write(buffer, 0, read);
                }
                else
                {
                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                }

                long now = Environment.TickCount64;
                if (progress is not null && now - lastReport >= ProgressIntervalMs)
                {
                    lastReport = now;
                    progress.Report(new DownloadProgress(received, total));
                }
            }

            progress?.Report(new DownloadProgress(received, total));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static Result<byte[]> ToBytes(byte[] bytes, HttpContent content, Uri address) => Result<byte[]>.Success(bytes);

    // Not a lambda with ?:, since string converts to Error implicitly and the text would become a failure.
    private static Result<string> ToText(byte[] bytes, HttpContent content, Uri address) =>
        Result<string>.Success(Decode(bytes, content));

    private static string Decode(byte[] bytes, HttpContent content)
    {
        using StreamReader reader = new(new MemoryStream(bytes), SharedHttp.GetEncoding(content), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static Func<byte[], HttpContent, Uri, Result<T>> JsonConverter<T>(JsonSerializerOptions? options)
    {
        JsonSerializerOptions resolved = options ?? JsonSerializerOptions.Niddy;
        return (bytes, content, address) => Deserialize(bytes, content, address,
            utf8 => JsonSerializer.Deserialize<T>(utf8, resolved),
            text => JsonSerializer.Deserialize<T>(text, resolved));
    }

    private static Func<byte[], HttpContent, Uri, Result<T>> JsonConverter<T>(JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        return (bytes, content, address) => Deserialize(bytes, content, address,
            utf8 => JsonSerializer.Deserialize(utf8, typeInfo),
            text => JsonSerializer.Deserialize(text, typeInfo));
    }

    private static Result<T> Deserialize<T>(byte[] bytes, HttpContent content, Uri address,
        Func<byte[], T?> fromUtf8, Func<string, T?> fromText)
    {
        try
        {
            Encoding? declared = SharedHttp.DeclaredEncoding(content);
            T? value = declared is null || declared.CodePage == Encoding.UTF8.CodePage
                ? fromUtf8(bytes)
                : fromText(Decode(bytes, content));
            if (value is null)
            {
                return new Error($"{Describe(address)} returned JSON null.", ErrorCodes.InvalidContent);
            }

            return Result<T>.Success(value);
        }
        catch (JsonException ex)
        {
            return new Error($"{Describe(address)} didn't return valid JSON for {typeof(T).Name}: {ex.Message}",
                ErrorCodes.InvalidContent, ex);
        }
    }

    // The query is left out of messages: it can hold keys or tokens.
    private static string Describe(Uri address) => address.GetLeftPart(UriPartial.Path);

    private static Error TooLarge(Uri address, long maxBytes) =>
        new($"{Describe(address)} is larger than the {Humanize.Bytes(maxBytes)} limit.", ErrorCodes.TooLarge);

    private static Error ToError(HttpResponseMessage response, Uri address)
    {
        HttpStatusCode status = response.StatusCode;
        string message = $"{Describe(response.RequestMessage?.RequestUri ?? address)} returned {(int)status} {response.ReasonPhrase}.";
        string code = status is HttpStatusCode.NotFound or HttpStatusCode.Gone ? ErrorCodes.NotFound : ErrorCodes.HttpError;
        return new Error(message, code, new HttpRequestException(message, null, status));
    }

    // Null for exceptions that should propagate: the caller's cancellation, and anything unexpected.
    private static Error? ToError(Exception exception, Uri address, string? path, CancellationToken cancellationToken) =>
        exception switch
        {
            OperationCanceledException when cancellationToken.IsCancellationRequested => null,
            OperationCanceledException => new Error($"{address.Host} didn't respond in time.", ErrorCodes.Network, exception),
            HttpIOException => new Error($"The connection to {address.Host} broke off: {exception.Message}", ErrorCodes.Network, exception),
            HttpRequestException => new Error($"Couldn't reach {address.Host}: {exception.Message}", ErrorCodes.Network, exception),
            IOException or UnauthorizedAccessException when path is not null =>
                new Error($"Couldn't save {path}: {exception.Message}", ErrorCodes.FileSystem, exception),
            _ => null,
        };

    /// <summary>A response body on its way to a file: the copy, and the checksum check at the end.</summary>
    private sealed class Body(HttpResponseMessage response, Uri address, long start, long? total, DownloadOptions options,
        IncrementalHash? hash, byte[]? expectedHash)
    {
        public HttpResponseMessage Response => response;

        public IncrementalHash? Hash => hash;

        public async ValueTask SaveAsync(Stream file, bool sync, CancellationToken cancellationToken)
        {
            using (Stream source = await SharedHttp.ReadStreamAsync(response.Content, sync, cancellationToken).ConfigureAwait(false))
            {
                await CopyAsync(source, file, start, total, options.MaxBytes, options.Progress, hash, sync, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (hash is not null && expectedHash is not null)
            {
                byte[] actual = hash.GetHashAndReset();
                if (!CryptographicOperations.FixedTimeEquals(actual, expectedHash))
                {
                    throw new ChecksumMismatchException(
                        $"{Describe(address)} didn't match the expected SHA-256 {Convert.ToHexStringLower(expectedHash)} " +
                        $"(it was {Convert.ToHexStringLower(actual)}), so it wasn't saved.");
                }
            }
        }
    }

    /// <summary>
    /// A resumable download's hidden <c>.{id}.download</c> file in the target folder, and the <c>.validator</c> file
    /// beside it holding the <c>ETag</c> or <c>Last-Modified</c> it was downloaded with.
    /// </summary>
    private sealed class PartialDownload
    {
        private PartialDownload(string path)
        {
            this.Path = path;
            this.ValidatorPath = path + ".validator";
        }

        public string Path { get; }

        public string ValidatorPath { get; }

        public static PartialDownload For(string directory, Uri address)
        {
            byte[] id = SHA256.HashData(Encoding.UTF8.GetBytes(address.AbsoluteUri));
            return new PartialDownload(System.IO.Path.Combine(directory, $".{Convert.ToHexStringLower(id.AsSpan(0, 8))}.download"));
        }

        /// <summary>How many bytes were kept and the condition to resume them with, or (0, null) for nothing to resume.</summary>
        public (long Length, RangeConditionHeaderValue? Condition) Load()
        {
            try
            {
                if (!File.Exists(this.Path) || !File.Exists(this.ValidatorPath))
                {
                    return (0, null);
                }

                long length = new FileInfo(this.Path).Length;
                RangeConditionHeaderValue? condition = ParseCondition(File.ReadAllText(this.ValidatorPath).Trim());
                return length > 0 && condition is not null ? (length, condition) : (0, null);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return (0, null);
            }
        }

        /// <summary>
        /// Writes the body to the partial file, after what's already there when <paramref name="resumed"/> (feeding
        /// those bytes to the hash first), and records the response's validator so a failure can resume.
        /// </summary>
        public async ValueTask SaveAsync(Body body, bool resumed, bool sync, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(this.Path)!);
            this.SaveValidator(body, resumed);
            using FileStream file = new(this.Path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None,
                bufferSize: 4096, useAsync: !sync);
            if (resumed)
            {
                if (body.Hash is not null)
                {
                    await HashExistingAsync(file, body.Hash, sync, cancellationToken).ConfigureAwait(false);
                }

                file.Seek(0, SeekOrigin.End);
            }
            else
            {
                file.SetLength(0);
            }

            await body.SaveAsync(file, sync, cancellationToken).ConfigureAwait(false);
            if (!sync)
            {
                await file.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            file.Flush(flushToDisk: true);
        }

        public void DeleteValidator() => TryDelete(this.ValidatorPath);

        public void Delete()
        {
            TryDelete(this.Path);
            TryDelete(this.ValidatorPath);
        }

        private void SaveValidator(Body body, bool resumed)
        {
            if (resumed)
            {
                // The server confirmed the kept validator still holds.
                return;
            }

            HttpResponseMessage response = body.Response;
            string? validator = response.Headers.ETag is { IsWeak: false } etag
                ? "etag:" + etag.Tag
                : response.Content.Headers.LastModified is DateTimeOffset modified
                    ? "date:" + modified.ToString("r")
                    : null;
            if (validator is null)
            {
                TryDelete(this.ValidatorPath);
            }
            else
            {
                File.WriteAllText(this.ValidatorPath, validator);
            }
        }

        private static RangeConditionHeaderValue? ParseCondition(string validator)
        {
            if (validator.StartsWith("etag:", StringComparison.Ordinal)
                && EntityTagHeaderValue.TryParse(validator["etag:".Length..], out EntityTagHeaderValue? etag))
            {
                return new RangeConditionHeaderValue(etag);
            }

            if (validator.StartsWith("date:", StringComparison.Ordinal)
                && DateTimeOffset.TryParse(validator["date:".Length..], out DateTimeOffset date))
            {
                return new RangeConditionHeaderValue(date);
            }

            return null;
        }

        private static async ValueTask HashExistingAsync(FileStream file, IncrementalHash hash, bool sync,
            CancellationToken cancellationToken)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            try
            {
                file.Seek(0, SeekOrigin.Begin);
                int read;
                while ((read = sync
                           ? file.Read(buffer, 0, BufferSize)
                           : await file.ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort: a stray file is harmless and is replaced by the next download.
            }
        }
    }

    private sealed class ContentTooLargeException(long maxBytes) : Exception
    {
        public long MaxBytes => maxBytes;
    }

    private sealed class ChecksumMismatchException(string message) : Exception(message);
}
