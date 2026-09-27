using System.Collections;
using Avalonia.Data.Converters;

namespace Niddy.Avalonia.Converters;

/// <summary>Converters for collections, e.g. to show an empty-state message.</summary>
/// <example>
///     <code>
///         &lt;TextBlock Text="Nothing here yet"
///                    IsVisible="{Binding Items, Converter={x:Static niddy:CollectionConverters.IsEmpty}}" /&gt;
///     </code>
///     Bind to <c>Items.Count</c> instead if the collection changes, since a binding to the collection itself only
///     updates when the property is replaced.
/// </example>
public static class CollectionConverters
{
    /// <summary>True for null, an empty collection or enumerable, or a count of zero.</summary>
    public static IValueConverter IsEmpty { get; } = new FuncValueConverter<object?, bool>(IsEmptyValue);

    /// <summary>True for a non-empty collection or enumerable, or a count above zero.</summary>
    public static IValueConverter IsNotEmpty { get; } = new FuncValueConverter<object?, bool>(value => !IsEmptyValue(value));

    private static bool IsEmptyValue(object? value) => value switch
    {
        null => true,
        int count => count <= 0,
        string text => text.Length == 0,
        ICollection collection => collection.Count == 0,
        IEnumerable enumerable => !enumerable.GetEnumerator().MoveNext(),
        _ => false,
    };
}
