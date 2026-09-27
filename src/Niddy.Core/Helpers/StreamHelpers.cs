using System.Text;

namespace Niddy.Helpers;

/// <summary>
/// Helper methods for working with streams.
/// </summary>
public static class StreamHelpers
{
    /// <summary>
    /// Reads the entire stream and returns its contents as a byte array.
    /// </summary>
    public static byte[] ReadToBytes(Stream stream)
    {
        using MemoryStream memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Asynchronously reads the entire stream and returns its contents as a byte array.
    /// </summary>
    public static async Task<byte[]> ReadToBytesAsync(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
    {
        using MemoryStream ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    /// <summary>
    /// Reads the entire stream and returns its contents as a string using the specified encoding.
    /// Defaults to UTF-8.
    /// </summary>
    public static string ReadToString(Stream stream, Encoding? encoding = null)
    {
        return (encoding ?? Encoding.UTF8).GetString(ReadToBytes(stream));
    }

    /// <summary>
    /// Asynchronously reads the entire stream and returns its contents as a string using the specified encoding.
    /// Defaults to UTF-8.
    /// </summary>
    public static async Task<string> ReadToStringAsync(Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default(CancellationToken))
    {
        byte[] bytes = await ReadToBytesAsync(stream, cancellationToken);
        return (encoding ?? Encoding.UTF8).GetString(bytes);
    }
}
