using System.Globalization;
using Avalonia.Data.Converters;
using Niddy.Core.Text;

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
    public static IValueConverter FileSize { get; } = new FuncValueConverter<object, string?>(value =>
        TryInt64(value, out long bytes) ? Humanize.Bytes(bytes, 1, binary: false, CultureInfo.CurrentCulture) : null);

    /// <summary>A <see cref="TimeSpan" /> as a duration, e.g. "2 h 5 min".</summary>
    public static IValueConverter Duration { get; } = new FuncValueConverter<TimeSpan?, string?>(value =>
        value is { } duration ? Humanize.Duration(duration, 2, CultureInfo.CurrentCulture) : null);

    /// <summary>
    ///     A <see cref="DateTimeOffset" /> or <see cref="DateTime" /> relative to now, e.g. "5 minutes ago". It's worked
    ///     out when the binding updates, so it doesn't tick on its own.
    /// </summary>
    public static IValueConverter RelativeTime { get; } = new FuncValueConverter<object, string?>(value => value switch
    {
        DateTimeOffset time => Humanize.RelativeTime(time),
        DateTime time => Humanize.RelativeTime(time),
        _ => null,
    });

    /// <summary>A number in short form, e.g. "12.3K". Accepts any integer type.</summary>
    public static IValueConverter Count { get; } = new FuncValueConverter<object, string?>(value =>
        TryInt64(value, out long count) ? Humanize.Count(count, 1, CultureInfo.CurrentCulture) : null);

    private static bool TryInt64(object? value, out long result)
    {
        switch (value)
        {
            case long l:
                result = l;
                return true;
            case int i:
                result = i;
                return true;
            case uint ui:
                result = ui;
                return true;
            case short s:
                result = s;
                return true;
            case ushort us:
                result = us;
                return true;
            case byte b:
                result = b;
                return true;
            case ulong ul when ul <= long.MaxValue:
                result = (long)ul;
                return true;
            case double d when double.IsFinite(d):
                result = (long)d;
                return true;
            case decimal m:
                result = (long)m;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
