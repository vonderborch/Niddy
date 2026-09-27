using System.Text.Json;
using System.Text.Json.Serialization;
using Niddy.IO;

namespace Niddy.Helpers;

/// <summary>
/// Helper methods for JSON serialization and deserialization.
/// </summary>
public static class JsonHelpers
{
    /// <summary>
    /// Default JSON serializer options: camelCase, indented, enums as strings, fields included.
    /// </summary>
    public static readonly JsonSerializerOptions DefaultOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        IncludeFields = true,
    };

    /// <summary>
    /// Deserializes a JSON file to the specified type.
    /// Returns default if the file does not exist or is empty.
    /// </summary>
    public static TValue? DeserializeFromFile<TValue>(string path, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(path))
            return default;

        var rawContents = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(rawContents))
            return default;

        return DeserializeString<TValue>(rawContents, options);
    }

    /// <summary>
    /// Asynchronously deserializes a JSON file to the specified type.
    /// Returns default if the file does not exist or is empty.
    /// </summary>
    public static async Task<TValue?> DeserializeFromFileAsync<TValue>(
        string path,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
            return default;

        var rawContents = await File.ReadAllTextAsync(path, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawContents))
            return default;

        return DeserializeString<TValue>(rawContents, options);
    }

    /// <summary>
    /// Deserializes a JSON string to the specified type.
    /// Returns default on failure when <paramref name="throwOnError"/> is false.
    /// </summary>
    public static TValue? DeserializeString<TValue>(
        string contents,
        JsonSerializerOptions? options = null,
        bool throwOnError = false)
    {
        if (string.IsNullOrWhiteSpace(contents))
            return default;

        var actualOptions = options ?? DefaultOptions;

        try
        {
            return JsonSerializer.Deserialize<TValue>(contents, actualOptions);
        }
        catch
        {
            if (throwOnError)
                throw;

            return default;
        }
    }

    /// <summary>
    /// Serializes an object to a JSON file. Creates the directory if it does not exist. The file is written
    /// atomically (see <see cref="AtomicFile"/>), so it is never left half-written.
    /// </summary>
    public static void SerializeToFile<TValue>(string path, TValue value, JsonSerializerOptions? options = null) =>
        AtomicFile.WriteAllText(path, SerializeToString(value, options));

    /// <summary>
    /// Asynchronously serializes an object to a JSON file. Creates the directory if it does not exist. The file is
    /// written atomically (see <see cref="AtomicFile"/>), so it is never left half-written.
    /// </summary>
    public static Task SerializeToFileAsync<TValue>(
        string path,
        TValue value,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        AtomicFile.WriteAllTextAsync(path, SerializeToString(value, options), cancellationToken: cancellationToken);

    /// <summary>
    /// Serializes an object to a JSON string.
    /// </summary>
    public static string SerializeToString<TValue>(TValue value, JsonSerializerOptions? options = null)
        => JsonSerializer.Serialize(value, options ?? DefaultOptions);
}
