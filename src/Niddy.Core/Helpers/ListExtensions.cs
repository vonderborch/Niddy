namespace Niddy.Helpers;

/// <summary>
/// Extension methods for lists.
/// </summary>
public static class ListExtensions
{
    /// <summary>
    /// Returns a new list containing all elements from both lists.
    /// </summary>
    public static List<T> CombineLists<T>(this List<T> list1, List<T> list2)
    {
        List<T> list3 = new List<T>(list1);
        list3.AddRange(list2);
        return list3;
    }

    /// <summary>
    /// Returns true if both lists contain the same elements, regardless of order.
    /// </summary>
    public static bool CompareLists<T>(this List<T> list1, List<T> list2) where T : IComparable<T>
    {
        if (list1.Count != list2.Count)
        {
            return false;
        }
        List<T> list3 = new List<T>(list1);
        list3.Sort();
        List<T> list4 = new List<T>(list2);
        list4.Sort();
        return list3.SequenceEqual(list4);
    }

    /// <summary>
    /// Returns true if <paramref name="value" /> is contained in the list.
    /// For strings, <paramref name="caseSensitive" /> controls whether the comparison is case-sensitive.
    /// </summary>
    public static bool IsContained<T>(this List<T> list, T value, bool caseSensitive = true)
    {
        if (list == null || list.Count == 0)
        {
            return false;
        }
        string strValue = value as string;
        if (strValue != null)
        {
            StringComparison comparison = (caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            return list.OfType<string>().Any((string item) => string.Equals(item, strValue, comparison));
        }
        return list.Contains(value);
    }
}
