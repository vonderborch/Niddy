using System.Text.Json;
using System.Text.Json.Serialization;

namespace Niddy.Core.Serialization;

/// <summary>
/// Extensions on <see cref="JsonSerializerOptions"/> and <see cref="JsonSerializer"/>: Niddy's default options
/// (<c>JsonSerializerOptions.Niddy</c>) and a deserialize that returns default instead of throwing
/// (<c>JsonSerializer.DeserializeOrDefault</c>).
/// </summary>
public static class JsonExtensions
{
    private static readonly JsonSerializerOptions NiddyOptions = CreateNiddyOptions();

    extension(JsonSerializerOptions)
    {
        /// <summary>
        /// Niddy's default options: camelCase, indented, enums as strings, fields included. One shared, read-only
        /// instance. To change something, copy it: <c>new JsonSerializerOptions(JsonSerializerOptions.Niddy)</c>.
        /// </summary>
        public static JsonSerializerOptions Niddy => NiddyOptions;
    }

    extension(JsonSerializer)
    {
        /// <summary>
        /// Deserializes <paramref name="json"/>, returning default instead of throwing when it's null, empty,
        /// whitespace or not valid for <typeparamref name="TValue"/>.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="options">The serializer options. Null is <c>JsonSerializerOptions.Niddy</c>.</param>
        public static TValue? DeserializeOrDefault<TValue>(string? json, JsonSerializerOptions? options = null)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                return JsonSerializer.Deserialize<TValue>(json, options ?? NiddyOptions);
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException or ArgumentException)
            {
                return default;
            }
        }
    }

    private static JsonSerializerOptions CreateNiddyOptions()
    {
        JsonSerializerOptions options = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
            IncludeFields = true,
        };
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
