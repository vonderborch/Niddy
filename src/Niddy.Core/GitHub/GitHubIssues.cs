using System.ComponentModel;
using System.Net;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// Opens GitHub issues from anywhere in an app: bug reports, feature requests, anything.
/// <see cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>
/// creates the issue through the REST API with a token; <see cref="OpenInBrowser"/> needs no token and opens GitHub's
/// new-issue form, filled in, for the user to submit;
/// <see cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>
/// tries the first and falls back to
/// the second. It also reads existing issues: <see cref="ListAsync"/>, <see cref="GetAsync"/>,
/// <see cref="SearchAsync"/> and <see cref="ListCommentsAsync"/>.
/// </summary>
/// <example>
/// <code>
/// Result&lt;GitHubIssueReference&gt; result = await GitHubIssues.CreateAsync(
///     "vonderborch/Niddy", token, GitHubIssue.Bug("Crash when saving", details) with { IncludeSystemInfo = true });
/// </code>
/// </example>
public static partial class GitHubIssues
{
    /// <summary>
    /// The <see cref="Error.Code"/> values
    /// <see cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>,
    /// <see cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>
    /// and the reads
    /// fail with.
    /// </summary>
    public static class ErrorCodes
    {
        /// <summary>401: the token is missing, invalid or expired.</summary>
        public const string Unauthorized = "Unauthorized";

        /// <summary>403: the token can't create issues in the repository, or can't read it.</summary>
        public const string Forbidden = "Forbidden";

        /// <summary>
        /// 403 or 429 from GitHub's rate limits, with nothing cached to fall back on. Try again later, or use a token.
        /// </summary>
        public const string RateLimited = "RateLimited";

        /// <summary>404: the repository or issue doesn't exist, or the token can't see it.</summary>
        public const string NotFound = "NotFound";

        /// <summary>410: the repository has issues turned off.</summary>
        public const string IssuesDisabled = "IssuesDisabled";

        /// <summary>
        /// 422: GitHub rejected the issue, e.g. an unknown issue type or milestone, or a read's filter or search query.
        /// </summary>
        public const string Invalid = "Invalid";

        /// <summary>Any other response, or a success response that couldn't be read.</summary>
        public const string Failed = "Failed";

        /// <summary>
        /// The request didn't get a response (with nothing cached to fall back on, for reads): no connection, DNS
        /// failure, or a timeout.
        /// </summary>
        public const string Network = "Network";

        /// <summary><see cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>
        /// needed the browser and couldn't open it.</summary>
        public const string BrowserUnavailable = "BrowserUnavailable";
    }

    /// <summary>The longest issue body GitHub accepts, in characters.</summary>
    internal const int MaxBodyLength = 65_536;

    /// <summary>How long <see cref="GetNewIssueUrl"/> lets a URL get before it shortens the body.</summary>
    internal const int MaxUrlLength = 8_000;

    /// <summary>
    /// Creates an issue through GitHub's REST API (<c>POST /repos/{owner}/{repo}/issues</c>).
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="token">
    /// A token that can create issues in <paramref name="repository"/>. Best is a fine-grained personal access token
    /// limited to that one repository with only the Issues (read and write) permission. A token inside a shipped app
    /// can be extracted, so anyone can use it with those permissions; for anything more, send reports to your own
    /// server and create the issue there.
    /// </param>
    /// <param name="issue">
    /// The issue. Its body is <see cref="GitHubIssue.FormatBody()"/>, cut to GitHub's 65,536-character limit: sections
    /// first, then the body text. If it has a <see cref="GitHubIssue.Fingerprint"/> and there's already an open issue
    /// with the same one, the report is added to that issue as a comment instead.
    /// </param>
    /// <param name="httpClient">The client to send with, or null for a shared one.</param>
    /// <param name="apiAddress">
    /// The API address, for GitHub Enterprise Server, e.g. <c>https://github.example.com/api/v3/</c>. Null uses
    /// <paramref name="httpClient"/>'s <see cref="HttpClient.BaseAddress"/>, or <c>https://api.github.com/</c> without
    /// one. It takes precedence over the client's, so one client can send to several servers. The token is sent here.
    /// </param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The new issue, or the existing one with <see cref="GitHubIssueReference.CommentUrl"/> set, or a failure whose
    /// <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>. Failures from an HTTP response carry an
    /// <see cref="HttpRequestException"/> with the status code, and their message includes GitHub's.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, <paramref name="token"/> is blank, the issue's title is
    /// blank, or <paramref name="apiAddress"/> isn't an absolute <c>http</c> or <c>https</c> address.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubIssueReference>> CreateAsync(string repository, string token,
        GitHubIssue issue, HttpClient? httpClient = null, Uri? apiAddress = null,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(repository, token, issue, httpClient, apiAddress, false, cancellationToken).AsTask();

