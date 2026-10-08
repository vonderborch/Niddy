using System.Globalization;

namespace Niddy.Core.Text;

/// <summary>
/// Formats numbers, sizes, durations and times for people: <c>"1.2 MB"</c>, <c>"3.4K"</c>, <c>"1 h 5 min"</c>,
/// <c>"3 minutes ago"</c>, <c>"2 files"</c>. Numbers use the current culture unless a provider is passed; the words
/// are English.
/// </summary>
public static class Humanize
{
    private static readonly string[] DecimalByteUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    private static readonly string[] BinaryByteUnits = ["B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB"];

    private static readonly string[] CountSuffixes = ["", "K", "M", "B", "T"];

    /// <summary>Formats a size in bytes, e.g. <c>"512 B"</c>, <c>"1.5 KB"</c>, <c>"3.2 GB"</c>.</summary>
    /// <param name="bytes">The size in bytes.</param>
    /// <param name="decimals">The most decimal places to show; trailing zeros are dropped.</param>
    /// <param name="binary">
    /// Whether to use powers of 1024 with KiB, MiB, ... units. By default powers of 1000 with KB, MB, ... are used, like
    /// macOS and most storage makers.
    /// </param>
    /// <param name="provider">The culture for the number; defaults to the current culture.</param>
    public static string Bytes(long bytes, int decimals = 1, bool binary = false, IFormatProvider? provider = null)
    {
        string[] units = binary ? BinaryByteUnits : DecimalByteUnits;
        (double value, int unit) = Scale(bytes, binary ? 1024 : 1000, units.Length);
        
        return unit == 0
            ? bytes.ToString("N0", provider ?? CultureInfo.CurrentCulture) + " B"
            : Number(value, decimals, provider) + " " + units[unit];
    }

    /// <summary>Formats a count compactly, e.g. <c>"950"</c>, <c>"1.2K"</c>, <c>"3.4M"</c>, <c>"2B"</c>.</summary>
    /// <param name="count">The count.</param>
    /// <param name="decimals">The most decimal places to show; trailing zeros are dropped.</param>
    /// <param name="provider">The culture for the number; defaults to the current culture.</param>
    public static string Count(long count, int decimals = 1, IFormatProvider? provider = null)
    {
        (double value, int unit) = Scale(count, 1000.0, CountSuffixes.Length);
        // Rounding can carry into the next unit, e.g. 999,960 is "1M", not "1,000K".
        if (unit < CountSuffixes.Length - 1 && Math.Abs(Math.Round(value, decimals)) >= 1000.0)
        {
            value /= 1000.0;
            unit++;
        }
        return Number(value, unit != 0 ? decimals : 0, provider) + CountSuffixes[unit];
    }

