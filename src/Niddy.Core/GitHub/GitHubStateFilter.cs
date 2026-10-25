namespace Niddy.Core.GitHub;

/// <summary>Which issues or pull requests a list returns, by state.</summary>
public enum GitHubStateFilter
{
    /// <summary>Only open ones.</summary>
    Open,

    /// <summary>Only closed ones, merged pull requests included.</summary>
    Closed,

    /// <summary>Open and closed.</summary>
    All,
}
