using System.Runtime.Versioning;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// <see cref="GitHubIssues"/> set up once for one repository: the token, client, browser launcher and default reporter
/// are given when it's made, so each report only needs the issue. Make one at startup and keep it.
/// </summary>
/// <example>
/// <code>
/// GitHubIssueClient issues = new("vonderborch/Niddy", token)
/// {
///     Reporter = new GitHubIssueReporter(Name: userName),
///     OpenUrl = url =&gt; _ = UriLauncher.TryLaunchAsync(mainWindow, url),
/// };
///
/// Result&lt;GitHubIssueSubmission&gt; sent = await issues.CreateOrOpenAsync(GitHubIssue.FromException(ex));
/// </code>
/// </example>
public sealed class GitHubIssueClient
{
    private readonly string? _token;

    /// <summary>Sets up issue reporting for <paramref name="repository"/>.</summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="token">
    /// A token that can create issues in <paramref name="repository"/>, as for <see cref="GitHubIssues.CreateAsync"/>,
    /// or null to only open the browser form.
    /// </param>
    /// <param name="httpClient">The client for API calls, as for <see cref="GitHubIssues.CreateAsync"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="repository"/> isn't <c>owner/name</c>.</exception>
    public GitHubIssueClient(string repository, string? token = null, HttpClient? httpClient = null)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        this.Repository = $"{owner}/{name}";
        this._token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        this.HttpClient = httpClient;
    }

    /// <summary>The repository, as <c>owner/name</c>.</summary>
    public string Repository { get; }

    /// <summary>Whether there's a token, so <see cref="CreateAsync"/> can be used.</summary>
    public bool HasToken => this._token is not null;

    /// <summary>The client for API calls, or null for the shared one.</summary>
    public HttpClient? HttpClient { get; }

    /// <summary>The reporter for issues that don't set their own <see cref="GitHubIssue.Reporter"/>.</summary>
    public GitHubIssueReporter? Reporter { get; init; }

    /// <summary>The GitHub site for the browser form, for GitHub Enterprise Server. Null means <c>https://github.com/</c>.</summary>
    public Uri? BaseAddress { get; init; }

    /// <summary>
    /// Opens the form's URL in <see cref="CreateOrOpenAsync"/>. Null uses <see cref="UrlLauncher.Open"/>; in an Avalonia
    /// app, set it to its launcher, e.g. <c>url =&gt; _ = UriLauncher.TryLaunchAsync(control, url)</c>.
    /// </summary>
    public Action<string>? OpenUrl { get; init; }

    /// <summary>Creates the issue through the API. See <see cref="GitHubIssues.CreateAsync"/>.</summary>
    /// <param name="issue">The issue.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The new issue, or the existing one it was added to as a comment, or a failure whose <see cref="Error.Code"/> is
    /// one of <see cref="GitHubIssues.ErrorCodes"/>.
    /// </returns>
    /// <exception cref="InvalidOperationException">There's no token.</exception>
    /// <exception cref="ArgumentException">The issue's title is blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public Task<Result<GitHubIssueReference>> CreateAsync(GitHubIssue issue,
        CancellationToken cancellationToken = default) =>
        GitHubIssues.CreateAsync(this.Repository, this.RequireToken(), this.WithReporter(issue), this.HttpClient,
            cancellationToken);

    /// <summary>
    /// Creates the issue through the API, blocking until it's done. See <see cref="GitHubIssues.Create"/> for where the
    /// synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="CreateAsync" path="/param"/>
    /// <inheritdoc cref="CreateAsync" path="/returns"/>
    /// <inheritdoc cref="CreateAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><see cref="HttpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public Result<GitHubIssueReference> Create(GitHubIssue issue, CancellationToken cancellationToken = default) =>
        GitHubIssues.Create(this.Repository, this.RequireToken(), this.WithReporter(issue), this.HttpClient,
            cancellationToken);

    /// <summary>
    /// Creates the issue through the API when it can, and otherwise opens the filled-in new-issue form with
    /// <see cref="OpenUrl"/>. See <see cref="GitHubIssues.CreateOrOpenAsync"/>.
    /// </summary>
    /// <param name="issue">The issue.</param>
    /// <param name="cancellationToken">Cancels the API request.</param>
    /// <returns>
    /// Which way the issue went, or a failure whose <see cref="Error.Code"/> is one of
    /// <see cref="GitHubIssues.ErrorCodes"/>.
    /// </returns>
    /// <exception cref="ArgumentException">The issue's title is blank.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public Task<Result<GitHubIssueSubmission>> CreateOrOpenAsync(GitHubIssue issue,
        CancellationToken cancellationToken = default) =>
        GitHubIssues.CreateOrOpenAsync(this.Repository, this._token, this.WithReporter(issue), this.HttpClient,
            this.OpenUrl, this.BaseAddress, cancellationToken);

    /// <summary>
    /// Creates the issue through the API when it can, and otherwise opens the filled-in new-issue form, blocking until
    /// it's done. See <see cref="GitHubIssues.Create"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="CreateOrOpenAsync" path="/param"/>
    /// <inheritdoc cref="CreateOrOpenAsync" path="/returns"/>
    /// <inheritdoc cref="CreateOrOpenAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><see cref="HttpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public Result<GitHubIssueSubmission> CreateOrOpen(GitHubIssue issue, CancellationToken cancellationToken = default) =>
        GitHubIssues.CreateOrOpen(this.Repository, this._token, this.WithReporter(issue), this.HttpClient,
            this.OpenUrl, this.BaseAddress, cancellationToken);

    /// <summary>The new-issue form's URL, filled in. See <see cref="GitHubIssues.GetNewIssueUrl"/>.</summary>
    /// <param name="issue">The issue.</param>
    /// <exception cref="ArgumentException">The issue's title is blank.</exception>
    public string GetNewIssueUrl(GitHubIssue issue) =>
        GitHubIssues.GetNewIssueUrl(this.Repository, this.WithReporter(issue), this.BaseAddress);

    /// <summary>
    /// Opens <see cref="GetNewIssueUrl"/> with <see cref="OpenUrl"/>, or <see cref="UrlLauncher.Open"/> if that's
    /// null, for the user to review and submit. Needs no token.
    /// </summary>
    /// <param name="issue">The issue.</param>
    /// <exception cref="ArgumentException">The issue's title is blank.</exception>
    /// <exception cref="PlatformNotSupportedException">
    /// <see cref="OpenUrl"/> is null and <see cref="UrlLauncher.Open"/> isn't supported here (Android, watchOS, WASI).
    /// </exception>
    public void OpenInBrowser(GitHubIssue issue) => (this.OpenUrl ?? UrlLauncher.Open)(this.GetNewIssueUrl(issue));

    private string RequireToken() => this._token ?? throw new InvalidOperationException(
        "This GitHubIssueClient has no token, so it can't use the API. Use CreateOrOpenAsync or OpenInBrowser.");

    private GitHubIssue WithReporter(GitHubIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return issue.Reporter is null && this.Reporter is not null ? issue with { Reporter = this.Reporter } : issue;
    }
}
