using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Niddy.Core.Text;

/// <summary>
/// Extensions on strings, and on sequences for joining them into one.
/// </summary>
public static class StringExtensions
{
    extension([NotNullWhen(false)] string? value)
    {
        /// <summary>
        /// Returns true if the string is null or empty. When it returns false, the compiler knows the string isn't null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNullOrEmpty()
        {
            bool output = string.IsNullOrEmpty(value);
            return output;
        }

        /// <summary>
        /// Returns true if the string is null or contains only whitespace. When it returns false, the compiler knows
        /// the string isn't null.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNullOrWhiteSpace()
        {
            bool output = string.IsNullOrWhiteSpace(value);
            return output;
        }
    }

    extension(string separator)
    {
        /// <summary>
        /// Joins <paramref name="values" />, with this string between each one. Null items become empty strings.
        /// </summary>
        /// <param name="values">The items to join.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Join<T>(IEnumerable<T> values)
        {
            IEnumerable<string> strings = values as IEnumerable<string> ?? values.Select(v => v?.ToString() ?? string.Empty);
            string output = string.Join(separator, strings);
            return output;
        }
    }

    extension<T>(IEnumerable<T> values)
    {
        /// <summary>
        /// Joins the items, with <paramref name="separator" /> between each one. Null items become empty strings.
        /// </summary>
        /// <param name="separator">What goes between the items.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string Join(string separator)
        {
            IEnumerable<string> strings = values as IEnumerable<string> ?? values.Select(v => v?.ToString() ?? string.Empty);
            string output = string.Join(separator, strings);
            return output;
        }
    }
}
