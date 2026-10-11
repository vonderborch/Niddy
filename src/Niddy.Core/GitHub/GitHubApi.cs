using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.IO;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// What <see cref="GitHubIssues"/> and <see cref="GitHubReleases"/> share about talking to GitHub's REST API: its
/// addresses and headers, reading its errors, and a cached <c>GET</c> that revalidates with <c>ETag</c>s.
/// </summary>
internal static class GitHubApi
{
    public static readonly Uri DefaultApiAddress = new("https://api.github.com/");

    public static readonly Uri DefaultWebAddress = new("https://github.com/");

    public static (string Owner, string Name) ParseRepository(string repository)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        string[] parts = repository.Trim().Split('/');
        if (parts.Length != 2 || parts.Any(part => part.Length == 0 || part.Any(char.IsWhiteSpace)))
        {
            throw new ArgumentException($"Expected a repository as owner/name, got \"{repository}\".", nameof(repository));
        }

        return (parts[0], parts[1]);
    }

    /// <summary><c>repos/{owner}/{name}</c>, escaped, for building API paths.</summary>
    public static string RepositoryPath(string owner, string name) =>
        $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}";

    /// <summary><paramref name="relative"/> under the client's <see cref="HttpClient.BaseAddress"/>, or api.github.com.</summary>
    public static Uri ApiAddress(HttpClient client, string relative) =>
        new(EnsureTrailingSlash(client.BaseAddress ?? DefaultApiAddress), relative);

    public static Uri EnsureTrailingSlash(Uri address) =>
        address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");

    public static HttpRequestMessage CreateRequest(HttpMethod method, Uri address, HttpClient client, string? token)
    {
        HttpRequestMessage request = new(method, address);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        AddApiHeaders(request, token);
        SharedHttp.AddUserAgent(request, client);
        return request;
    }

    /// <summary>The API version header and, when there's a token, the authorization.</summary>
    public static void AddApiHeaders(HttpRequestMessage request, string? token)
    {
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }
    }

    /// <summary>Sends <paramref name="request"/> and reads the whole response.</summary>
    public static async ValueTask<GitHubResponse> SendAsync(HttpClient client, HttpRequestMessage request, bool sync,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await SharedHttp
            .SendAsync(client, request, HttpCompletionOption.ResponseContentRead, sync, cancellationToken)
            .ConfigureAwait(false);
        string content = await SharedHttp.ReadStringAsync(response.Content, sync, cancellationToken).ConfigureAwait(false);
        return GitHubResponse.From(response, content);
    }

    /// <summary>
    /// A <c>GET</c> that, given a <paramref name="cacheDirectory"/>, keeps successful responses on disk: a response
    /// younger than <paramref name="cacheDuration"/> is served without a request, an older one is revalidated with
    /// <c>If-None-Match</c> (a <c>304</c> doesn't count against the rate limit), and when GitHub can't be reached,
    /// is rate limiting, or fails with a 5xx, the cached copy is served however old it is.
    /// </summary>
    /// <remarks>Network failures with nothing cached propagate, for the caller to turn into a result.</remarks>
    public static async ValueTask<GitHubResponse> GetAsync(HttpClient client, Uri address, string? token,
        string? cacheDirectory, TimeSpan cacheDuration, bool sync, CancellationToken cancellationToken)
    {
        string? cachePath = cacheDirectory is null ? null : CachePath(cacheDirectory, address, token);
        CacheEntry? cached = cachePath is null ? null : CacheEntry.TryLoad(cachePath, address);
        if (cached is not null && DateTimeOffset.UtcNow - cached.FetchedAt < cacheDuration)
        {
            return GitHubResponse.Cached(cached.Body);
        }

        GitHubResponse response;
        try
        {
            using HttpRequestMessage request = CreateRequest(HttpMethod.Get, address, client, token);
            if (cached?.ETag is { } etag && EntityTagHeaderValue.TryParse(etag, out EntityTagHeaderValue? tag))
            {
                request.Headers.IfNoneMatch.Add(tag);
            }

            response = await SendAsync(client, request, sync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (cached is not null && IsNetworkFailure(ex, cancellationToken))
        {
            return GitHubResponse.Cached(cached.Body);
        }

        if (cached is not null && response.Status == HttpStatusCode.NotModified)
        {
            (cached with { FetchedAt = DateTimeOffset.UtcNow }).TrySave(cachePath!);
            return GitHubResponse.Cached(cached.Body);
        }

        if (response.IsSuccess)
        {
            if (cachePath is not null)
            {
                new CacheEntry(address.AbsoluteUri, response.ETag, DateTimeOffset.UtcNow, response.Content).TrySave(cachePath);
            }

            return response;
        }

        return cached is not null && (response.IsRateLimited || (int)response.Status >= 500)
            ? GitHubResponse.Cached(cached.Body)
            : response;
    }

    /// <summary>
    /// The error for a failed send, or null for the caller's cancellation and anything unexpected, which propagate.
    /// </summary>
    public static Error? ToNetworkError(Exception exception, string code, CancellationToken cancellationToken) =>
        exception switch
        {
            OperationCanceledException when cancellationToken.IsCancellationRequested => null,
            OperationCanceledException => new Error("GitHub didn't respond in time.", code, exception),
            HttpRequestException => new Error($"Couldn't reach GitHub: {exception.Message}", code, exception),
            _ => null,
        };

    /// <summary>GitHub's error message, plus the details of any field errors, or null if there isn't one.</summary>
    public static string? ReadMessage(string content)
    {
        try
        {
            JsonNode? json = JsonNode.Parse(content);
            string? message = json?["message"]?.GetValue<string>();
            if (json?["errors"] is JsonArray errors)
            {
                IEnumerable<string> details = errors.Select(error => error switch
                {
                    JsonObject o when o["message"] is JsonValue m => m.ToString(),
                    JsonObject o => string.Join(" ", new[] { o["field"], o["code"] }.Where(v => v is not null)),
                    JsonValue v => v.ToString(),
                    _ => string.Empty,
                }).Where(detail => detail.Length > 0);
                string joined = string.Join("; ", details);
                if (joined.Length > 0)
                {
                    message = message is null ? joined : $"{message} ({joined})";
                }
            }

            return string.IsNullOrWhiteSpace(message) ? null : message;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// An <see cref="Error"/> for a response that isn't a success, with GitHub's own message appended and an
    /// <see cref="HttpRequestException"/> carrying the status code.
    /// </summary>
    public static Error ToError(GitHubResponse response, string message, string code)
    {
        if (ReadMessage(response.Content) is { } gitHubMessage)
        {
            message += $" GitHub said: {gitHubMessage}";
        }

        return new Error(message, code, new HttpRequestException(message, null, response.Status));
    }

    private static bool IsNetworkFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException
        || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private static string CachePath(string cacheDirectory, Uri address, string? token)
    {
        // The token is part of the key, so what one token could see is never served to another.
        byte[] key = SHA256.HashData(Encoding.UTF8.GetBytes($"{address.AbsoluteUri}\n{token?.Trim()}"));
        return Path.Combine(Path.GetFullPath(cacheDirectory), $"{Convert.ToHexStringLower(key.AsSpan(0, 16))}.json");
    }

    /// <summary>A cached response: the URL it's for, its ETag, when it was last fetched or revalidated, and its body.</summary>
    private sealed record CacheEntry(string Url, string? ETag, DateTimeOffset FetchedAt, string Body)
    {
        public static CacheEntry? TryLoad(string path, Uri address)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }

                JsonNode? json = JsonNode.Parse(File.ReadAllText(path));
                string? url = json?["url"]?.GetValue<string>();
                string? body = json?["body"]?.GetValue<string>();
                DateTimeOffset? fetchedAt = json?["fetchedAt"]?.GetValue<DateTimeOffset>();
                return url == address.AbsoluteUri && body is not null && fetchedAt is DateTimeOffset at
                    ? new CacheEntry(url, json?["etag"]?.GetValue<string>(), at, body)
                    : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
                or InvalidOperationException or FormatException)
            {
                // A cache that can't be read is a cache miss.
                return null;
            }
        }

        public void TrySave(string path)
        {
            try
            {
                JsonObject json = new()
                {
                    ["url"] = this.Url,
                    ["etag"] = this.ETag,
                    ["fetchedAt"] = this.FetchedAt,
                    ["body"] = this.Body,
                };
                AtomicFile.WriteAllText(path, json.ToJsonString());
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Caching is best effort: the response was already read and is still returned.
            }
        }
    }
}

/// <summary>A GitHub API response, read in full, or a cached copy of one.</summary>
internal sealed record GitHubResponse(HttpStatusCode Status, string? ReasonPhrase, string Content, string? ETag,
    bool IsRateLimited, bool FromCache)
{
    public bool IsSuccess => (int)this.Status is >= 200 and <= 299;

    public static GitHubResponse Cached(string content) => new(HttpStatusCode.OK, "OK", content, null, false, true);

    public static GitHubResponse From(HttpResponseMessage response, string content)
    {
        HttpStatusCode status = response.StatusCode;
        bool rateLimited = status == HttpStatusCode.TooManyRequests
            || (status == HttpStatusCode.Forbidden
                && ((response.Headers.TryGetValues("x-ratelimit-remaining", out IEnumerable<string>? remaining)
                        && remaining.Contains("0"))
                    || response.Headers.Contains("retry-after")
                    || GitHubApi.ReadMessage(content)?.Contains("rate limit", StringComparison.OrdinalIgnoreCase) == true));
        return new GitHubResponse(status, response.ReasonPhrase, content, response.Headers.ETag?.ToString(), rateLimited, false);
    }
}
