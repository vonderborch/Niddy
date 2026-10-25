namespace Niddy.Core.Collections;

/// <summary>
/// One page of a longer list, as an app's own paging reads it: the items, and what's known about the rest. Sources
/// differ in what they can say. Some know the total, some only whether there's more, and some (cursor-based APIs such
/// as GraphQL) need a <see cref="NextCursor"/> to read the next page.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed record ItemPage<T>
{
    /// <summary>The page's items, in order. Can be empty, e.g. past the end.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Whether there's a page after this one.</summary>
    public bool HasMore { get; init; }

    /// <summary>
    /// For cursor-based sources, what to pass to read the next page; null on the last page, and for sources that are
    /// read by page number.
    /// </summary>
    public string? NextCursor { get; init; }

    /// <summary>How many items there are across all pages, or null if the source doesn't say.</summary>
    public int? TotalCount { get; init; }

    /// <summary>How many pages there are at this page size, or null if the source doesn't say.</summary>
    public int? PageCount { get; init; }

    /// <summary>An empty last page.</summary>
    public static ItemPage<T> Empty { get; } = new() { Items = [], PageCount = 1, TotalCount = 0 };
}
