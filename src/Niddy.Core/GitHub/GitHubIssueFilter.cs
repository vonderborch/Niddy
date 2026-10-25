using System.Globalization;
using System.Text;

namespace Niddy.Core.GitHub;

/// <summary>Which issues <see cref="GitHubIssues.ListAsync"/> returns, and in what order.</summary>
public sealed record GitHubIssueFilter
{
    internal static readonly GitHubIssueFilter Default = new();

    /// <summary>Open, closed or all. Defaults to open.</summary>
    public GitHubStateFilter State { get; init; } = GitHubStateFilter.Open;

    /// <summary>Only issues with all of these labels. Empty, the default, doesn't filter.</summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>Only issues assigned to this login; <c>none</c> for unassigned ones, <c>*</c> for assigned ones.</summary>
    public string? Assignee { get; init; }

    /// <summary>Only issues opened by this login.</summary>
    public string? Creator { get; init; }

    /// <summary>Only issues that mention this login.</summary>
    public string? Mentioned { get; init; }

    /// <summary>
    /// Only issues in this milestone, by its number (not its title); <c>none</c> for issues without one, <c>*</c> for
    /// issues with one.
    /// </summary>
    public string? Milestone { get; init; }

    /// <summary>Only issues updated at or after this time.</summary>
    public DateTimeOffset? Since { get; init; }

    /// <summary>What to sort by. Defaults to when they were created.</summary>
    public GitHubIssueSort Sort { get; init; } = GitHubIssueSort.Created;

    /// <summary>Whether to sort oldest or fewest first. Defaults to false: newest or most first.</summary>
    public bool Ascending { get; init; }

    /// <summary>
    /// Whether to include pull requests, which GitHub's issues list also returns. Defaults to false. They come back as
    /// <see cref="GitHubIssueInfo"/> with <see cref="GitHubIssueInfo.IsPullRequest"/> set; use
    /// <see cref="GitHubPullRequests.ListAsync"/> for their branches and drafts.
    /// </summary>
    public bool IncludePullRequests { get; init; }

    /// <summary>The query string, starting with <c>?</c>.</summary>
    internal string ToQuery()
    {
        StringBuilder query = new($"?state={StateQuery(this.State)}");
        if (this.Labels.Count > 0)
        {
            Add("labels", string.Join(",", this.Labels.Select(label => label.Trim())));
        }

        Add("assignee", this.Assignee);
        Add("creator", this.Creator);
        Add("mentioned", this.Mentioned);
        Add("milestone", this.Milestone);
        Add("since", this.Since?.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        query.Append("&sort=").Append(this.Sort switch
        {
            GitHubIssueSort.Updated => "updated",
            GitHubIssueSort.Comments => "comments",
            _ => "created",
        });
        query.Append("&direction=").Append(this.Ascending ? "asc" : "desc");
        return query.ToString();

        void Add(string name, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                query.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value.Trim()));
            }
        }
    }

    internal static string StateQuery(GitHubStateFilter state) => state switch
    {
        GitHubStateFilter.Closed => "closed",
        GitHubStateFilter.All => "all",
        _ => "open",
    };
}
