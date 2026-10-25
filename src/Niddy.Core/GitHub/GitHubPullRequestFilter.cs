using System.Text;

namespace Niddy.Core.GitHub;

/// <summary>Which pull requests <see cref="GitHubPullRequests.ListAsync"/> returns, and in what order.</summary>
public sealed record GitHubPullRequestFilter
{
    internal static readonly GitHubPullRequestFilter Default = new();

    /// <summary>Open, closed (merged included) or all. Defaults to open.</summary>
    public GitHubStateFilter State { get; init; } = GitHubStateFilter.Open;

    /// <summary>
    /// Only pull requests from this branch, as <c>owner:branch</c>, e.g. <c>octocat:fix-crash</c>. A bare branch name
    /// matches nothing; use <see cref="GitHubPullRequest.HeadBranch"/> on the results instead.
    /// </summary>
    public string? Head { get; init; }

    /// <summary>Only pull requests into this branch, e.g. <c>main</c>.</summary>
    public string? Base { get; init; }

    /// <summary>What to sort by. Defaults to when they were created.</summary>
    public GitHubPullRequestSort Sort { get; init; } = GitHubPullRequestSort.Created;

    /// <summary>Whether to sort oldest or least first. Defaults to false: newest or most first.</summary>
    public bool Ascending { get; init; }

    /// <summary>The query string, starting with <c>?</c>.</summary>
    internal string ToQuery()
    {
        StringBuilder query = new($"?state={GitHubIssueFilter.StateQuery(this.State)}");
        if (!string.IsNullOrWhiteSpace(this.Head))
        {
            query.Append("&head=").Append(Uri.EscapeDataString(this.Head.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(this.Base))
        {
            query.Append("&base=").Append(Uri.EscapeDataString(this.Base.Trim()));
        }

        query.Append("&sort=").Append(this.Sort switch
        {
            GitHubPullRequestSort.Updated => "updated",
            GitHubPullRequestSort.Popularity => "popularity",
            GitHubPullRequestSort.LongRunning => "long-running",
            _ => "created",
        });
        query.Append("&direction=").Append(this.Ascending ? "asc" : "desc");
        return query.ToString();
    }
}
