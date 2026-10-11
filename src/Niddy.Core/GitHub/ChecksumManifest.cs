namespace Niddy.Core.GitHub;

/// <summary>
/// Reads SHA-256 checksum files: <c>sha256sum</c>'s output (<c>hash  name</c> or <c>hash *name</c>), BSD style
/// (<c>SHA256 (name) = hash</c>), or a lone hash, as in a <c>.sha256</c> file next to a single asset.
/// </summary>
internal static class ChecksumManifest
{
    /// <summary>Whether <paramref name="name"/> looks like a checksum list for a whole release, e.g. <c>SHA256SUMS</c>.</summary>
    public static bool IsManifestName(string name)
    {
        string lower = name.ToLowerInvariant();
        return !lower.EndsWith(".sig", StringComparison.Ordinal) && !lower.EndsWith(".asc", StringComparison.Ordinal)
            && !lower.EndsWith(".pem", StringComparison.Ordinal)
            && (lower.Contains("sha256sum") || lower.Contains("checksum"));
    }

    /// <summary>The hash listed for <paramref name="fileName"/>, as lowercase hex, or null if it isn't listed.</summary>
    public static string? Find(string content, string fileName)
    {
        foreach (string rawLine in content.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            (string? hash, string? name) = ParseLine(line);
            if (hash is not null && name is not null && string.Equals(FileNameOf(name), fileName, StringComparison.Ordinal))
            {
                return hash;
            }
        }

        return null;
    }

    /// <summary>The hash in a file that holds one, e.g. <c>app.zip.sha256</c>: its first line's hash, or null.</summary>
    public static string? FindSingle(string content)
    {
        string? first = content.Split('\n').Select(line => line.Trim()).FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));
        if (first is null)
        {
            return null;
        }

        return IsHash(first) ? first.ToLowerInvariant() : ParseLine(first).Hash;
    }

    private static (string? Hash, string? Name) ParseLine(string line)
    {
        // BSD: SHA256 (name) = hash
        if (line.StartsWith("SHA256 (", StringComparison.OrdinalIgnoreCase))
        {
            int close = line.LastIndexOf(") = ", StringComparison.Ordinal);
            if (close > 0)
            {
                string hash = line[(close + 4)..].Trim();
                return IsHash(hash) ? (hash.ToLowerInvariant(), line["SHA256 (".Length..close]) : (null, null);
            }

            return (null, null);
        }

        // GNU: hash  name, or hash *name for binary mode.
        int space = line.IndexOfAny([' ', '\t']);
        if (space == 64 && IsHash(line[..64]))
        {
            string name = line[space..].TrimStart(' ', '\t');
            return (line[..64].ToLowerInvariant(), name.StartsWith('*') ? name[1..] : name);
        }

        return (null, null);
    }

    private static string FileNameOf(string name)
    {
        string normalized = name.Trim().Replace('\\', '/');
        return normalized[(normalized.LastIndexOf('/') + 1)..];
    }

    private static bool IsHash(string text) => text.Length == 64 && text.All(char.IsAsciiHexDigit);
}
