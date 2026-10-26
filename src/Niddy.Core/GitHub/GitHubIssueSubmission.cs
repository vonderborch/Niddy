using Niddy.Core.Results;

namespace Niddy.Core.GitHub;

/// <summary>Which way
/// <see cref="GitHubIssues.CreateOrOpenAsync(string, string?, GitHubIssue, HttpClient?, Action{string}?, Uri?, Uri?, CancellationToken)"/>
/// sent an issue.</summary>
public sealed record GitHubIssueSubmission
{
    private GitHubIssueSubmission(GitHubIssueReference? issue, string? newIssueUrl, Error? apiError)
    {
        this.Issue = issue;
        this.NewIssueUrl = newIssueUrl;
        this.ApiError = apiError;
    }

    /// <summary>The issue created through the API, or null if the form was opened in the browser instead.</summary>
    public GitHubIssueReference? Issue { get; }

    /// <summary>The new-issue form's URL, if it was opened in the browser.</summary>
    public string? NewIssueUrl { get; }

    /// <summary>Why the API wasn't used: its failure, or null if there was no token or the issue was created.</summary>
    public Error? ApiError { get; }

    /// <summary>
    /// Whether the form was opened in the browser. The issue only exists once the user submits it, which the app
    /// can't tell.
    /// </summary>
    public bool OpenedInBrowser => this.Issue is null;

    internal static GitHubIssueSubmission Created(GitHubIssueReference issue) => new(issue, null, null);

    internal static GitHubIssueSubmission Opened(string newIssueUrl, Error? apiError) => new(null, newIssueUrl, apiError);
}
