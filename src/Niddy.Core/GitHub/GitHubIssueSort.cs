namespace Niddy.Core.GitHub;

/// <summary>What <see cref="GitHubIssueFilter.Sort"/> sorts issues by.</summary>
public enum GitHubIssueSort
{
    /// <summary>When they were opened.</summary>
    Created,

    /// <summary>When they were last updated.</summary>
    Updated,

    /// <summary>How many comments they have.</summary>
    Comments,
}
