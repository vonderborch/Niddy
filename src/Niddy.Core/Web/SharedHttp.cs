using System.Diagnostics;
using System.Net.Http.Headers;
using System.Runtime.Versioning;
using System.Text;

namespace Niddy.Core.Web;

/// <summary>
/// The HTTP clients and User-Agent Niddy.Core sends with when the caller doesn't pass a client, and the plumbing that
/// lets one implementation serve both the async methods and their synchronous twins.
/// </summary>
/// <remarks>
/// The synchronous methods share their async code: it takes a <c>sync</c> flag and, when it's set, calls only
/// synchronous APIs (<see cref="HttpClient.Send(HttpRequestMessage, HttpCompletionOption, CancellationToken)"/>,
/// <see cref="Stream.Read(byte[], int, int)"/> and so on), so the returned task has already completed and
/// <see cref="RunSync{T}"/> never blocks on real asynchronous work.
/// </remarks>
internal static class SharedHttp
{
    private static HttpClient? _client;

    private static HttpClient? _syncClient;

    /// <summary><c>Niddy.Core/{version}</c>. Some servers, GitHub's API among them, refuse requests without one.</summary>
    public static ProductInfoHeaderValue UserAgent { get; } =
        new("Niddy.Core", typeof(SharedHttp).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    /// <summary>One client for the whole process, so connections are reused.</summary>
    public static HttpClient Client => LazyInitializer.EnsureInitialized(ref _client, () => new HttpClient());

    /// <summary>
    /// The shared client for synchronous requests. It uses <see cref="SocketsHttpHandler"/>, which supports
    /// <see cref="HttpClient.Send(HttpRequestMessage)"/>; the native handlers <see cref="HttpClient"/> defaults to on
    /// Android and iOS don't. On desktop that's the default handler anyway.
    /// </summary>
    [UnsupportedOSPlatform("browser")]
    public static HttpClient SyncClient =>
        LazyInitializer.EnsureInitialized(ref _syncClient, () => new HttpClient(new SocketsHttpHandler()));

    /// <summary><paramref name="client"/>, or the shared client for async or synchronous requests.</summary>
    public static HttpClient ClientFor(HttpClient? client, bool sync)
    {
        if (client is not null)
        {
            return client;
        }

        if (!sync)
        {
            return Client;
        }

        if (OperatingSystem.IsBrowser())
        {
            throw new PlatformNotSupportedException("Synchronous HTTP requests aren't supported in the browser. Use the async methods.");
        }

        return SyncClient;
    }

    /// <summary>Adds <see cref="UserAgent"/> to <paramref name="request"/> unless the client already sends one.</summary>
    public static void AddUserAgent(HttpRequestMessage request, HttpClient client)
    {
        if (client.DefaultRequestHeaders.UserAgent.Count == 0 && request.Headers.UserAgent.Count == 0)
        {
            request.Headers.UserAgent.Add(UserAgent);
        }
    }

    /// <summary>Sends <paramref name="request"/> with <c>Send</c> or <c>SendAsync</c>.</summary>
    public static async ValueTask<HttpResponseMessage> SendAsync(HttpClient client, HttpRequestMessage request,
        HttpCompletionOption completionOption, bool sync, CancellationToken cancellationToken)
    {
        if (sync)
        {
            if (OperatingSystem.IsBrowser())
            {
                throw new PlatformNotSupportedException("Synchronous HTTP requests aren't supported in the browser. Use the async methods.");
            }

            return client.Send(request, completionOption, cancellationToken);
        }

        return await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the response body with <c>ReadAsStream</c> or <c>ReadAsStreamAsync</c>.</summary>
    public static async ValueTask<Stream> ReadStreamAsync(HttpContent content, bool sync, CancellationToken cancellationToken) =>
        sync ? content.ReadAsStream(cancellationToken) : await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

    /// <summary>Reads the whole response body as text, decoded like <c>ReadAsStringAsync</c> does.</summary>
    public static async ValueTask<string> ReadStringAsync(HttpContent content, bool sync, CancellationToken cancellationToken)
    {
        if (!sync)
        {
            return await content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }

        using Stream body = content.ReadAsStream(cancellationToken);
        using StreamReader reader = new(body, GetEncoding(content), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// The charset in the response's <c>Content-Type</c>, or UTF-8 when there's none or it's unknown, as browsers do.
    /// </summary>
    public static Encoding GetEncoding(HttpContent content) => DeclaredEncoding(content) ?? Encoding.UTF8;

    /// <summary>The charset in the response's <c>Content-Type</c>, or null when there's none or it's unknown.</summary>
    public static Encoding? DeclaredEncoding(HttpContent content)
    {
        string? charset = content.Headers.ContentType?.CharSet?.Trim('"', '\'', ' ');
        if (string.IsNullOrEmpty(charset))
        {
            return null;
        }

        try
        {
            return charset.Equals("utf8", StringComparison.OrdinalIgnoreCase) ? Encoding.UTF8 : Encoding.GetEncoding(charset);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>The result of a task started with <c>sync: true</c>, which has already completed.</summary>
    public static T RunSync<T>(ValueTask<T> task)
    {
        Debug.Assert(task.IsCompleted, "A sync: true call awaited real asynchronous work.");
        return task.GetAwaiter().GetResult();
    }

    /// <inheritdoc cref="RunSync{T}(ValueTask{T})"/>
    public static void RunSync(ValueTask task)
    {
        Debug.Assert(task.IsCompleted, "A sync: true call awaited real asynchronous work.");
        task.GetAwaiter().GetResult();
    }
}
