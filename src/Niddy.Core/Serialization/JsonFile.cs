using System.Text.Json;
using Niddy.Core.IO;

namespace Niddy.Core.Serialization;

/// <summary>
/// Reads and writes JSON files. Writes are atomic (see <see cref="AtomicFile"/>), so a file is never left
/// half-written. Null options means <c>JsonSerializerOptions.Niddy</c>.
/// </summary>
public static class JsonFile
{
    /// <summary>
    /// Reads and deserializes a JSON file. Returns default if the file doesn't exist, is empty, or isn't valid JSON
    /// for <typeparamref name="TValue"/>.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="options">The serializer options. Null is <c>JsonSerializerOptions.Niddy</c>.</param>
    public static TValue? Read<TValue>(string path, JsonSerializerOptions? options = null)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        string json = File.ReadAllText(path);
        return JsonSerializer.DeserializeOrDefault<TValue>(json, options);
    }

    /// <summary>
    /// Asynchronously reads and deserializes a JSON file. Returns default if the file doesn't exist, is empty, or
    /// isn't valid JSON for <typeparamref name="TValue"/>.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="options">The serializer options. Null is <c>JsonSerializerOptions.Niddy</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public static async Task<TValue?> ReadAsync<TValue>(
        string path,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return default;
        }

        string json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.DeserializeOrDefault<TValue>(json, options);
    }

    /// <summary>
    /// Serializes <paramref name="value"/> to a JSON file, atomically. Creates the folder if it doesn't exist.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options. Null is <c>JsonSerializerOptions.Niddy</c>.</param>
    public static void Write<TValue>(string path, TValue value, JsonSerializerOptions? options = null) =>
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(value, options ?? JsonSerializerOptions.Niddy));

    /// <summary>
    /// Asynchronously serializes <paramref name="value"/> to a JSON file, atomically. Creates the folder if it
    /// doesn't exist.
    /// </summary>
    /// <param name="path">The file to write.</param>
    /// <param name="value">The value to serialize.</param>
    /// <param name="options">The serializer options. Null is <c>JsonSerializerOptions.Niddy</c>.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public static Task WriteAsync<TValue>(
        string path,
        TValue value,
        JsonSerializerOptions? options = null,
        CancellationToken cancellationToken = default) =>
        AtomicFile.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(value, options ?? JsonSerializerOptions.Niddy),
            cancellationToken: cancellationToken);
}
