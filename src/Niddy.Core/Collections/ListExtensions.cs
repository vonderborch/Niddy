namespace Niddy.Core.Collections;

/// <summary>
/// Extensions on <see cref="List{T}"/>.
/// </summary>
public static class ListExtensions
{
    extension<T>(List<T> list)
    {
        /// <summary>
        /// Returns a new list with this list's items followed by <paramref name="other"/>'s. Neither list is changed.
        /// </summary>
        /// <param name="other">The items to add after this list's.</param>
        public List<T> CombineLists(List<T> other)
        {
            List<T> combined = new(list);
            combined.AddRange(other);
            return combined;
        }

        /// <summary>
        /// Returns true if <paramref name="value" /> is in the list. For strings, <paramref name="caseSensitive" />
        /// controls whether case matters.
        /// </summary>
        /// <param name="value">The item to look for.</param>
        /// <param name="caseSensitive">For strings, whether case matters.</param>
        public bool IsContained(T value, bool caseSensitive = true)
        {
            if (list == null || list.Count == 0)
            {
                return false;
            }
            if (value is string strValue)
            {
                StringComparison comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                return list.OfType<string>().Any(item => string.Equals(item, strValue, comparison));
            }
            return list.Contains(value);
        }
    }

    extension<T>(List<T> list) where T : IComparable<T>
    {
        /// <summary>
        /// Returns true if both lists hold the same items, in any order.
        /// </summary>
        /// <param name="other">The list to compare with.</param>
        public bool CompareLists(List<T> other)
        {
            if (list.Count != other.Count)
            {
                return false;
            }
            List<T> sorted = new(list);
            sorted.Sort();
            List<T> otherSorted = new(other);
            otherSorted.Sort();
            return sorted.SequenceEqual(otherSorted);
        }
    }
}
