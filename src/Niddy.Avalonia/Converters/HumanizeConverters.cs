using System.Globalization;
using Avalonia.Data.Converters;
using Niddy.Helpers;

namespace Niddy.Avalonia.Converters;

/// <summary>
///     Converters that format values for people, using <see cref="Humanize" />: file sizes, durations, relative times
///     and large counts.
/// </summary>
/// <example>
///     <code>
///         &lt;TextBlock Text="{Binding Length, Converter={x:Static niddy:HumanizeConverters.FileSize}}" /&gt;
///         &lt;TextBlock Text="{Binding Modified, Converter={x:Static niddy:HumanizeConverters.RelativeTime}}" /&gt;
///     </code>
/// </example>
public static class HumanizeConverters
{
    /// <summary>A byte count as a file size, e.g. "1.5 MB". Accepts any integer type.</summary>
    public static IValueConverter FileSize { get; } = new FuncValueConverter<object, string>((object? value) => TryInt64(value, out var result) ? Humanize.Bytes(result, 1, binary: false, CultureInfo.CurrentCulture) : null);

    /// <summary>A <see cref="TimeSpan" /> as a duration, e.g. "2 h 5 min".</summary>
    public static IValueConverter Duration { get; } = new FuncValueConverter<TimeSpan?, string>(value =>
    {
        object result;
        if (value.HasValue)
        {
            TimeSpan valueOrDefault = value.GetValueOrDefault();
            result = Humanize.Duration(valueOrDefault, 2, CultureInfo.CurrentCulture);
        }
        else
        {
            result = null;
        }
        return (string?)result;
    });

    /// <summary>
    ///     A <see cref="DateTimeOffset" /> or <see cref="DateTime" /> relative to now, e.g. "5 minutes ago". It's worked
    ///     out when the binding updates, so it doesn't tick on its own.
    /// </summary>
    public static IValueConverter RelativeTime { get; } = new FuncValueConverter<object, string>(value =>
    {
        string result = ((value is DateTimeOffset time) ? Humanize.RelativeTime(time) : ((!(value is DateTime time2)) ? null : Humanize.RelativeTime(time2)));
        return result;
    });

    /// <summary>A number in short form, e.g. "12.3K". Accepts any integer type.</summary>
    public static IValueConverter Count { get; } = new FuncValueConverter<object, string>((object? value) => TryInt64(value, out var result) ? Humanize.Count(result, 1, CultureInfo.CurrentCulture) : null);

    private static bool TryInt64(object? value, out long result)
    {
        if (!(value is long num))
        {
            if (!(value is int num2))
            {
                if (!(value is uint num3))
                {
                    if (!(value is short num4))
                    {
                        if (!(value is ushort num5))
                        {
                            if (!(value is byte b))
                            {
                                if (!(value is ulong num6))
                                {
                                    if (!(value is double num7))
                                    {
                                        if (value is decimal num8)
                                        {
                                            result = (long)num8;
                                            return true;
                                        }
                                    }
                                    else if (!double.IsNaN(num7) && !double.IsInfinity(num7))
                                    {
                                        result = (long)num7;
                                        return true;
                                    }
                                }
                                else if (num6 <= long.MaxValue)
                                {
                                    result = (long)num6;
                                    return true;
                                }
                                result = 0L;
                                return false;
                            }
                            result = b;
                            return true;
                        }
                        result = num5;
                        return true;
                    }
                    result = num4;
                    return true;
                }
                result = num3;
                return true;
            }
            result = num2;
            return true;
        }
        result = num;
        return true;
    }
}
