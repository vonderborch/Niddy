using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Niddy.Avalonia.Converters;

/// <summary>
///     Binds a radio button or toggle to one value of an enum: true when the bound value equals the converter
///     parameter, and back to that value when checked. The parameter can be the enum value itself (<c>{x:Static}</c>)
///     or its name.
/// </summary>
/// <example>
///     <code>
///         &lt;RadioButton Content="Dark"
///                      IsChecked="{Binding Theme, Converter={x:Static niddy:EnumToBoolConverter.Instance}, ConverterParameter=Dark}" /&gt;
///     </code>
/// </example>
public sealed class EnumToBoolConverter : IValueConverter
{
    /// <summary>A shared instance.</summary>
    public static EnumToBoolConverter Instance { get; } = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum enumValue || parameter is null)
            return false;
        return ToEnum(enumValue.GetType(), parameter) is { } target && enumValue.Equals(target);
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null)
            return BindingOperations.DoNothing;
        var enumType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        return enumType.IsEnum ? ToEnum(enumType, parameter) ?? BindingOperations.DoNothing : BindingOperations.DoNothing;
    }

    private static object? ToEnum(Type enumType, object parameter) => parameter switch
    {
        _ when parameter.GetType() == enumType => parameter,
        string name when Enum.TryParse(enumType, name, ignoreCase: true, out var parsed) => parsed,
        _ => null,
    };
}
