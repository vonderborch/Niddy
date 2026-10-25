using System.Text.Json;
using System.Text.Json.Nodes;

namespace Niddy.Core.GitHub;

/// <summary>A field on a GitHub project, e.g. Status or Priority, as <see cref="GitHubProjects.ListFieldsAsync"/> reads it.</summary>
public sealed record GitHubProjectField
{
    /// <summary>GitHub's node ID for the field.</summary>
    public required string Id { get; init; }

    /// <summary>The field's name, e.g. <c>Status</c>.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// Its type as GitHub names it, e.g. <c>TEXT</c>, <c>NUMBER</c>, <c>DATE</c>, <c>SINGLE_SELECT</c>,
    /// <c>ITERATION</c>, or a built-in one such as <c>TITLE</c>, <c>ASSIGNEES</c> or <c>LABELS</c>. A string, since
    /// GitHub adds types.
    /// </summary>
    public string DataType { get; init; } = string.Empty;

    /// <summary>
    /// A single-select field's option names, or an iteration field's iteration titles (current and upcoming, then
    /// completed), in the project's order. Empty for other fields.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = [];

    /// <summary>The fields <see cref="FromJson"/> reads, as a GraphQL selection on <c>ProjectV2FieldConfiguration</c>.</summary>
    internal const string Selection = """
        ... on ProjectV2FieldCommon { id name dataType }
        ... on ProjectV2SingleSelectField { options { name } }
        ... on ProjectV2IterationField { configuration { iterations { title } completedIterations { title } } }
        """;

    /// <summary>Reads a field from the GraphQL API's JSON. Throws if it has no ID or name.</summary>
    internal static GitHubProjectField FromJson(JsonNode json)
    {
        JsonNode? iterations = json["configuration"];
        return new GitHubProjectField
        {
            Id = GitHubReader.String(json, "id") ?? throw new JsonException("The project field has no id."),
            Name = GitHubReader.String(json, "name") ?? throw new JsonException("The project field has no name."),
            DataType = GitHubReader.String(json, "dataType") ?? string.Empty,
            Options =
            [
                .. Names(json["options"], "name"),
                .. Names(iterations?["iterations"], "title"),
                .. Names(iterations?["completedIterations"], "title"),
            ],
        };
    }

    private static IEnumerable<string> Names(JsonNode? array, string property) =>
        array is JsonArray items ? items.Select(item => GitHubReader.String(item, property)).OfType<string>() : [];
}
