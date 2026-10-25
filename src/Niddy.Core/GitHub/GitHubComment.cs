using System.Text.Json;
using System.Text.Json.Nodes;

namespace Niddy.Core.GitHub;

/// <summary>A comment on an issue or pull request, as <see cref="GitHubIssues.ListCommentsAsync"/> reads it.</summary>
public sealed record GitHubComment
{
    /// <summary>GitHub's ID for the comment.</summary>
    public required long Id { get; init; }

    /// <summary>The comment, in Markdown.</summary>
    public required string Body { get; init; }

    /// <summary>The login of whoever wrote it, or null for a deleted account.</summary>
    public string? Author { get; init; }

    /// <summary>The comment's link on GitHub.</summary>
    public Uri? Url { get; init; }

    /// <summary>When it was written.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When it was last edited, or when it was written if it never was.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>Reads a comment from the REST API's JSON. Throws if it has no ID.</summary>
    internal static GitHubComment FromJson(JsonNode json) => new()
    {
        Id = json["id"] is JsonValue id && id.TryGetValue(out long value) ? value : throw new JsonException("The comment has no id."),
        Body = GitHubReader.String(json, "body") ?? string.Empty,
        Author = GitHubReader.String(json["user"], "login"),
        Url = GitHubReader.Url(json, "html_url"),
        CreatedAt = GitHubReader.Date(json, "created_at"),
        UpdatedAt = GitHubReader.Date(json, "updated_at"),
    };
}