    /// <summary>
    /// Formats a duration with its largest units, e.g. <c>"450 ms"</c>, <c>"3.2 s"</c>, <c>"5 min 3 s"</c>,
    /// <c>"1 h 5 min"</c>, <c>"2 d 3 h"</c>.
    /// </summary>
    /// <param name="duration">The duration. Negative durations get a minus sign.</param>
    /// <param name="maxUnits">The most units to show, from the largest; smaller ones are dropped, not rounded.</param>
    /// <param name="provider">The culture for the number; defaults to the current culture.</param>
    public static string Duration(TimeSpan duration, int maxUnits = 2, IFormatProvider? provider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxUnits, 1);
        string sign = duration < TimeSpan.Zero ? "-" : "";
        TimeSpan length = duration.Duration();
        if (length < TimeSpan.FromSeconds(1L))
        {
            return sign + Number(length.TotalMilliseconds, 0, provider) + " ms";
        }
        if (length < TimeSpan.FromMinutes(1L))
        {
            return sign + Number(Math.Floor(length.TotalSeconds * 10.0) / 10.0, 1, provider) + " s";
        }
        List<string> parts = new(maxUnits);
        (long Amount, string Unit)[] units =
        [
            ((long)length.TotalDays, "d"),
            (length.Hours, "h"),
            (length.Minutes, "min"),
            (length.Seconds, "s")
        ];
        foreach ((long amount, string unit) in units)
        {
            if (parts.Count == maxUnits)
            {
                break;
            }
            if (amount > 0)
            {
                parts.Add(amount.ToString("N0", provider ?? CultureInfo.CurrentCulture) + " " + unit);
            }
            else if (parts.Count > 0)
            {
                // Only adjacent units: 1 h 0 min 5 s is "1 h", not "1 h 5 s".
                break;
            }
        }
        return sign + string.Join(' ', parts);
    }

    /// <summary>
    /// Describes <paramref name="time" /> relative to now, e.g. <c>"just now"</c>, <c>"3 minutes ago"</c>,
    /// <c>"in 2 hours"</c>, <c>"yesterday"</c>, <c>"5 days ago"</c>, <c>"2 years ago"</c>.
    /// </summary>
    /// <param name="time">The time to describe.</param>
    /// <param name="now">The current time; defaults to <see cref="DateTimeOffset.UtcNow" />.</param>
    public static string RelativeTime(DateTimeOffset time, DateTimeOffset? now = null)
    {
        TimeSpan elapsed = (now ?? DateTimeOffset.UtcNow) - time;
        bool isPast = elapsed >= TimeSpan.Zero;
        TimeSpan distance = elapsed.Duration();
        if (distance < TimeSpan.FromSeconds(45L))
        {
            return "just now";
        }
        if (distance >= TimeSpan.FromHours(24) && distance < TimeSpan.FromHours(48))
        {
            return isPast ? "yesterday" : "tomorrow";
        }
        double totalDays = distance.TotalDays;
        string text = distance < TimeSpan.FromMinutes(45L) ? Plural(Math.Max(1, (int)Math.Round(distance.TotalMinutes)), "minute")
            : totalDays < 1.0 ? Plural(Math.Max(1, (int)Math.Round(distance.TotalHours)), "hour")
            : totalDays < 30.0 ? Plural((int)Math.Round(totalDays), "day")
            : totalDays < 365.0 ? Plural(Math.Max(1, (int)Math.Round(totalDays / 30.44)), "month")
            : Plural(Math.Max(1, (int)Math.Round(totalDays / 365.25)), "year");
        return isPast ? text + " ago" : "in " + text;
    }

    /// <inheritdoc cref="RelativeTime(DateTimeOffset,Nullable{DateTimeOffset})" />
    /// <remarks>A <see cref="DateTimeKind.Unspecified" /> time is treated as local.</remarks>
    public static string RelativeTime(DateTime time, DateTime? now = null)
    {
        return RelativeTime(ToOffset(time), now is { } value ? ToOffset(value) : null);

        static DateTimeOffset ToOffset(DateTime value) =>
            new(value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Local) : value);
    }

    /// <summary>Formats a count with a noun, e.g. <c>"1 file"</c>, <c>"0 files"</c>, <c>"1,234 files"</c>.</summary>
    /// <param name="count">The count.</param>
    /// <param name="singular">The noun for one.</param>
    /// <param name="plural">The noun for any other count; defaults to <paramref name="singular" /> plus "s".</param>
    /// <param name="provider">The culture for the number; defaults to the current culture.</param>
    public static string Plural(long count, string singular, string? plural = null, IFormatProvider? provider = null)
    {
        string text = count.ToString("N0", provider ?? CultureInfo.CurrentCulture);
        return text + " " + (count is 1 or -1 ? singular : plural ?? singular + "s");
    }

    /// <summary>Formats a number as an English ordinal, e.g. <c>"1st"</c>, <c>"2nd"</c>, <c>"11th"</c>, <c>"23rd"</c>.</summary>
    public static string Ordinal(long number)
    {
        string suffix = Math.Abs(number % 100) is >= 11 and <= 13
            ? "th"
            : Math.Abs(number % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
        return number.ToString(CultureInfo.InvariantCulture) + suffix;
    }

    private static (double Value, int Unit) Scale(long amount, double step, int unitCount)
    {
        double value = amount;
        int unit = 0;
        while (Math.Abs(value) >= step && unit < unitCount - 1)
        {
            value /= step;
            unit++;
        }
        return (value, unit);
    }

    private static string Number(double value, int decimals, IFormatProvider? provider)
    {
        return value.ToString(decimals <= 0 ? "#,0" : "#,0." + new string('#', decimals), provider ?? CultureInfo.CurrentCulture);
    }
}
