using Avalonia.Controls;

namespace Niddy.Avalonia.Tables;

/// <summary>What a <see cref="TableGrid"/> asks its <see cref="TableSource{T}"/> for: one page, sorted.</summary>
public sealed record TablePageRequest
{
    /// <summary>The page, from 1.</summary>
    public required int Page { get; init; }

    /// <summary>The page size: how many items a full page has.</summary>
    public required int PageSize { get; init; }

    /// <summary>
    /// For cursor-based sources, the <see cref="Niddy.Core.Collections.ItemPage{T}.NextCursor"/> the page before this one
    /// returned. Null for the first page, and for pages the table hasn't been given a cursor for.
    /// </summary>
    public string? Cursor { get; init; }

    /// <summary>
    /// The column the user sorted by, or null to keep the source's own order. Also null when the sort was set in code
    /// for a path no column has; <see cref="SortKey"/> still says what to sort by.
    /// </summary>
    public DataGridColumn? SortColumn { get; init; }

    /// <summary>
    /// What to sort by, or null to keep the source's own order: the sort column's
    /// <see cref="DataGridColumn.SortMemberPath"/>, else its binding's path, else its header text.
    /// </summary>
    public string? SortKey { get; init; }

    /// <summary>Whether to sort descending rather than ascending.</summary>
    public bool SortDescending { get; init; }

    /// <summary>How many items come before this page: <c>(Page - 1) * PageSize</c>.</summary>
    public int Skip => (Page - 1) * PageSize;
}
