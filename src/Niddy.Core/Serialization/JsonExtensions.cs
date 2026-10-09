using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Niddy.Core.Serialization;

/// <summary>
/// Extensions on <see cref="JsonSerializerOptions"/>, <see cref="JsonSerializer"/> and <see cref="JsonObject"/>:
/// Niddy's default options (<c>JsonSerializerOptions.Niddy</c>), a deserialize that returns default instead of throwing
/// (<c>JsonSerializer.DeserializeOrDefault</c>), and <c>RenameProperty</c> for upgrading JSON.
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

    extension(JsonObject json)
    {
        /// <summary>
        /// Renames a property, keeping its value and its place in the object. If <paramref name="newName"/> is already
        /// there, it's replaced. Handy in a <see cref="Niddy.Core.Settings.SettingsMigrations"/> upgrade step.
        /// </summary>
        /// <param name="oldName">The property's current name.</param>
        /// <param name="newName">Its new name.</param>
        /// <returns>True if the property was renamed; false if there's no <paramref name="oldName"/> property.</returns>
        public bool RenameProperty(string oldName, string newName)
        {
            ArgumentNullException.ThrowIfNull(oldName);
            ArgumentNullException.ThrowIfNull(newName);

            int index = json.IndexOf(oldName);
            if (index < 0)
            {
                return false;
            }

            if (oldName == newName)
            {
                return true;
            }

            JsonNode? value = json.GetAt(index).Value;
            json.RemoveAt(index);
            int existing = json.IndexOf(newName);
            if (existing >= 0)
            {
                json.RemoveAt(existing);
                if (existing < index)
                {
                    index--;
                }
            }

            json.Insert(index, newName, value);
            return true;
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