    /// <summary>
    /// Creates an issue through GitHub's REST API, blocking until it's done. Otherwise the same as
    /// <see cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>. Like
    /// <see cref="WebContent"/>'s synchronous methods it doesn't work in the browser, and a client you pass needs a
    /// handler that supports synchronous sends.
    /// </summary>
    /// <inheritdoc cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)" path="/param"/>
    /// <inheritdoc cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubIssueReference> Create(string repository, string token, GitHubIssue issue,
        HttpClient? httpClient = null, Uri? apiAddress = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(CreateCoreAsync(repository, token, issue, httpClient, apiAddress, true, cancellationToken));

    // The signatures from before apiAddress, so calls that pass the token positionally still compile.

    /// <inheritdoc cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Task<Result<GitHubIssueReference>> CreateAsync(string repository, string token, GitHubIssue issue,
        HttpClient? httpClient, CancellationToken cancellationToken) =>
        CreateAsync(repository, token, issue, httpClient, null, cancellationToken);

    /// <inheritdoc cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubIssueReference> Create(string repository, string token, GitHubIssue issue,
        HttpClient? httpClient, CancellationToken cancellationToken) =>
        Create(repository, token, issue, httpClient, null, cancellationToken);

    /// <summary>
    /// The URL of GitHub's new-issue form for <paramref name="repository"/>, filled in with the issue's title,
    /// <see cref="GitHubIssue.FormatBody()"/>, labels, assignees and type. No token is involved: the user reviews the
    /// issue and submits it as themselves, and GitHub drops what they can't set. If the URL would be too long for
    /// browsers, the body is cut, sections first, and the cuts are marked "(cut)". <see cref="GitHubIssue.Milestone"/>
    /// is left out.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="issue">The issue.</param>
    /// <param name="baseAddress">The GitHub site, for GitHub Enterprise Server. Null means <c>https://github.com/</c>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, or the issue's title is blank.
    /// </exception>
    public static string GetNewIssueUrl(string repository, GitHubIssue issue, Uri? baseAddress = null)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ValidateIssue(issue);

        StringBuilder url = new(new Uri(GitHubApi.EnsureTrailingSlash(baseAddress ?? GitHubApi.DefaultWebAddress),
            $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(name)}/issues/new").AbsoluteUri);
        url.Append("?title=").Append(Uri.EscapeDataString(issue.Title.Trim()));
        AppendList(url, "labels", issue.Labels);
        AppendList(url, "assignees", issue.Assignees);
        if (!string.IsNullOrWhiteSpace(issue.Type))
        {
            url.Append("&type=").Append(Uri.EscapeDataString(issue.Type.Trim()));
        }

        string body = EscapedBodyThatFits(issue, MaxUrlLength - url.Length - "&body=".Length);
        if (body.Length > 0)
        {
            url.Append("&body=").Append(body);
        }

        return url.ToString();
    }

    /// <summary>
    /// Opens <see cref="GetNewIssueUrl"/> with <see cref="UrlLauncher.Open"/>, so the user can review and submit the issue
    /// in their browser. Needs no token. Throws <see cref="PlatformNotSupportedException"/> where
    /// <see cref="UrlLauncher.Open"/> does (Android, watchOS, WASI); in an Avalonia app, pass
    /// <see cref="GetNewIssueUrl"/> to its launcher there instead.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="issue">The issue.</param>
    /// <param name="baseAddress">The GitHub site, for GitHub Enterprise Server. Null means <c>https://github.com/</c>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, or the issue's title is blank.
    /// </exception>
    public static void OpenInBrowser(string repository, GitHubIssue issue, Uri? baseAddress = null) =>
        UrlLauncher.Open(GetNewIssueUrl(repository, issue, baseAddress));

    /// <summary>
    /// Creates the issue through the API when it can, and otherwise opens the filled-in new-issue form for the user to
    /// submit. It falls back to the browser when there's no token, or when the API refuses the request in a way the user
    /// might not hit: <see cref="ErrorCodes.Unauthorized"/>, <see cref="ErrorCodes.Forbidden"/>,
    /// <see cref="ErrorCodes.NotFound"/>, <see cref="ErrorCodes.RateLimited"/> or <see cref="ErrorCodes.Invalid"/>.
    /// Other failures, such as no network or issues being turned off, are returned, since the form wouldn't work either.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="token">A token as for <see cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>, or null to go straight to the browser.</param>
    /// <param name="issue">The issue.</param>
    /// <param name="httpClient">The client for the API call, as for <see cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>.</param>
    /// <param name="openUrl">
    /// Opens the form's URL. Null uses <see cref="UrlLauncher.Open"/>; in an Avalonia app pass its launcher, e.g.
    /// <c>url =&gt; _ = UriLauncher.TryLaunchAsync(control, url)</c>, which works on every platform. If it throws,
    /// the result is a failure with <see cref="ErrorCodes.BrowserUnavailable"/>.
    /// </param>
    /// <param name="baseAddress">
    /// The GitHub site for the form, as for <see cref="GetNewIssueUrl"/>. For GitHub Enterprise Server, set it as well
    /// as the API address, or the fallback opens github.com's form.
    /// </param>
    /// <param name="apiAddress">The API address, as for <see cref="CreateAsync(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/>.</param>
    /// <param name="cancellationToken">Cancels the API request.</param>
    /// <returns>Which way the issue went, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, the issue's title is blank, or
    /// <paramref name="apiAddress"/> isn't an absolute <c>http</c> or <c>https</c> address.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubIssueSubmission>> CreateOrOpenAsync(string repository, string? token,
        GitHubIssue issue, HttpClient? httpClient = null, Action<string>? openUrl = null, Uri? baseAddress = null,
        Uri? apiAddress = null, CancellationToken cancellationToken = default) =>
        CreateOrOpenCoreAsync(repository, token, issue, httpClient, openUrl, baseAddress, apiAddress, false,
            cancellationToken).AsTask();

    /// <summary>
    /// Creates the issue through the API when it can, and otherwise opens the filled-in new-issue form, blocking until
    /// it's done. Otherwise the same as
    /// <see cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>;
    /// see <see cref="Create(string, string, GitHubIssue, HttpClient?, Uri?, CancellationToken)"/> for where the
    /// synchronous
    /// versions work.
    /// </summary>
    /// <inheritdoc cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)" path="/param"/>
    /// <inheritdoc cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)" path="/returns"/>
    /// <inheritdoc cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubIssueSubmission> CreateOrOpen(string repository, string? token, GitHubIssue issue,
        HttpClient? httpClient = null, Action<string>? openUrl = null, Uri? baseAddress = null,
        Uri? apiAddress = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(CreateOrOpenCoreAsync(repository, token, issue, httpClient, openUrl, baseAddress, apiAddress,
            true, cancellationToken));

    /// <inheritdoc cref="CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static Task<Result<GitHubIssueSubmission>> CreateOrOpenAsync(string repository, string? token,
        GitHubIssue issue, HttpClient? httpClient, Action<string>? openUrl, Uri? baseAddress,
        CancellationToken cancellationToken) =>
        CreateOrOpenAsync(repository, token, issue, httpClient, openUrl, baseAddress, null, cancellationToken);

    /// <inheritdoc cref="CreateOrOpen(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>
    [EditorBrowsable(EditorBrowsableState.Never)]
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubIssueSubmission> CreateOrOpen(string repository, string? token, GitHubIssue issue,
        HttpClient? httpClient, Action<string>? openUrl, Uri? baseAddress, CancellationToken cancellationToken) =>
        CreateOrOpen(repository, token, issue, httpClient, openUrl, baseAddress, null, cancellationToken);

    private static async ValueTask<Result<GitHubIssueReference>> CreateCoreAsync(string repository, string token,
        GitHubIssue issue, HttpClient? httpClient, Uri? apiAddress, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ValidateIssue(issue);
        GitHubApi.CheckApiAddress(apiAddress, nameof(apiAddress));

        HttpClient client = SharedHttp.ClientFor(httpClient, sync);
        if (issue.FingerprintToken is { } fingerprint
            && await AddToOpenIssueAsync(client, apiAddress, owner, name, token, issue, fingerprint, sync,
                cancellationToken).ConfigureAwait(false) is { } existing)
        {
            return existing;
        }

        Uri address = GitHubApi.ApiAddress(client, apiAddress, $"{GitHubApi.RepositoryPath(owner, name)}/issues");
        using HttpRequestMessage request = GitHubApi.CreateRequest(HttpMethod.Post, address, client, token);
        request.Content = new StringContent(BuildRequestBody(issue).ToJsonString(), Encoding.UTF8, "application/json");

        try
        {
            GitHubResponse response = await GitHubApi.SendAsync(client, request, sync, cancellationToken).ConfigureAwait(false);
            return response.IsSuccess ? ReadCreatedIssue(response.Content) : ToError(response, $"{owner}/{name}");
        }
        catch (Exception ex) when (GitHubApi.ToNetworkError(ex, ErrorCodes.Network, cancellationToken) is { } error)
        {
            return error;
        }
    }

    /// <summary>
    /// Finds the oldest open issue whose body has <paramref name="fingerprint"/>'s marker and adds the report to it as
    /// a comment. Null if there's no such issue, or if the search or the comment fails, so that a new issue is opened
    /// instead: a duplicate is better than a lost report.
    /// </summary>
    private static async ValueTask<GitHubIssueReference?> AddToOpenIssueAsync(HttpClient client, Uri? apiAddress,
        string owner, string name, string token, GitHubIssue issue, string fingerprint, bool sync,
        CancellationToken cancellationToken)
    {
        try
        {
            string query = $"repo:{owner}/{name} is:issue is:open in:body \"{fingerprint}\"";
            Uri searchAddress = GitHubApi.ApiAddress(client, apiAddress,
                $"search/issues?q={Uri.EscapeDataString(query)}&sort=created&order=asc&per_page=20");
            using HttpRequestMessage search = GitHubApi.CreateRequest(HttpMethod.Get, searchAddress, client, token);
            GitHubResponse found = await GitHubApi.SendAsync(client, search, sync, cancellationToken).ConfigureAwait(false);
            if (!found.IsSuccess || ReadOpenIssue(found.Content, fingerprint) is not { } existing)
            {
                return null;
            }

            // The comment repeats the title only when it differs, e.g. a different message for the same crash.
            string title = issue.Title.Trim();
            string heading = title == existing.Title.Trim() ? string.Empty : $"**{title}**\n\n";
            string comment = heading + issue.FormatContent(MaxBodyLength - heading.Length);
            if (comment.Length == 0)
            {
                comment = "Reported again.";
            }

            Uri commentAddress = GitHubApi.ApiAddress(client, apiAddress,
                $"{GitHubApi.RepositoryPath(owner, name)}/issues/{existing.Number}/comments");
            using HttpRequestMessage post = GitHubApi.CreateRequest(HttpMethod.Post, commentAddress, client, token);
            post.Content = new StringContent(new JsonObject { ["body"] = comment }.ToJsonString(), Encoding.UTF8,
                "application/json");
            GitHubResponse commented = await GitHubApi.SendAsync(client, post, sync, cancellationToken).ConfigureAwait(false);
            if (!commented.IsSuccess)
            {
                return null;
            }

            // The comment exists even if its address can't be read, so the issue's stands in for it.
            return new GitHubIssueReference(existing.Number, existing.Url)
            {
                CommentUrl = ReadUrl(commented.Content) ?? existing.Url,
            };
        }
        catch (Exception ex) when (GitHubApi.ToNetworkError(ex, ErrorCodes.Network, cancellationToken) is not null)
        {
            return null;
        }
    }

    /// <summary>The first open issue in search results whose body really has the fingerprint, not just a near match.</summary>
    private static (int Number, Uri Url, string Title)? ReadOpenIssue(string content, string fingerprint)
    {
        try
        {
            if (JsonNode.Parse(content)?["items"] is not JsonArray items)
            {
                return null;
            }

            foreach (JsonNode? item in items)
            {
                if (item is not JsonObject found || found["pull_request"] is not null
                    || found["state"]?.GetValue<string>() != "open"
                    || found["body"]?.GetValue<string>()?.Contains(fingerprint, StringComparison.Ordinal) != true)
                {
                    continue;
                }

                int? number = found["number"]?.GetValue<int>();
                if (number is int n && Uri.TryCreate(found["html_url"]?.GetValue<string>(), UriKind.Absolute, out Uri? url))
                {
                    return (n, url, found["title"]?.GetValue<string>() ?? string.Empty);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            // Unreadable results are no results: a new issue is opened.
        }

        return null;
    }

    private static Uri? ReadUrl(string content)
    {
        try
        {
            return Uri.TryCreate(JsonNode.Parse(content)?["html_url"]?.GetValue<string>(), UriKind.Absolute, out Uri? url)
                ? url
                : null;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static async ValueTask<Result<GitHubIssueSubmission>> CreateOrOpenCoreAsync(string repository,
        string? token, GitHubIssue issue, HttpClient? httpClient, Action<string>? openUrl, Uri? baseAddress,
        Uri? apiAddress, bool sync, CancellationToken cancellationToken)
    {
        GitHubApi.CheckApiAddress(apiAddress, nameof(apiAddress));
        string url = GetNewIssueUrl(repository, issue, baseAddress);
        Error? apiError = null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            Result<GitHubIssueReference> created =
                await CreateCoreAsync(repository, token, issue, httpClient, apiAddress, sync, cancellationToken)
                    .ConfigureAwait(false);
            if (created.IsSuccess)
            {
                return GitHubIssueSubmission.Created(created.Value!);
            }

            apiError = created.Error!;
            if (apiError.Code is not (ErrorCodes.Unauthorized or ErrorCodes.Forbidden or ErrorCodes.NotFound
                or ErrorCodes.RateLimited or ErrorCodes.Invalid))
            {
                return apiError;
            }
        }

        try
        {
            (openUrl ?? UrlLauncher.Open)(url);
        }
        catch (Exception ex)
        {
            string reason = apiError is null ? string.Empty : $" (GitHub refused the issue first: {apiError.Message})";
            return new Error($"Couldn't open the browser: {ex.Message}{reason}", ErrorCodes.BrowserUnavailable, ex);
        }

        return GitHubIssueSubmission.Opened(url, apiError);
    }

    internal static JsonObject BuildRequestBody(GitHubIssue issue)
    {
        JsonObject json = new() { ["title"] = issue.Title.Trim() };
        string body = issue.FormatBody(MaxBodyLength);
        if (body.Length > 0)
        {
            json["body"] = body;
        }

        string[] labels = Clean(issue.Labels);
        if (labels.Length > 0)
        {
            json["labels"] = new JsonArray([.. labels.Select(label => (JsonNode?)label)]);
        }

        string[] assignees = Clean(issue.Assignees);
        if (assignees.Length > 0)
        {
            json["assignees"] = new JsonArray([.. assignees.Select(user => (JsonNode?)user)]);
        }

        if (issue.Milestone is int milestone)
        {
            json["milestone"] = milestone;
        }

        if (!string.IsNullOrWhiteSpace(issue.Type))
        {
            json["type"] = issue.Type.Trim();
        }

        return json;
    }

    private static Result<GitHubIssueReference> ReadCreatedIssue(string content)
    {
        try
        {
            JsonNode? json = JsonNode.Parse(content);
            int? number = json?["number"]?.GetValue<int>();
            string? url = json?["html_url"]?.GetValue<string>();
            if (number is int n && Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            {
                return new GitHubIssueReference(n, uri);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return new Error("GitHub created the issue, but its response couldn't be read.", ErrorCodes.Failed, ex);
        }

        return new Error("GitHub created the issue, but its response had no issue number or URL.", ErrorCodes.Failed);
    }

    private static Error ToError(GitHubResponse response, string repository)
    {
        HttpStatusCode status = response.Status;
        (string message, string code) = status switch
        {
            _ when response.IsRateLimited => ("GitHub's rate limit was hit. Try again later.", ErrorCodes.RateLimited),
            HttpStatusCode.Unauthorized => ("The GitHub token is missing, invalid or expired.", ErrorCodes.Unauthorized),
            HttpStatusCode.Forbidden =>
                ($"The GitHub token can't create issues in {repository}. It needs the Issues (write) permission.",
                    ErrorCodes.Forbidden),
            HttpStatusCode.NotFound =>
                ($"The repository {repository} doesn't exist, or the token can't see it.", ErrorCodes.NotFound),
            HttpStatusCode.Gone => ($"{repository} has issues turned off.", ErrorCodes.IssuesDisabled),
            HttpStatusCode.UnprocessableEntity => ("GitHub rejected the issue.", ErrorCodes.Invalid),
            _ => ($"GitHub returned {(int)status} {response.ReasonPhrase}.", ErrorCodes.Failed),
        };

        return GitHubApi.ToError(response, message, code);
    }

    private static void ValidateIssue(GitHubIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (string.IsNullOrWhiteSpace(issue.Title))
        {
            throw new ArgumentException("The issue needs a title.", nameof(issue));
        }
    }

    private static string[] Clean(IReadOnlyList<string>? values) =>
        [.. (values ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim().TrimStart('@')).Distinct()];

    private static void AppendList(StringBuilder url, string name, IReadOnlyList<string> values)
    {
        string[] cleaned = Clean(values);
        if (cleaned.Length > 0)
        {
            url.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(string.Join(",", cleaned)));
        }
    }

    /// <summary>
    /// The issue's body escaped for a URL, as long as fits in <paramref name="available"/> characters: whole if it
    /// fits, otherwise cut the way <see cref="GitHubIssue.FormatBody()"/> cuts, sections first.
    /// </summary>
    private static string EscapedBodyThatFits(GitHubIssue issue, int available)
    {
        string full = issue.FormatBody();
        string escaped = Uri.EscapeDataString(full);
        if (escaped.Length <= available)
        {
            return escaped;
        }

        // The longest body whose escaped form fits.
        int low = 0;
        int high = full.Length;
        string best = string.Empty;
        while (low <= high)
        {
            int length = low + ((high - low) / 2);
            string candidate = Uri.EscapeDataString(issue.FormatBody(length));
            if (candidate.Length <= available)
            {
                best = candidate;
                low = length + 1;
            }
            else
            {
                high = length - 1;
            }
        }

        return best;
    }
}
