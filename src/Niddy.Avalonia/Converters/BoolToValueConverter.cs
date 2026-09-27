using System.Globalization;
using Avalonia.Data.Converters;

namespace Niddy.Avalonia.Converters;

/// <summary>
///     Converts a bool to one of two values, e.g. a brush, text or thickness. Null converts to
///     <see cref="FalseValue" />.
/// </summary>
/// <example>
///     <code>
///         &lt;niddy:BoolToValueConverter x:Key="OnlineColor" TrueValue="Green" FalseValue="Gray" /&gt;
///         ...
///         &lt;Ellipse Fill="{Binding IsOnline, Converter={StaticResource OnlineColor}}" /&gt;
///     </code>
/// </example>
public class BoolToValueConverter : IValueConverter
{
    /// <summary>The value for true.</summary>
    public object? TrueValue { get; set; }

    /// <summary>The value for false or null.</summary>
    public object? FalseValue { get; set; }

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (value is bool && (bool)value) ? TrueValue : FalseValue;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return object.Equals(value, TrueValue) || (object.Equals(value?.ToString(), TrueValue?.ToString()) && value != null);
    }
}
