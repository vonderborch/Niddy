namespace Niddy.Core.GitHub;

/// <summary>What a <see cref="GitHubProjectItem"/> is.</summary>
public enum GitHubProjectItemType
{
    /// <summary>An issue.</summary>
    Issue,

    /// <summary>A pull request.</summary>
    PullRequest,

    /// <summary>A draft issue, which lives only in the project.</summary>
    DraftIssue,

    /// <summary>An issue or pull request the token can't see, so it has no title, number or link.</summary>
    Redacted,
}
