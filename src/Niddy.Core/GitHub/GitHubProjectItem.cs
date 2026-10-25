using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Niddy.Core.GitHub;

/// <summary>
/// An item on a GitHub project, as <see cref="GitHubProjects.ListItemsAsync"/> reads it: an issue, a pull request or
/// a draft issue, with the values of the project's fields, e.g. its <see cref="Status"/>.
/// </summary>
public sealed record GitHubProjectItem
{
    /// <summary>GitHub's node ID for the item. It's the item's, not the issue's.</summary>
    public required string Id { get; init; }

    /// <summary>Whether it's an issue, a pull request, a draft or something the token can't see.</summary>
    public GitHubProjectItemType Type { get; init; }

    /// <summary>The title, or null for a <see cref="GitHubProjectItemType.Redacted"/> item.</summary>
    public string? Title { get; init; }

    /// <summary>A draft issue's body, in Markdown, or null. Issues' and pull requests' bodies aren't read.</summary>
    public string? Body { get; init; }

    /// <summary>The issue or pull request number, or null for a draft.</summary>
    public int? Number { get; init; }

    /// <summary>The issue's or pull request's page on GitHub, or null for a draft.</summary>
    public Uri? Url { get; init; }

    /// <summary>The repository the issue or pull request is in, as <c>owner/name</c>, or null for a draft.</summary>
    public string? Repository { get; init; }

    /// <summary>Whether the issue or pull request is open, closed or merged, or null for a draft.</summary>
    public GitHubItemState? State { get; init; }

    /// <summary>Whether the item is archived in the project. Check it to leave archived items out.</summary>
    public bool IsArchived { get; init; }

    /// <summary>
    /// The item's field values by field name, ignoring case, for the fields that have one: text, numbers (formatted
    /// with the invariant culture), dates (<c>yyyy-MM-dd</c>), single-select options and iteration titles. The
    /// built-in Title field is here too. Field types without a single value, e.g. assignees and labels, aren't.
    /// </summary>
    public IReadOnlyDictionary<string, string> Fields { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Status field's value, e.g. <c>Todo</c> or <c>In Progress</c>, or null if it has none.</summary>
    public string? Status => this.GetField("Status");

    /// <summary>A field's value by its name, ignoring case, or null if the item has none for it.</summary>
    /// <param name="name">The field's name as the project shows it, e.g. <c>Priority</c>.</param>
    /// <returns>The value, formatted as <see cref="Fields"/> says, or null.</returns>
    public string? GetField(string name) => this.Fields.GetValueOrDefault(name);

    /// <summary>The fields <see cref="FromJson"/> reads, as a GraphQL fragment on <c>ProjectV2Item</c>.</summary>
    /// <remarks>
    /// <c>state</c> is aliased because issues' and pull requests' are different enums, which GraphQL won't merge.
    /// </remarks>
    internal const string Fragment = """
        fragment Item on ProjectV2Item {
          id type isArchived
          content {
            ... on DraftIssue { title body }
            ... on Issue { number title url issueState: state repository { nameWithOwner } }
            ... on PullRequest { number title url pullRequestState: state repository { nameWithOwner } }
          }
          fieldValues(first: 50) {
            nodes {
              ... on ProjectV2ItemFieldTextValue { text field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldNumberValue { number field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldDateValue { date field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldSingleSelectValue { name field { ... on ProjectV2FieldCommon { name } } }
              ... on ProjectV2ItemFieldIterationValue { title field { ... on ProjectV2FieldCommon { name } } }
            }
          }
        }
        """;

    /// <summary>Reads an item from the GraphQL API's JSON. Throws if it has no ID.</summary>
    internal static GitHubProjectItem FromJson(JsonNode json)
    {
        JsonNode? content = json["content"];
        string? state = GitHubReader.String(content, "issueState") ?? GitHubReader.String(content, "pullRequestState");
        return new GitHubProjectItem
        {
            Id = GitHubReader.String(json, "id") ?? throw new JsonException("The project item has no id."),
            Type = GitHubReader.String(json, "type") switch
            {
                "ISSUE" => GitHubProjectItemType.Issue,
                "PULL_REQUEST" => GitHubProjectItemType.PullRequest,
                "DRAFT_ISSUE" => GitHubProjectItemType.DraftIssue,
                _ => GitHubProjectItemType.Redacted,
            },
            Title = GitHubReader.String(content, "title"),
            Body = GitHubReader.String(content, "body"),
            Number = GitHubReader.Int(content, "number"),
            Url = GitHubReader.Url(content, "url"),
            Repository = GitHubReader.String(content?["repository"], "nameWithOwner"),
            State = state switch
            {
                "OPEN" => GitHubItemState.Open,
                "CLOSED" => GitHubItemState.Closed,
                "MERGED" => GitHubItemState.Merged,
                _ => null,
            },
            IsArchived = GitHubReader.Bool(json, "isArchived"),
            Fields = ReadFields(json["fieldValues"]?["nodes"]),
        };
    }

    private static Dictionary<string, string> ReadFields(JsonNode? values)
    {
        Dictionary<string, string> fields = new(StringComparer.OrdinalIgnoreCase);
        if (values is not JsonArray array)
        {
            return fields;
        }

        foreach (JsonNode? value in array)
        {
            // Values of field types the query doesn't ask about come back as empty objects.
            if (GitHubReader.String(value?["field"], "name") is not { } name)
            {
                continue;
            }

            string? text = value!["number"] is JsonValue number && number.TryGetValue(out double amount)
                ? amount.ToString(CultureInfo.InvariantCulture)
                : GitHubReader.String(value, "text") ?? GitHubReader.String(value, "date")
                ?? GitHubReader.String(value, "name") ?? GitHubReader.String(value, "title");
            if (text is not null)
            {
                fields[name] = text;
            }
        }

        return fields;
    }
}
