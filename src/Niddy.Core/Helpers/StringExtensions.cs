using System.Runtime.CompilerServices;

namespace Niddy.Helpers;

/// <summary>
/// Extension methods for strings and string-producing operations on enumerables.
/// </summary>
public static class StringExtensions
{
    /// <summary>
    /// Returns true if the string is null or empty.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrEmpty(this string? value)
    {
        return string.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Returns true if the string is null or contains only whitespace.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsNullOrWhiteSpace(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    /// <summary>
    /// Joins the elements of <paramref name="values" /> using <paramref name="separator" /> as the separator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Join<T>(this string separator, IEnumerable<T> values)
    {
        IEnumerable<string> values2 = (values as IEnumerable<string>) ?? values.Select(v =>
        {
            object obj = v?.ToString();
            if (obj == null)
            {
                obj = string.Empty;
            }
            return (string)obj;
        });
        return string.Join(separator, values2);
    }

    /// <summary>
    /// Joins the elements of <paramref name="values" /> using <paramref name="separator" /> as the separator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Join<T>(this IEnumerable<T> values, string separator)
    {
        IEnumerable<string> values2 = (values as IEnumerable<string>) ?? values.Select(v =>
        {
            object obj = v?.ToString();
            if (obj == null)
            {
                obj = string.Empty;
            }
            return (string)obj;
        });
        return string.Join(separator, values2);
    }
}
