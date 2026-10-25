using Niddy.Core.Collections;
using Niddy.Core.Results;

namespace Niddy.Avalonia.Tables;

/// <summary>
/// A <see cref="TableSource"/> whose pages come from a function, e.g. one that calls a web API a page at a time. The
/// function gets the page, page size, cursor and sort, and returns an <see cref="ItemPage{T}"/>; Niddy.Core's
/// <c>…PageAsync</c> methods (such as <c>GitHubIssues.ListPageAsync</c>) fit it directly.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
public sealed class PagedTableSource<T> : TableSource<T>
{
    private readonly Func<TablePageRequest, CancellationToken, Task<Result<ItemPage<T>>>> _loadPage;

    /// <summary>Creates a source.</summary>
    /// <param name="loadPage">
    /// Loads a page. It's called on the UI thread, so it should await anything slow rather than block.
    /// </param>
    /// <param name="canJumpToPage">
    /// False for cursor-based APIs, which can only read the page after one they've read; the table then pages forward
    /// with each page's <see cref="ItemPage{T}.NextCursor"/>, and back with the cursors it kept.
    /// </param>
    /// <param name="canSort">
    /// Whether <paramref name="loadPage"/> sorts by <see cref="TablePageRequest.SortKey"/>. Defaults to false, so
    /// clicking a header does nothing. For an API that sorts by only some fields, pass true and set
    /// <c>CanUserSort = false</c> on the other columns.
    /// </param>
    public PagedTableSource(
        Func<TablePageRequest, CancellationToken, Task<Result<ItemPage<T>>>> loadPage,
        bool canJumpToPage = true,
        bool canSort = false)
    {
        ArgumentNullException.ThrowIfNull(loadPage);
        _loadPage = loadPage;
        CanJumpToPage = canJumpToPage;
        CanSort = canSort;
    }

    /// <inheritdoc />
    public override bool CanJumpToPage { get; }

    /// <inheritdoc />
    public override bool CanSort { get; }

    /// <summary>Calls the function.</summary>
    /// <param name="request">The page, page size, cursor and sort.</param>
    /// <param name="cancellationToken">Cancelled when the table moves on before the page loads.</param>
    /// <returns>What the function returned.</returns>
    protected override Task<Result<ItemPage<T>>> LoadPageAsync(TablePageRequest request, CancellationToken cancellationToken) =>
        _loadPage(request, cancellationToken);
}
