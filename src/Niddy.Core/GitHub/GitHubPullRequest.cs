using System.Text.Json;
using System.Text.Json.Nodes;

namespace Niddy.Core.GitHub;

/// <summary>A GitHub pull request, as <see cref="GitHubPullRequests"/> reads it.</summary>
public sealed record GitHubPullRequest
{
    /// <summary>The pull request number, e.g. <c>42</c> for <c>#42</c>.</summary>
    public required int Number { get; init; }

    /// <summary>The title.</summary>
    public required string Title { get; init; }

    /// <summary>The description, in Markdown, or null if it's empty.</summary>
    public string? Body { get; init; }

    /// <summary>Open, closed without merging, or merged.</summary>
    public GitHubItemState State { get; init; }

    /// <summary>Whether it's a draft.</summary>
    public bool IsDraft { get; init; }

    /// <summary>The pull request's page on GitHub.</summary>
    public Uri? Url { get; init; }

    /// <summary>The repository it's in, as <c>owner/name</c>.</summary>
    public string? Repository { get; init; }

    /// <summary>The login of whoever opened it, or null for a deleted account.</summary>
    public string? Author { get; init; }

    /// <summary>The names of its labels.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>The logins it's assigned to.</summary>
    public IReadOnlyList<string> Assignees { get; init; } = [];

    /// <summary>The logins whose review is requested and still pending. Requested teams aren't included.</summary>
    public IReadOnlyList<string> RequestedReviewers { get; init; } = [];

    /// <summary>Its milestone's title, or null.</summary>
    public string? Milestone { get; init; }

    /// <summary>The branch with the changes, e.g. <c>fix-crash</c>.</summary>
    public string? HeadBranch { get; init; }

    /// <summary>The commit the branch is at.</summary>
    public string? HeadSha { get; init; }

    /// <summary>
    /// The repository the branch is in, as <c>owner/name</c>: <see cref="Repository"/>, or a fork. Null if the fork
    /// was deleted.
    /// </summary>
    public string? HeadRepository { get; init; }

    /// <summary>The branch it merges into, e.g. <c>main</c>.</summary>
    public string? BaseBranch { get; init; }

    /// <summary>When it was opened.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When it was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>When it was closed or merged, or null if it's open.</summary>
    public DateTimeOffset? ClosedAt { get; init; }

    /// <summary>When it was merged, or null if it wasn't.</summary>
    public DateTimeOffset? MergedAt { get; init; }

    /// <summary>
    /// Whether it can be merged without conflicts. Null in lists, and from <see cref="GitHubPullRequests.GetAsync"/>
    /// while GitHub is still working it out, which it starts doing on the first request.
    /// </summary>
    public bool? IsMergeable { get; init; }

    /// <summary>How many conversation comments it has. Null in lists; only <see cref="GitHubPullRequests.GetAsync"/> reads it.</summary>
    public int? CommentCount { get; init; }

    /// <summary>How many review comments (on lines of the diff) it has. Null in lists.</summary>
    public int? ReviewCommentCount { get; init; }

    /// <summary>How many commits it has. Null in lists.</summary>
    public int? CommitCount { get; init; }

    /// <summary>How many lines it adds. Null in lists.</summary>
    public int? Additions { get; init; }

    /// <summary>How many lines it removes. Null in lists.</summary>
    public int? Deletions { get; init; }

    /// <summary>How many files it changes. Null in lists.</summary>
    public int? ChangedFiles { get; init; }

    /// <summary>Reads a pull request from the REST API's JSON. Throws if it has no number or title.</summary>
    internal static GitHubPullRequest FromJson(JsonNode json)
    {
        DateTimeOffset? mergedAt = GitHubReader.Date(json, "merged_at");
        JsonNode? head = json["head"];
        return new GitHubPullRequest
        {
            Number = GitHubReader.Int(json, "number") ?? throw new JsonException("The pull request has no number."),
            Title = json["title"]?.GetValue<string>() ?? throw new JsonException("The pull request has no title."),
            Body = GitHubReader.String(json, "body"),
            State = mergedAt is not null || GitHubReader.Bool(json, "merged") ? GitHubItemState.Merged
                : GitHubReader.String(json, "state") == "closed" ? GitHubItemState.Closed
                : GitHubItemState.Open,
            IsDraft = GitHubReader.Bool(json, "draft"),
            Url = GitHubReader.Url(json, "html_url"),
            Repository = GitHubReader.String(json["base"]?["repo"], "full_name"),
            Author = GitHubReader.String(json["user"], "login"),
            Labels = GitHubReader.Labels(json["labels"]),
            Assignees = GitHubReader.Logins(json["assignees"]),
            RequestedReviewers = GitHubReader.Logins(json["requested_reviewers"]),
            Milestone = GitHubReader.String(json["milestone"], "title"),
            HeadBranch = GitHubReader.String(head, "ref"),
            HeadSha = GitHubReader.String(head, "sha"),
            HeadRepository = GitHubReader.String(head?["repo"], "full_name"),
            BaseBranch = GitHubReader.String(json["base"], "ref"),
            CreatedAt = GitHubReader.Date(json, "created_at"),
            UpdatedAt = GitHubReader.Date(json, "updated_at"),
            ClosedAt = GitHubReader.Date(json, "closed_at"),
            MergedAt = mergedAt,
            IsMergeable = json["mergeable"] is JsonValue mergeable && mergeable.TryGetValue(out bool canMerge) ? canMerge : null,
            CommentCount = GitHubReader.Int(json, "comments"),
            ReviewCommentCount = GitHubReader.Int(json, "review_comments"),
            CommitCount = GitHubReader.Int(json, "commits"),
            Additions = GitHubReader.Int(json, "additions"),
            Deletions = GitHubReader.Int(json, "deletions"),
            ChangedFiles = GitHubReader.Int(json, "changed_files"),
        };
    }
}
