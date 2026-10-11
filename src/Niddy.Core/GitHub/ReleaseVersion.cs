namespace Niddy.Core.GitHub;

/// <summary>
/// A version read from a release tag, ordered the way semantic versioning orders them: <c>v1.2.0</c>,
/// <c>release-1.2</c> and <c>1.2.0.0</c> are equal, <c>1.2.0-beta.2</c> comes before <c>1.2.0-beta.10</c>, which comes
/// before <c>1.2.0</c>. <c>+build</c> metadata is ignored.
/// </summary>
internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    private ReleaseVersion(Version version, string[] prerelease)
    {
        this.Version = version;
        this.Prerelease = prerelease;
    }

    /// <summary>The numeric part, as written: <c>1.2</c> stays two components.</summary>
    public Version Version { get; }

    /// <summary>The prerelease label's dot-separated parts, e.g. <c>["beta", "2"]</c>; empty for a release.</summary>
    public string[] Prerelease { get; }

    public bool IsPrerelease => this.Prerelease.Length > 0;

    /// <summary>
    /// Reads a version from a tag or version string: from its first digit, up to 4 numeric components, then an optional
    /// prerelease label (after <c>-</c>, or straight after the numbers as in <c>1.0rc1</c>). Null if there's no number.
    /// </summary>
    public static ReleaseVersion? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        int start = text.AsSpan().IndexOfAnyInRange('0', '9');
        if (start < 0)
        {
            return null;
        }

        string rest = text[start..].Trim();
        int plus = rest.IndexOf('+');
        if (plus >= 0)
        {
            rest = rest[..plus];
        }

        int end = 0;
        while (end < rest.Length && (char.IsAsciiDigit(rest[end]) || (rest[end] == '.' && end + 1 < rest.Length && char.IsAsciiDigit(rest[end + 1]))))
        {
            end++;
        }

        string[] parts = rest[..end].Split('.');
        if (parts.Length > 4)
        {
            return null;
        }

        int[] numbers = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], out numbers[i]))
            {
                return null;
            }
        }

        Version version = numbers.Length switch
        {
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3]),
        };

        string label = rest[end..].TrimStart('-', '.', '_').Trim();
        string[] prerelease = label.Length == 0 ? [] : label.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return new ReleaseVersion(version, prerelease);
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int numeric = Normalize(this.Version).CompareTo(Normalize(other.Version));
        if (numeric != 0)
        {
            return numeric;
        }

        // A release outranks its prereleases.
        if (this.IsPrerelease != other.IsPrerelease)
        {
            return this.IsPrerelease ? -1 : 1;
        }

        for (int i = 0; i < Math.Min(this.Prerelease.Length, other.Prerelease.Length); i++)
        {
            int part = CompareIdentifiers(this.Prerelease[i], other.Prerelease[i]);
            if (part != 0)
            {
                return part;
            }
        }

        return this.Prerelease.Length.CompareTo(other.Prerelease.Length);
    }

    public override string ToString() =>
        this.IsPrerelease ? $"{this.Version}-{string.Join('.', this.Prerelease)}" : this.Version.ToString();

    // 1.2 and 1.2.0.0 are the same version; Version itself orders them differently.
    private static Version Normalize(Version version) =>
        new(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

    // Numeric identifiers compare as numbers and sort before alphanumeric ones, which compare ordinally.
    private static int CompareIdentifiers(string left, string right)
    {
        bool leftNumeric = long.TryParse(left, out long leftNumber);
        bool rightNumeric = long.TryParse(right, out long rightNumber);
        return (leftNumeric, rightNumeric) switch
        {
            (true, true) => leftNumber.CompareTo(rightNumber),
            (true, false) => -1,
            (false, true) => 1,
            _ => string.Compare(left, right, StringComparison.OrdinalIgnoreCase),
        };
    }
}
