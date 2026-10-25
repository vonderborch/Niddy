namespace Niddy.Core.GitHub;

/// <summary>Whether an issue or pull request is open, closed, or (pull requests only) merged.</summary>
public enum GitHubItemState
{
    /// <summary>Open.</summary>
    Open,

    /// <summary>Closed. For a pull request, closed without being merged.</summary>
    Closed,

    /// <summary>A pull request that was merged, which also closes it.</summary>
    Merged,
}
