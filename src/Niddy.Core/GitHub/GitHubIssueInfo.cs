using System.Text.Json;
using System.Text.Json.Nodes;

namespace Niddy.Core.GitHub;

/// <summary>
/// An existing GitHub issue, as <see cref="GitHubIssues.ListAsync"/>, <see cref="GitHubIssues.GetAsync"/> and
/// <see cref="GitHubIssues.SearchAsync"/> read it. (<see cref="GitHubIssue"/> is the one for opening an issue.) It can
/// be a pull request, with <see cref="IsPullRequest"/> set, since GitHub treats pull requests as issues too.
/// </summary>
public sealed record GitHubIssueInfo
{
    /// <summary>The issue number, e.g. <c>42</c> for <c>#42</c>.</summary>
    public required int Number { get; init; }

    /// <summary>The title.</summary>
    public required string Title { get; init; }

    /// <summary>The body, in Markdown, or null if it's empty.</summary>
    public string? Body { get; init; }

    /// <summary>Open or closed; <see cref="GitHubItemState.Merged"/> for a merged pull request.</summary>
    public GitHubItemState State { get; init; }

    /// <summary>
    /// Why it's in its state, as GitHub says it, e.g. <c>completed</c>, <c>not_planned</c>, <c>duplicate</c> or
    /// <c>reopened</c>. Null if GitHub gave none.
    /// </summary>
    public string? StateReason { get; init; }

    /// <summary>Whether it's a pull request.</summary>
    public bool IsPullRequest { get; init; }

    /// <summary>The issue's page on GitHub.</summary>
    public Uri? Url { get; init; }

    /// <summary>
    /// The repository it's in, as <c>owner/name</c>. Useful for <see cref="GitHubIssues.SearchAsync"/> results, which
    /// can span repositories.
    /// </summary>
    public string? Repository { get; init; }

    /// <summary>The login of whoever opened it, or null for a deleted account.</summary>
    public string? Author { get; init; }

    /// <summary>The names of its labels.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>The logins it's assigned to.</summary>
    public IReadOnlyList<string> Assignees { get; init; } = [];

    /// <summary>Its milestone's title, or null.</summary>
    public string? Milestone { get; init; }

    /// <summary>How many comments it has.</summary>
    public int CommentCount { get; init; }

    /// <summary>Whether the conversation is locked.</summary>
    public bool IsLocked { get; init; }

    /// <summary>When it was opened.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When it was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>When it was closed, or null if it's open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>Reads an issue from the REST API's JSON. Throws if it has no number or title.</summary>
    internal static GitHubIssueInfo FromJson(JsonNode json)
    {
        JsonNode? pullRequest = json["pull_request"];
        bool merged = GitHubReader.Date(pullRequest, "merged_at") is not null;
        return new GitHubIssueInfo
        {
            Number = GitHubReader.Int(json, "number") ?? throw new JsonException("The issue has no number."),
            Title = json["title"]?.GetValue<string>() ?? throw new JsonException("The issue has no title."),
            Body = GitHubReader.String(json, "body"),
            State = merged ? GitHubItemState.Merged
                : GitHubReader.String(json, "state") == "closed" ? GitHubItemState.Closed
                : GitHubItemState.Open,
            StateReason = GitHubReader.String(json, "state_reason"),
            IsPullRequest = pullRequest is not null,
            Url = GitHubReader.Url(json, "html_url"),
            Repository = GitHubReader.RepositoryFromApiUrl(GitHubReader.String(json, "repository_url")),
            Author = GitHubReader.String(json["user"], "login"),
            Labels = GitHubReader.Labels(json["labels"]),
            Assignees = GitHubReader.Logins(json["assignees"]),
            Milestone = GitHubReader.String(json["milestone"], "title"),
            CommentCount = GitHubReader.Int(json, "comments") ?? 0,
            IsLocked = GitHubReader.Bool(json, "locked"),
            CreatedAt = GitHubReader.Date(json, "created_at"),
            UpdatedAt = GitHubReader.Date(json, "updated_at"),
            ClosedAt = GitHubReader.Date(json, "closed_at"),
        };
    }
}
