namespace Niddy.Helpers;

/// <summary>
/// Helper methods for working with byte quantities.
/// </summary>
public static class ByteHelpers
{
    private static readonly string[] Suffixes = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>
    /// Formats a byte count as a human-readable string (e.g. "1.4 MB").
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        var value = (double)bytes;
        var suffixIndex = 0;

        while (value >= 1024 && suffixIndex < Suffixes.Length - 1)
        {
            value /= 1024;
            suffixIndex++;
        }

        return suffixIndex == 0
            ? $"{(long)value} {Suffixes[suffixIndex]}"
            : $"{value:F1} {Suffixes[suffixIndex]}";
    }
}
