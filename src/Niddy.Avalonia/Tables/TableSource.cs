using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using Niddy.Core.Collections;
using Niddy.Core.Results;

namespace Niddy.Avalonia.Tables;

/// <summary>
/// Where a <see cref="TableGrid"/> gets its rows when they're loaded a page at a time, e.g. from a web API: the source
/// loads each page, sorted, when the table asks. Use <see cref="PagedTableSource{T}"/> for a function that loads pages,
/// or derive from <see cref="TableSource{T}"/>. Rows already in memory don't need one: set the table's
/// <c>ItemsSource</c>.
/// </summary>
public abstract class TableSource
{
    private int _changeCount;

    private protected TableSource()
    {
    }

    /// <summary>
    /// Whether any page can be loaded directly. False for cursor-based sources, which the table reads forward from the
    /// last page it has a cursor for; its page box is then read-only and Last is off. Defaults to true.
    /// </summary>
    public virtual bool CanJumpToPage => true;

    /// <summary>
    /// Whether the source sorts by <see cref="TablePageRequest.SortKey"/>. When false, clicking a column header does
    /// nothing. To sort by only some columns, set <c>CanUserSort = false</c> on the others. Defaults to true.
    /// </summary>
    public virtual bool CanSort => true;

    /// <summary>Counts changes, so a table that wasn't listening can tell it missed one.</summary>
    internal int ChangeCount => Volatile.Read(ref _changeCount);

    /// <summary>Raised by <see cref="Refresh"/> and <see cref="Reset"/>, with whether to go back to the first page.</summary>
    internal event Action<bool>? Changed;

    /// <summary>Tells the tables showing this source to load their current page again. Any thread.</summary>
    public void Refresh() => RaiseChanged(false);

    /// <summary>
    /// Tells the tables showing this source to go back to the first page, e.g. after a filter changed. Any thread.
    /// </summary>
    public void Reset() => RaiseChanged(true);

    internal abstract Task<Result<ItemPage<object>>> LoadAsync(TablePageRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the collection a table shows this source's pages in. It's typed, so the grid sees the row type, e.g. to
    /// generate columns.
    /// </summary>
    internal abstract IPageItems CreatePageItems();

    private void RaiseChanged(bool reset)
    {
        Interlocked.Increment(ref _changeCount);
        Changed?.Invoke(reset);
    }
}

/// <summary>
/// A <see cref="TableSource"/> of <typeparamref name="T"/>: override <see cref="LoadPageAsync"/> to load one page.
/// </summary>
/// <typeparam name="T">The row type.</typeparam>
public abstract class TableSource<T> : TableSource
{
    /// <summary>
    /// Loads one page. It's called on the UI thread; anything slow should be awaited, not run on it. A failed
    /// <see cref="Result{T}"/> shows its message in the table, with a Retry button, and so does an exception.
    /// </summary>
    /// <param name="request">The page, page size, cursor and sort.</param>
    /// <param name="cancellationToken">Cancelled when the table moves on, e.g. to another page, before this one loads.</param>
    /// <returns>The page's items and what's known about the rest.</returns>
    protected abstract Task<Result<ItemPage<T>>> LoadPageAsync(TablePageRequest request, CancellationToken cancellationToken);

    internal override async Task<Result<ItemPage<object>>> LoadAsync(TablePageRequest request, CancellationToken cancellationToken)
    {
        Result<ItemPage<T>> result = await LoadPageAsync(request, cancellationToken);
        if (!result.TryGetValue(out ItemPage<T> page, out Error? error))
        {
            return error!;
        }

        IReadOnlyList<object> items = page.Items as IReadOnlyList<object> ?? page.Items.Cast<object>().ToList();
        return new ItemPage<object>
        {
            Items = items,
            HasMore = page.HasMore,
            NextCursor = page.NextCursor,
            TotalCount = page.TotalCount,
            PageCount = page.PageCount,
        };
    }

    internal override IPageItems CreatePageItems() => new PageItems<T>();
}

/// <summary>The rows of the page a <see cref="TableGrid"/> shows, replaced as pages load.</summary>
internal interface IPageItems : IEnumerable<object>
{
    /// <summary>Replaces the rows, raising one reset.</summary>
    void ReplaceAll(IReadOnlyList<object> items);
}

/// <summary>A page's rows, typed so the grid sees the row type.</summary>
internal sealed class PageItems<T> : ObservableCollection<T>, IPageItems
{
    public void ReplaceAll(IReadOnlyList<object> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (object item in items)
        {
            Items.Add((T)item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    IEnumerator<object> IEnumerable<object>.GetEnumerator() => Items.Cast<object>().GetEnumerator();
}
