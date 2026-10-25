using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Niddy.Core.Collections;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// What <see cref="GitHubIssues"/>' reads, <see cref="GitHubPullRequests"/> and <see cref="GitHubProjects"/> share:
/// cached REST reads with paging, GraphQL queries with cursors, their errors, and reading GitHub's JSON. Error codes
/// are <see cref="GitHubIssues.ErrorCodes"/>'; the other classes' <c>ErrorCodes</c> have the same values.
/// </summary>
internal static partial class GitHubReader
{
    /// <summary>The most a REST or GraphQL page holds.</summary>
    public const int MaxPageSize = 100;

    /// <summary>
    /// Reads up to <paramref name="maxCount"/> items from a paged REST list, adding <c>per_page</c> and <c>page</c> to
    /// <paramref name="relative"/>'s query string. <paramref name="parse"/> returns null for items to skip.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>>> ListAsync<T>(string relative, int maxCount,
        Func<JsonNode, JsonArray?> items, Func<JsonNode, T?> parse, GitHubReadOptions options, HttpClient? httpClient,
        string notFound, string display, bool sync, CancellationToken cancellationToken)
        where T : class
    {
        int perPage = Math.Min(maxCount, MaxPageSize);
        char separator = relative.Contains('?') ? '&' : '?';
        List<T> all = [];
        for (int page = 1; all.Count < maxCount; page++)
        {
            Result<JsonNode> json = await GetJsonAsync($"{relative}{separator}per_page={perPage}&page={page}", options,
                httpClient, notFound, display, sync, cancellationToken).ConfigureAwait(false);
            if (!json.TryGetValue(out JsonNode node, out Error? error))
            {
                return error!;
            }

            int count;
            try
            {
                count = ReadItems(node, items, parse, all, maxCount);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
            {
                return Unreadable(ex);
            }

            // Skipped items still count towards a full page, so a short page is the last one.
            if (count < perPage)
            {
                break;
            }
        }

        return Result<IReadOnlyList<T>>.Success(all);
    }

    /// <summary>
    /// Reads one page of a paged REST list, adding <c>per_page</c> and <c>page</c> to <paramref name="relative"/>'s
    /// query string. <paramref name="parse"/> returns null for items to skip, so a page can hold fewer than
    /// <paramref name="pageSize"/>. What's after it comes from <paramref name="totalCount"/> when given (search), where
    /// only the first <paramref name="maxResults"/> can be read, and otherwise from the <c>Link</c> header.
    /// </summary>
    public static async ValueTask<Result<ItemPage<T>>> PageAsync<T>(string relative, int page, int pageSize,
        Func<JsonNode, JsonArray?> items, Func<JsonNode, T?> parse, Func<JsonNode, int?>? totalCount, int? maxResults,
        GitHubReadOptions options, HttpClient? httpClient, string notFound, string display, bool sync,
        CancellationToken cancellationToken)
        where T : class
    {
        char separator = relative.Contains('?') ? '&' : '?';
        Result<(JsonNode Json, string? Link)> response = await GetJsonWithLinkAsync(
            $"{relative}{separator}per_page={pageSize}&page={page}", options, httpClient, notFound, display, sync,
            cancellationToken).ConfigureAwait(false);
        if (!response.TryGetValue(out (JsonNode Json, string? Link) read, out Error? error))
        {
            return error!;
        }

        List<T> found = [];
        int count;
        int? total;
        try
        {
            count = ReadItems(read.Json, items, parse, found, int.MaxValue);
            total = totalCount?.Invoke(read.Json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(ex);
        }

        if (total is int known)
        {
            int reachable = maxResults is int most ? Math.Min(known, most) : known;
            int pageCount = PageCount(reachable, pageSize);
            return new ItemPage<T>
            {
                Items = found, HasMore = page < pageCount, TotalCount = known, PageCount = pageCount,
            };
        }

        (bool hasMore, int? pages) = FromLink(read.Link, page, count, pageSize);
        return new ItemPage<T> { Items = found, HasMore = hasMore, PageCount = pages };
    }

    /// <summary>Reads one item with a REST <c>GET</c>.</summary>
    public static async ValueTask<Result<T>> GetAsync<T>(string relative, Func<JsonNode, T> parse,
        GitHubReadOptions options, HttpClient? httpClient, string notFound, string display, bool sync,
        CancellationToken cancellationToken)
    {
        Result<JsonNode> json = await GetJsonAsync(relative, options, httpClient, notFound, display, sync,
            cancellationToken).ConfigureAwait(false);
        if (!json.TryGetValue(out JsonNode node, out Error? error))
        {
            return error!;
        }

        try
        {
            return parse(node);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(ex);
        }
    }

    /// <summary>
    /// Reads up to <paramref name="maxCount"/> nodes of a GraphQL connection. The query takes <c>$first</c> and
    /// <c>$after</c>; <paramref name="connection"/> picks the connection out of <c>data</c>, and null from it means
    /// <paramref name="notFound"/>.
    /// </summary>
    public static async ValueTask<Result<IReadOnlyList<T>>> ListGraphQLAsync<T>(string query, JsonObject variables,
        int maxCount, Func<JsonNode, JsonNode?> connection, Func<JsonNode, T> parse, GitHubReadOptions options,
        HttpClient? httpClient, string notFound, bool sync, CancellationToken cancellationToken)
    {
        List<T> all = [];
        string? after = null;
        while (all.Count < maxCount)
        {
            Result<GraphQLPage<T>> page = await GraphQLPageAsync(query, variables, Math.Min(maxCount - all.Count, MaxPageSize),
                after, connection, parse, options, httpClient, notFound, sync, cancellationToken).ConfigureAwait(false);
            if (!page.TryGetValue(out GraphQLPage<T> read, out Error? error))
            {
                return error!;
            }

            all.AddRange(read.Items.Take(maxCount - all.Count));
            after = read.NextCursor;
            if (after is null)
            {
                break;
            }
        }

        return Result<IReadOnlyList<T>>.Success(all);
    }

    /// <summary>
    /// Reads one page of a GraphQL connection, as <see cref="ListGraphQLAsync{T}"/> does, after
    /// <paramref name="cursor"/> (null for the first page). The connection should select <c>totalCount</c>.
    /// </summary>
    public static async ValueTask<Result<ItemPage<T>>> PageGraphQLAsync<T>(string query, JsonObject variables,
        int pageSize, string? cursor, Func<JsonNode, JsonNode?> connection, Func<JsonNode, T> parse,
        GitHubReadOptions options, HttpClient? httpClient, string notFound, bool sync, CancellationToken cancellationToken)
    {
        Result<GraphQLPage<T>> page = await GraphQLPageAsync(query, variables, pageSize, cursor, connection, parse,
            options, httpClient, notFound, sync, cancellationToken).ConfigureAwait(false);
        if (!page.TryGetValue(out GraphQLPage<T> read, out Error? error))
        {
            return error!;
        }

        return new ItemPage<T>
        {
            Items = read.Items,
            HasMore = read.NextCursor is not null,
            NextCursor = read.NextCursor,
            TotalCount = read.TotalCount,
            PageCount = read.TotalCount is int total ? PageCount(total, pageSize) : null,
        };
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a <paramref name="page"/> under 1, or a
    /// <paramref name="pageSize"/> outside 1 to <see cref="MaxPageSize"/>.
    /// </summary>
    public static void CheckPage(int page, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        CheckPageSize(pageSize);
    }

    /// <summary>
    /// Throws <see cref="ArgumentOutOfRangeException"/> for a <paramref name="pageSize"/> outside 1 to
    /// <see cref="MaxPageSize"/>.
    /// </summary>
    public static void CheckPageSize(int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);
    }

    /// <summary>
    /// Runs a GraphQL query and returns its <c>data</c>. GraphQL errors, which come with a <c>200</c>, are failures:
    /// <c>NOT_FOUND</c> is <paramref name="notFound"/>.
    /// </summary>
    public static async ValueTask<Result<JsonNode>> GraphQLAsync(string query, JsonObject variables,
        GitHubReadOptions options, HttpClient? httpClient, string notFound, bool sync, CancellationToken cancellationToken)
    {
        HttpClient client = SharedHttp.ClientFor(httpClient, sync);
        GitHubResponse response;
        try
        {
            response = await GitHubApi.GraphQLAsync(client, query, variables, options.Token, options.CacheDirectory,
                options.CacheDuration, sync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (GitHubApi.ToNetworkError(ex, GitHubIssues.ErrorCodes.Network, cancellationToken) is { } error)
        {
            return error;
        }

        if (!response.IsSuccess)
        {
            return ToError(response, notFound, "the project");
        }

        JsonNode? json;
        try
        {
            json = JsonNode.Parse(response.Content);
        }
        catch (JsonException ex)
        {
            return Unreadable(ex);
        }

        if (json?["errors"] is JsonArray { Count: > 0 } errors)
        {
            return ToGraphQLError(errors, notFound);
        }

        return json?["data"] is { } data
            ? Result<JsonNode>.Success(data)
            : Unreadable(new JsonException("GitHub's response had no data."));
    }

    public static Error Unreadable(Exception exception) =>
        new($"GitHub's response couldn't be read: {exception.Message}", GitHubIssues.ErrorCodes.Failed, exception);

    /// <summary>A string property, or null if it's missing, JSON null, or blank.</summary>
    public static string? String(JsonNode? json, string name) =>
        json?[name] is JsonValue value && value.TryGetValue(out string? text) && !string.IsNullOrWhiteSpace(text)
            ? text
            : null;

    public static int? Int(JsonNode? json, string name) =>
        json?[name] is JsonValue value && value.TryGetValue(out int number) ? number : null;

    public static bool Bool(JsonNode? json, string name) =>
        json?[name] is JsonValue value && value.TryGetValue(out bool flag) && flag;

    public static DateTimeOffset? Date(JsonNode? json, string name) =>
        json?[name] is JsonValue value && value.TryGetValue(out DateTimeOffset at) ? at : null;

    public static Uri? Url(JsonNode? json, string name) =>
        Uri.TryCreate(String(json, name), UriKind.Absolute, out Uri? uri) ? uri : null;

    /// <summary>The logins of an array of users, skipping any without one.</summary>
    public static IReadOnlyList<string> Logins(JsonNode? users) =>
        users is JsonArray array ? [.. array.Select(user => String(user, "login")).OfType<string>()] : [];

    /// <summary>The names of an array of labels, which may be objects or plain strings.</summary>
    public static IReadOnlyList<string> Labels(JsonNode? labels) =>
        labels is JsonArray array
            ? [.. array.Select(label => label is JsonValue value && value.TryGetValue(out string? name) ? name : String(label, "name"))
                .OfType<string>()]
            : [];

    /// <summary><c>owner/name</c> from an API URL ending in <c>repos/{owner}/{name}</c>, or null.</summary>
    public static string? RepositoryFromApiUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string[] segments = uri.AbsolutePath.Trim('/').Split('/');
        return segments.Length >= 3 && segments[^3] == "repos"
            ? $"{Uri.UnescapeDataString(segments[^2])}/{Uri.UnescapeDataString(segments[^1])}"
            : null;
    }

    /// <summary>How many pages <paramref name="count"/> items take; an empty list is one empty page.</summary>
    private static int PageCount(int count, int pageSize) => Math.Max(1, (int)Math.Ceiling(count / (double)pageSize));

    /// <summary>
    /// Adds the parsed items of a REST list to <paramref name="into"/>, up to <paramref name="limit"/> in all, and
    /// returns how many the list held, skipped ones included.
    /// </summary>
    private static int ReadItems<T>(JsonNode node, Func<JsonNode, JsonArray?> items, Func<JsonNode, T?> parse,
        List<T> into, int limit)
        where T : class
    {
        JsonArray array = items(node) ?? throw new JsonException("Expected a list.");
        foreach (JsonNode? item in array)
        {
            if (item is not null && parse(item) is { } parsed && into.Count < limit)
            {
                into.Add(parsed);
            }
        }

        return array.Count;
    }

    /// <summary>
    /// Whether there's a page after <paramref name="page"/>, and how many there are, from a REST response's
    /// <c>Link</c> header: <c>rel="next"</c> means more, and <c>rel="last"</c>'s <c>page</c> is the count. Without a
    /// header (a single page, or a server that doesn't send one), a full page means there may be more.
    /// </summary>
    private static (bool HasMore, int? PageCount) FromLink(string? link, int page, int count, int pageSize)
    {
        if (link is null)
        {
            bool full = count >= pageSize;
            return (full, full ? null : page);
        }

        bool hasMore = false;
        int? last = null;
        foreach (Match match in LinkPattern().Matches(link))
        {
            string[] rels = match.Groups["rel"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (rels.Contains("next", StringComparer.OrdinalIgnoreCase))
            {
                hasMore = true;
            }

            if (rels.Contains("last", StringComparer.OrdinalIgnoreCase) && PageOf(match.Groups["url"].Value) is int n)
            {
                last = n;
            }
        }

        // The last page has no next or last link; past the end, the count isn't known.
        return (hasMore, last ?? (hasMore || (count == 0 && page > 1) ? null : page));
    }

    /// <summary>The <c>page</c> query parameter of a URL, or null.</summary>
    private static int? PageOf(string url)
    {
        int query = url.IndexOf('?');
        if (query < 0)
        {
            return null;
        }

        foreach (string pair in url[(query + 1)..].Split('&'))
        {
            if (pair.StartsWith("page=", StringComparison.Ordinal) && int.TryParse(pair.AsSpan(5), out int value))
            {
                return value;
            }
        }

        return null;
    }

    [GeneratedRegex(@"<(?<url>[^>]*)>\s*;\s*rel=""(?<rel>[^""]*)""")]
    private static partial Regex LinkPattern();

    /// <summary>One page of a GraphQL connection: its items, the cursor after it (null on the last page), and the total.</summary>
    private sealed record GraphQLPage<T>(IReadOnlyList<T> Items, string? NextCursor, int? TotalCount);

    private static async ValueTask<Result<GraphQLPage<T>>> GraphQLPageAsync<T>(string query, JsonObject variables,
        int first, string? after, Func<JsonNode, JsonNode?> connection, Func<JsonNode, T> parse,
        GitHubReadOptions options, HttpClient? httpClient, string notFound, bool sync, CancellationToken cancellationToken)
    {
        JsonObject pageVariables = (JsonObject)variables.DeepClone();
        pageVariables["first"] = first;
        pageVariables["after"] = after;
        Result<JsonNode> data = await GraphQLAsync(query, pageVariables, options, httpClient, notFound, sync,
            cancellationToken).ConfigureAwait(false);
        if (!data.TryGetValue(out JsonNode node, out Error? error))
        {
            return error!;
        }

        try
        {
            if (connection(node) is not { } page)
            {
                return new Error(notFound, GitHubIssues.ErrorCodes.NotFound);
            }

            if (page["nodes"] is not JsonArray nodes)
            {
                throw new JsonException("Expected a list of nodes.");
            }

            List<T> items = [.. nodes.OfType<JsonNode>().Select(parse)];
            string? next = page["pageInfo"]?["hasNextPage"]?.GetValue<bool>() == true
                ? page["pageInfo"]?["endCursor"]?.GetValue<string>()
                : null;
            return new GraphQLPage<T>(items, next, Int(page, "totalCount"));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return Unreadable(ex);
        }
    }

    private static async ValueTask<Result<JsonNode>> GetJsonAsync(string relative, GitHubReadOptions options,
        HttpClient? httpClient, string notFound, string display, bool sync, CancellationToken cancellationToken)
    {
        Result<(JsonNode Json, string? Link)> read = await GetJsonWithLinkAsync(relative, options, httpClient, notFound,
            display, sync, cancellationToken).ConfigureAwait(false);
        return read.TryGetValue(out (JsonNode Json, string? Link) value, out Error? error) ? value.Json : error!;
    }

    private static async ValueTask<Result<(JsonNode Json, string? Link)>> GetJsonWithLinkAsync(string relative,
        GitHubReadOptions options, HttpClient? httpClient, string notFound, string display, bool sync,
        CancellationToken cancellationToken)
    {
        HttpClient client = SharedHttp.ClientFor(httpClient, sync);
        Uri address = GitHubApi.ApiAddress(client, relative);
        GitHubResponse response;
        try
        {
            response = await GitHubApi.GetAsync(client, address, options.Token, options.CacheDirectory,
                options.CacheDuration, sync, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (GitHubApi.ToNetworkError(ex, GitHubIssues.ErrorCodes.Network, cancellationToken) is { } error)
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
                ? Result<(JsonNode Json, string? Link)>.Success((node, response.Link))
                : Unreadable(new JsonException("GitHub returned JSON null."));
        }
        catch (JsonException ex)
        {
            return Unreadable(ex);
        }
    }

    private static Error ToError(GitHubResponse response, string notFound, string display)
    {
        HttpStatusCode status = response.Status;
        (string message, string code) = status switch
        {
            _ when response.IsRateLimited =>
                ("GitHub's rate limit was hit. Try again later, use a token, or set a cache directory.",
                    GitHubIssues.ErrorCodes.RateLimited),
            HttpStatusCode.Unauthorized => ("The GitHub token is missing, invalid or expired.", GitHubIssues.ErrorCodes.Unauthorized),
            HttpStatusCode.Forbidden => ($"The GitHub token can't read {display}.", GitHubIssues.ErrorCodes.Forbidden),
            HttpStatusCode.NotFound => (notFound, GitHubIssues.ErrorCodes.NotFound),
            HttpStatusCode.Gone => ($"{display} has issues turned off.", GitHubIssues.ErrorCodes.IssuesDisabled),
            HttpStatusCode.UnprocessableEntity => ("GitHub rejected the request.", GitHubIssues.ErrorCodes.Invalid),
            _ => ($"GitHub returned {(int)status} {response.ReasonPhrase}.", GitHubIssues.ErrorCodes.Failed),
        };

        return GitHubApi.ToError(response, message, code);
    }

    private static Error ToGraphQLError(JsonArray errors, string notFound)
    {
        string? type = String(errors[0], "type");
        (string message, string code) = type switch
        {
            "NOT_FOUND" => (notFound, GitHubIssues.ErrorCodes.NotFound),
            "FORBIDDEN" or "INSUFFICIENT_SCOPES" =>
                ("The GitHub token can't read the project. Classic tokens need the read:project scope.",
                    GitHubIssues.ErrorCodes.Forbidden),
            "RATE_LIMITED" =>
                ("GitHub's rate limit was hit. Try again later, or set a cache directory.", GitHubIssues.ErrorCodes.RateLimited),
            _ => ("GitHub couldn't run the query.", GitHubIssues.ErrorCodes.Failed),
        };

        string said = string.Join("; ", errors.Select(error => String(error, "message")).OfType<string>());
        return new Error(said.Length > 0 ? $"{message} GitHub said: {said}" : message, code);
    }
}
