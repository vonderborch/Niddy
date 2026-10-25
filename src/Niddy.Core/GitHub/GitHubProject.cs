using System.Text.Json;
using System.Text.Json.Nodes;

namespace Niddy.Core.GitHub;

/// <summary>A GitHub project (the current kind, Projects v2), as <see cref="GitHubProjects"/> reads it.</summary>
public sealed record GitHubProject
{
    /// <summary>GitHub's node ID for the project, e.g. <c>PVT_kwDOAB…</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The project number: the <c>5</c> in <c>github.com/orgs/acme/projects/5</c>.</summary>
    public required int Number { get; init; }

    /// <summary>The title.</summary>
    public required string Title { get; init; }

    /// <summary>The short description, or null if it has none.</summary>
    public string? Description { get; init; }

    /// <summary>The project's page on GitHub.</summary>
    public Uri? Url { get; init; }

    /// <summary>The login of the user or organization that owns it.</summary>
    public string? Owner { get; init; }

    /// <summary>Whether it's closed.</summary>
    public bool IsClosed { get; init; }

    /// <summary>Whether anyone can see it.</summary>
    public bool IsPublic { get; init; }

    /// <summary>How many items it has, archived ones included.</summary>
    public int ItemCount { get; init; }

    /// <summary>When it was created.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>When it was last updated.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    /// <summary>The fields <see cref="FromJson"/> reads, as a GraphQL fragment on <c>ProjectV2</c>.</summary>
    internal const string Fragment = """
        fragment Project on ProjectV2 {
          id number title shortDescription url closed public createdAt updatedAt
          owner { ... on RepositoryOwner { login } }
          items { totalCount }
        }
        """;

    /// <summary>Reads a project from the GraphQL API's JSON. Throws if it has no ID, number or title.</summary>
    internal static GitHubProject FromJson(JsonNode json) => new()
    {
        Id = GitHubReader.String(json, "id") ?? throw new JsonException("The project has no id."),
        Number = GitHubReader.Int(json, "number") ?? throw new JsonException("The project has no number."),
        Title = json["title"]?.GetValue<string>() ?? throw new JsonException("The project has no title."),
        Description = GitHubReader.String(json, "shortDescription"),
        Url = GitHubReader.Url(json, "url"),
        Owner = GitHubReader.String(json["owner"], "login"),
        IsClosed = GitHubReader.Bool(json, "closed"),
        IsPublic = GitHubReader.Bool(json, "public"),
        ItemCount = GitHubReader.Int(json["items"], "totalCount") ?? 0,
        CreatedAt = GitHubReader.Date(json, "createdAt"),
        UpdatedAt = GitHubReader.Date(json, "updatedAt"),
    };
}
