namespace Niddy.Core.GitHub;

/// <summary>An issue that <see cref="GitHubIssues.CreateAsync"/> created.</summary>
/// <param name="Number">The issue number, e.g. <c>42</c> for <c>#42</c>.</param>
/// <param name="Url">The issue's page on GitHub, to show or open for the user.</param>
public sealed record GitHubIssueReference(int Number, Uri Url);
