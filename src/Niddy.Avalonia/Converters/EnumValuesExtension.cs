using Avalonia.Markup.Xaml;

namespace Niddy.Avalonia.Converters;

/// <summary>
///     Provides the values of an enum, e.g. for a combo box's items.
/// </summary>
/// <example>
///     <code>
///         &lt;ComboBox ItemsSource="{niddy:EnumValues local:Priority}" SelectedItem="{Binding Priority}" /&gt;
///     </code>
/// </example>
public sealed class EnumValuesExtension : MarkupExtension
{
    /// <summary>The enum type.</summary>
    public Type? Type { get; set; }

    /// <summary>Creates the extension; set <see cref="Type" />.</summary>
    public EnumValuesExtension()
    {
    }

    /// <summary>Creates the extension for <paramref name="type" />.</summary>
    public EnumValuesExtension(Type type)
    {
        Type = type;
    }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        Type type = Type ?? throw new InvalidOperationException("EnumValues needs a Type.");
        Type type2 = Nullable.GetUnderlyingType(type) ?? type;
        if (!type2.IsEnum)
        {
            throw new ArgumentException($"{type} isn't an enum.");
        }
        return Enum.GetValues(type2);
    }
}
