namespace Niddy.Core.GitHub;

/// <summary>
/// Who is reporting a <see cref="GitHubIssue"/>. Every part is optional; whatever is set shows in a
/// "Reported by" line in the issue body.
/// </summary>
/// <param name="Name">The person's name.</param>
/// <param name="Email">
/// An email address to reach them at. On a public repository the issue, and so the address, is public.
/// </param>
/// <param name="GitHubUsername">
/// Their GitHub username, with or without a leading <c>@</c>. It's written as an <c>@mention</c>, so GitHub
/// notifies them.
/// </param>
public sealed record GitHubIssueReporter(string? Name = null, string? Email = null, string? GitHubUsername = null)
{
    /// <summary>The set parts joined for display, e.g. <c>Jane Doe · @jane · jane@example.com</c>, or null if none are set.</summary>
    internal string? Format()
    {
        List<string> parts = [];
        if (!string.IsNullOrWhiteSpace(this.Name))
        {
            parts.Add(this.Name.Trim());
        }

        if (!string.IsNullOrWhiteSpace(this.GitHubUsername))
        {
            parts.Add("@" + this.GitHubUsername.Trim().TrimStart('@'));
        }

        if (!string.IsNullOrWhiteSpace(this.Email))
        {
            parts.Add(this.Email.Trim());
        }

        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}
