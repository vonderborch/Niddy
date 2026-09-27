using System.Globalization;

namespace Niddy.Helpers;

/// <summary>
/// Formats numbers, sizes, durations and times for people: <c>"1.2 MB"</c>, <c>"3.4K"</c>, <c>"1 h 5 min"</c>,
/// <c>"3 minutes ago"</c>, <c>"2 files"</c>. Numbers use the current culture unless a provider is passed; the words
/// are English.
/// </summary>
public static class Humanize
{
    private static readonly string[] DecimalByteUnits = new string[7] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };

    private static readonly string[] BinaryByteUnits = new string[7] { "B", "KiB", "MiB", "GiB", "TiB", "PiB", "EiB" };

    private static readonly string[] CountSuffixes = new string[5] { "", "K", "M", "B", "T" };

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
        string[] array = (binary ? BinaryByteUnits : DecimalByteUnits);
        var (value, num) = Scale(bytes, binary ? 1024 : 1000, array.Length);
        return (num == 0) ? (bytes.ToString("N0", provider ?? CultureInfo.CurrentCulture) + " B") : (Number(value, decimals, provider) + " " + array[num]);
    }

    /// <summary>Formats a count compactly, e.g. <c>"950"</c>, <c>"1.2K"</c>, <c>"3.4M"</c>, <c>"2B"</c>.</summary>
    /// <param name="count">The count.</param>
    /// <param name="decimals">The most decimal places to show; trailing zeros are dropped.</param>
    /// <param name="provider">The culture for the number; defaults to the current culture.</param>
    public static string Count(long count, int decimals = 1, IFormatProvider? provider = null)
    {
        var (num, num2) = Scale(count, 1000.0, CountSuffixes.Length);
        if (num2 < CountSuffixes.Length - 1 && Math.Abs(Math.Round(num, decimals)) >= 1000.0)
        {
            num /= 1000.0;
            num2++;
        }
        return Number(num, (num2 != 0) ? decimals : 0, provider) + CountSuffixes[num2];
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
        string text = ((duration < TimeSpan.Zero) ? "-" : "");
        TimeSpan timeSpan = duration.Duration();
        if (timeSpan < TimeSpan.FromSeconds(1L))
        {
            return text + Number(timeSpan.TotalMilliseconds, 0, provider) + " ms";
        }
        if (timeSpan < TimeSpan.FromMinutes(1L))
        {
            return text + Number(Math.Floor(timeSpan.TotalSeconds * 10.0) / 10.0, 1, provider) + " s";
        }
        List<string> list = new List<string>(maxUnits);
        (long, string)[] array = new(long, string)[4]
        {
            ((long)timeSpan.TotalDays, "d"),
            (timeSpan.Hours, "h"),
            (timeSpan.Minutes, "min"),
            (timeSpan.Seconds, "s")
        };
        for (int i = 0; i < array.Length; i++)
        {
            var (num, text2) = array[i];
            if (list.Count == maxUnits)
            {
                break;
            }
            if (num > 0)
            {
                list.Add(num.ToString("N0", provider ?? CultureInfo.CurrentCulture) + " " + text2);
            }
            else if (list.Count > 0)
            {
                break;
            }
        }
        return text + string.Join(' ', list);
    }

    /// <summary>
    /// Describes <paramref name="time" /> relative to now, e.g. <c>"just now"</c>, <c>"3 minutes ago"</c>,
    /// <c>"in 2 hours"</c>, <c>"yesterday"</c>, <c>"5 days ago"</c>, <c>"2 years ago"</c>.
    /// </summary>
    /// <param name="time">The time to describe.</param>
    /// <param name="now">The current time; defaults to <see cref="UtcNow" />.</param>
    public static string RelativeTime(DateTimeOffset time, DateTimeOffset? now = null)
    {
        TimeSpan timeSpan = (now ?? DateTimeOffset.UtcNow) - time;
        bool flag = timeSpan >= TimeSpan.Zero;
        TimeSpan timeSpan2 = timeSpan.Duration();
        if (timeSpan2 < TimeSpan.FromSeconds(45L))
        {
            return "just now";
        }
        if (timeSpan2 >= TimeSpan.FromHours(24) && timeSpan2 < TimeSpan.FromHours(48))
        {
            return flag ? "yesterday" : "tomorrow";
        }
        double totalDays = timeSpan2.TotalDays;
        string text = ((timeSpan2 < TimeSpan.FromMinutes(45L)) ? Plural(Math.Max(1, (int)Math.Round(timeSpan2.TotalMinutes)), "minute") : ((totalDays < 1.0) ? Plural(Math.Max(1, (int)Math.Round(timeSpan2.TotalHours)), "hour") : ((totalDays < 30.0) ? Plural((int)Math.Round(timeSpan2.TotalDays), "day") : ((!(totalDays < 365.0)) ? Plural(Math.Max(1, (int)Math.Round(timeSpan2.TotalDays / 365.25)), "year") : Plural(Math.Max(1, (int)Math.Round(timeSpan2.TotalDays / 30.44)), "month")))));
        string text2 = text;
        return flag ? (text2 + " ago") : ("in " + text2);
    }

    /// <inheritdoc cref="RelativeTime(DateTimeOffset,Nullable{DateTimeOffset})" />
    /// <remarks>A <see cref="Unspecified" /> time is treated as local.</remarks>
    public static string RelativeTime(DateTime time, DateTime? now = null)
    {
        DateTimeOffset time2 = new DateTimeOffset((time.Kind == DateTimeKind.Unspecified) ? DateTime.SpecifyKind(time, DateTimeKind.Local) : time);
        DateTimeOffset? now2;
        if (now.HasValue)
        {
            DateTime valueOrDefault = now.GetValueOrDefault();
            now2 = new DateTimeOffset((valueOrDefault.Kind == DateTimeKind.Unspecified) ? DateTime.SpecifyKind(valueOrDefault, DateTimeKind.Local) : valueOrDefault);
        }
        else
        {
            now2 = null;
        }
        return RelativeTime(time2, now2);
    }

    /// <summary>Formats a count with a noun, e.g. <c>"1 file"</c>, <c>"0 files"</c>, <c>"1,234 files"</c>.</summary>
    /// <param name="count">The count.</param>
    /// <param name="singular">The noun for one.</param>
    /// <param name="plural">The noun for any other count; defaults to <paramref name="singular" /> plus "s".</param>
    /// <param name="provider">The culture for the number; defaults to the current culture.</param>
    public static string Plural(long count, string singular, string? plural = null, IFormatProvider? provider = null)
    {
        string text = count.ToString("N0", provider ?? CultureInfo.CurrentCulture);
        bool flag = ((count == -1 || count == 1) ? true : false);
        return text + " " + (flag ? singular : (plural ?? (singular + "s")));
    }

    /// <summary>Formats a number as an English ordinal, e.g. <c>"1st"</c>, <c>"2nd"</c>, <c>"11th"</c>, <c>"23rd"</c>.</summary>
    public static string Ordinal(long number)
    {
        long num = Math.Abs(number % 100);
        string text;
        if (num >= 11 && num <= 13)
        {
            text = "th";
            goto IL_0077;
        }
        long num2 = Math.Abs(number % 10);
        long num3 = num2 - 1;
        if ((ulong)num3 > 2uL)
        {
            goto IL_0069;
        }
        switch ((int)num3)
        {
        case 0:
            break;
        case 1:
            goto IL_0059;
        case 2:
            goto IL_0061;
        default:
            goto IL_0069;
        }
        string text2 = "st";
        goto IL_0071;
        IL_0061:
        text2 = "rd";
        goto IL_0071;
        IL_0059:
        text2 = "nd";
        goto IL_0071;
        IL_0071:
        text = text2;
        goto IL_0077;
        IL_0077:
        string text3 = text;
        return number.ToString(CultureInfo.InvariantCulture) + text3;
        IL_0069:
        text2 = "th";
        goto IL_0071;
    }

    private static (double Value, int Unit) Scale(long amount, double step, int unitCount)
    {
        double num = amount;
        int num2 = 0;
        while (Math.Abs(num) >= step && num2 < unitCount - 1)
        {
            num /= step;
            num2++;
        }
        return (Value: num, Unit: num2);
    }

    private static string Number(double value, int decimals, IFormatProvider? provider)
    {
        return value.ToString((decimals <= 0) ? "#,0" : ("#,0." + new string('#', decimals)), provider ?? CultureInfo.CurrentCulture);
    }
}
