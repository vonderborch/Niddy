using System.Text;
using Niddy.Helpers;

namespace Niddy.IO;

/// <summary>
/// Writes files so readers only ever see the old contents or the new ones, never a half-written file, even if the
/// app crashes or the power fails mid-write. The contents go to a temporary file in the same directory, which is
/// flushed to disk and then renamed over the target.
/// </summary>
/// <remarks>
/// The rename is atomic on the same volume on Linux, macOS and Windows (NTFS). The directory is created if needed.
/// On Windows the rename is retried briefly if another process (such as a virus scanner) holds the target open.
/// </remarks>
public static class AtomicFile
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>Writes <paramref name="contents"/> to <paramref name="path"/> atomically. UTF-8 without a BOM by default.</summary>
    public static void WriteAllText(string path, string contents, Encoding? encoding = null) =>
        WriteAllBytes(path, (encoding ?? Utf8NoBom).GetBytes(contents));

    /// <summary>Writes <paramref name="contents"/> to <paramref name="path"/> atomically. UTF-8 without a BOM by default.</summary>
    public static Task WriteAllTextAsync(string path, string contents, Encoding? encoding = null, CancellationToken cancellationToken = default) =>
        WriteAllBytesAsync(path, (encoding ?? Utf8NoBom).GetBytes(contents), cancellationToken);

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/> atomically.</summary>
    public static void WriteAllBytes(string path, ReadOnlySpan<byte> bytes)
    {
        var temp = CreateTempPath(path);
        try
        {
            using (var stream = OpenTemp(temp, useAsync: false))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            Replace(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>Writes <paramref name="bytes"/> to <paramref name="path"/> atomically.</summary>
    public static Task WriteAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken = default) =>
        WriteAsync(path, (stream, ct) => stream.WriteAsync(bytes, ct).AsTask(), cancellationToken);

    /// <summary>
    /// Writes to <paramref name="path"/> atomically through a stream, e.g. for serializers that write to streams. If
    /// <paramref name="write"/> throws, the target is left untouched.
    /// </summary>
    public static void Write(string path, Action<Stream> write)
    {
        ArgumentNullException.ThrowIfNull(write);
        var temp = CreateTempPath(path);
        try
        {
            using (var stream = OpenTemp(temp, useAsync: false))
            {
                write(stream);
                stream.Flush(flushToDisk: true);
            }

            Replace(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    /// <summary>
    /// Writes to <paramref name="path"/> atomically through a stream. If <paramref name="write"/> throws or is cancelled,
    /// the target is left untouched.
    /// </summary>
    public static async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(write);
        var temp = CreateTempPath(path);
        try
        {
            await using (var stream = OpenTemp(temp, useAsync: true))
            {
                await write(stream, cancellationToken);
                await stream.FlushAsync(cancellationToken);
                stream.Flush(flushToDisk: true);
            }

            cancellationToken.ThrowIfCancellationRequested();
            Replace(temp, path);
        }
        catch
        {
            TryDelete(temp);
            throw;
        }
    }

    private static string CreateTempPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
    }

    private static FileStream OpenTemp(string temp, bool useAsync) =>
        new(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync);

    private static void Replace(string temp, string path) =>
        Retry.Execute(() => File.Move(temp, path, overwrite: true), maxRetries: 5, baseDelayMs: 20, maxDelayMs: 200);

    private static void TryDelete(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch
        {
            // Best effort: a stray temporary file is harmless.
        }
    }
}
