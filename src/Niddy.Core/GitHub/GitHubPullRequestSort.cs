namespace Niddy.Core.GitHub;

/// <summary>What <see cref="GitHubPullRequestFilter.Sort"/> sorts pull requests by.</summary>
public enum GitHubPullRequestSort
{
    /// <summary>When they were opened.</summary>
    Created,

    /// <summary>When they were last updated.</summary>
    Updated,

    /// <summary>How many comments they have.</summary>
    Popularity,

    /// <summary>How long they've been open, ignoring those not updated in the last month.</summary>
    LongRunning,
}
