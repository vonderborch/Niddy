namespace Niddy.Core.GitHub;

/// <summary>
/// An issue that <see cref="GitHubIssues.CreateAsync"/> created, or, for an issue with a
/// <see cref="GitHubIssue.Fingerprint"/>, the open issue it found and added the report to as a comment.
/// </summary>
/// <param name="Number">The issue number, e.g. <c>42</c> for <c>#42</c>.</param>
/// <param name="Url">The issue's page on GitHub, to show or open for the user.</param>
public sealed record GitHubIssueReference(int Number, Uri Url)
{
    /// <summary>
    /// The comment the report was added as, when the issue already existed; null when the issue is new.
    /// </summary>
    public Uri? CommentUrl { get; init; }

    /// <summary>Whether the issue already existed and the report was added to it as a comment.</summary>
    public bool AddedAsComment => this.CommentUrl is not null;
}
