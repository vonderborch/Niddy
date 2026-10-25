using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Niddy.Core.Collections;
using Niddy.Core.Results;

namespace Niddy.Avalonia.Tables;

/// <summary>
/// A <see cref="DataGrid"/> that's read-only by default, pages its rows, and can lock rows against editing: everything a
/// DataGrid does works the same way. A pager under the rows (first, previous, page box, next, last, rows per page,
/// "61–90 of 352" and refresh) is added automatically.
/// </summary>
/// <remarks>
/// <para>
/// Rows come from <c>ItemsSource</c>, as with any DataGrid, and are paged in memory; sorting and filtering through
/// <c>CollectionView</c> apply to all of them, not just the page shown. Rows loaded a page at a time, e.g. from a web
/// API, come from <see cref="Source"/> instead: the source loads each page, sorted, when the table asks, and while one
/// loads a progress bar shows; a failure shows its message with a Retry button.
/// </para>
/// <para>
/// Set <c>IsReadOnly="False"</c> to edit, as with a DataGrid. <see cref="LockRows"/> then keeps each row locked until
/// it's unlocked with its lock button or <see cref="UnlockRow"/>.
/// </para>
/// <para>
/// The DataGrid needs its theme: <c>NiddyApp</c>'s adds it; otherwise include
/// <c>avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml</c> in the app's styles.
/// </para>
/// </remarks>
public partial class TableGrid : DataGrid
{
    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<TableSource?> SourceProperty =
        AvaloniaProperty.Register<TableGrid, TableSource?>(nameof(Source));

    /// <summary>Defines the <see cref="PageSize"/> property.</summary>
    public static readonly StyledProperty<int> PageSizeProperty =
        AvaloniaProperty.Register<TableGrid, int>(nameof(PageSize), 25, validate: size => size >= 1);

    /// <summary>Defines the <see cref="PageSizes"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<int>> PageSizesProperty =
        AvaloniaProperty.Register<TableGrid, IReadOnlyList<int>>(nameof(PageSizes), [10, 25, 50, 100]);

    /// <summary>Defines the <see cref="ShowPager"/> property.</summary>
    public static readonly StyledProperty<bool> ShowPagerProperty =
        AvaloniaProperty.Register<TableGrid, bool>(nameof(ShowPager), true);

    /// <summary>Defines the <see cref="ShowPageSizes"/> property.</summary>
    public static readonly StyledProperty<bool> ShowPageSizesProperty =
        AvaloniaProperty.Register<TableGrid, bool>(nameof(ShowPageSizes), true);

    /// <summary>Defines the <see cref="EmptyText"/> property.</summary>
    public static readonly StyledProperty<string> EmptyTextProperty =
        AvaloniaProperty.Register<TableGrid, string>(nameof(EmptyText), "Nothing to show.");

    /// <summary>Defines the <see cref="LockRows"/> property.</summary>
    public static readonly StyledProperty<bool> LockRowsProperty =
        AvaloniaProperty.Register<TableGrid, bool>(nameof(LockRows));

    /// <summary>Defines the <see cref="ShowLockButtons"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLockButtonsProperty =
        AvaloniaProperty.Register<TableGrid, bool>(nameof(ShowLockButtons), true);

    /// <summary>Defines the <see cref="Page"/> property.</summary>
    public static readonly DirectProperty<TableGrid, int> PageProperty =
        AvaloniaProperty.RegisterDirect<TableGrid, int>(nameof(Page), grid => grid.Page);

    /// <summary>Defines the <see cref="PageCount"/> property.</summary>
    public static readonly DirectProperty<TableGrid, int?> PageCountProperty =
        AvaloniaProperty.RegisterDirect<TableGrid, int?>(nameof(PageCount), grid => grid.PageCount);

    /// <summary>Defines the <see cref="TotalCount"/> property.</summary>
    public static readonly DirectProperty<TableGrid, int?> TotalCountProperty =
        AvaloniaProperty.RegisterDirect<TableGrid, int?>(nameof(TotalCount), grid => grid.TotalCount);

    /// <summary>Defines the <see cref="HasMore"/> property.</summary>
    public static readonly DirectProperty<TableGrid, bool> HasMoreProperty =
        AvaloniaProperty.RegisterDirect<TableGrid, bool>(nameof(HasMore), grid => grid.HasMore);

    /// <summary>Defines the <see cref="IsLoading"/> property.</summary>
    public static readonly DirectProperty<TableGrid, bool> IsLoadingProperty =
        AvaloniaProperty.RegisterDirect<TableGrid, bool>(nameof(IsLoading), grid => grid.IsLoading);

    /// <summary>Defines the <see cref="Error"/> property.</summary>
    public static readonly DirectProperty<TableGrid, Error?> ErrorProperty =
        AvaloniaProperty.RegisterDirect<TableGrid, Error?>(nameof(Error), grid => grid.Error);

    private readonly TableGridPager _pager = new();
    private readonly Panel _overlay = new();
    private readonly ProgressBar _progress = new();
    private readonly StackPanel _messagePanel = new();
    private readonly TextBlock _message = new();
    private readonly Button _retry = new();
    private readonly Dictionary<int, string> _cursors = [];
    private readonly HashSet<object> _unlocked = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<DataGridRow> _rows = [];
    private readonly ConditionalWeakTable<DataGridRow, string[]> _rowClasses = new();
    private IPageItems? _pageItems;
    private IDataGridCollectionView? _view;
    private TableSource? _listening;
    private bool _attached;
    private bool _settingItemsSource;
    private int _seenChanges;
    private CancellationTokenSource? _loadCancellation;
    private int _loadVersion;
    private int? _pendingPage;
    private int? _failedPage;
    private int _shownPageSize = 25;
    private int _rowsShown;
    private bool _updatingPageSizes;
    private bool _lockColumnPosted;
    private bool _sortPosted;
    private bool _rewritingSort;
    private bool _pageFixPosted;
    private (DataGridColumn? Column, string? Key, bool Descending) _sort;
    private object? _editingItem;
    private bool _editingCell;

    static TableGrid()
    {
        IsReadOnlyProperty.OverrideDefaultValue<TableGrid>(true);
    }

    /// <summary>Creates an empty, read-only table.</summary>
    public TableGrid()
    {
        LockColumn = new TableGridLockColumn(this);

        _pager.ButtonFirst.Click += (_, _) => _ = GoToPageAsync(1);
        _pager.ButtonPrevious.Click += (_, _) => _ = GoToPageAsync(Page - 1);
        _pager.ButtonNext.Click += (_, _) => _ = GoToPageAsync(Page + 1);
        _pager.ButtonLast.Click += (_, _) => _ = GoToPageAsync(PageCount ?? Page);
        _pager.ButtonRefresh.Click += (_, _) => _ = RefreshAsync();
        _pager.TextPage.KeyDown += TextPage_OnKeyDown;
        _pager.TextPage.LostFocus += (_, _) => CommitPageText();
        _pager.ComboPageSize.SelectionChanged += ComboPageSize_OnSelectionChanged;

        _progress.IsIndeterminate = true;
        _progress.IsHitTestVisible = false;
        _progress.VerticalAlignment = VerticalAlignment.Top;
        _progress.MinHeight = 3;
        _message.TextWrapping = TextWrapping.Wrap;
        _message.TextAlignment = TextAlignment.Center;
        _message.MaxWidth = 420;
        _retry.Content = "Retry";
        _retry.HorizontalAlignment = HorizontalAlignment.Center;
        _retry.Click += (_, _) => _ = LoadAsync(_failedPage ?? Page);
        _messagePanel.Spacing = 8;
        _messagePanel.Margin = new Thickness(16);
        _messagePanel.HorizontalAlignment = HorizontalAlignment.Center;
        _messagePanel.VerticalAlignment = VerticalAlignment.Center;
        _messagePanel.Children.Add(_message);
        _messagePanel.Children.Add(_retry);
        _overlay.Children.Add(_progress);
        _overlay.Children.Add(_messagePanel);

        Columns.CollectionChanged += (_, _) => PostEnsureLockColumn();
        AddHandler(KeyDownEvent, TableGrid_OnKeyDown, RoutingStrategies.Tunnel);
        DoubleTapped += TableGrid_OnDoubleTapped;
        UpdatePageSizes();
        UpdatePager();
    }

    /// <summary>
    /// Raised when a row is double-tapped, or Enter is pressed on the selected row, unless the row can be edited (Enter
    /// then moves on, as in any DataGrid) or a cell is being edited.
    /// </summary>
    public event EventHandler<TableGridItemEventArgs>? ItemActivated;

    /// <summary>Raised when a row is locked or unlocked, for the lock buttons.</summary>
    internal event Action? LockChanged;

    /// <summary>
    /// Where the rows come from when they're loaded a page at a time, or null to use <c>ItemsSource</c>. Setting it
    /// sets <c>ItemsSource</c> to the page shown and goes to the first page; setting <c>ItemsSource</c> clears it.
    /// </summary>
    public TableSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>
    /// How many rows a page has. Defaults to 25; must be at least 1. Changing it keeps the first row shown on screen,
    /// except for a <see cref="Source"/> that can't jump to pages, which goes back to the first page.
    /// </summary>
    public int PageSize
    {
        get => GetValue(PageSizeProperty);
        set => SetValue(PageSizeProperty, value);
    }

    /// <summary>The choices in the rows-per-page box. Defaults to 10, 25, 50 and 100; <see cref="PageSize"/> is added if missing.</summary>
    public IReadOnlyList<int> PageSizes
    {
        get => GetValue(PageSizesProperty);
        set => SetValue(PageSizesProperty, value);
    }

    /// <summary>
    /// Whether the pager shows. Defaults to true. Without it, rows from <c>ItemsSource</c> aren't paged; a
    /// <see cref="Source"/> still loads a page at a time, and <see cref="GoToPageAsync"/> moves between them.
    /// </summary>
    public bool ShowPager
    {
        get => GetValue(ShowPagerProperty);
        set => SetValue(ShowPagerProperty, value);
    }

    /// <summary>Whether the pager has the rows-per-page box. Defaults to true.</summary>
    public bool ShowPageSizes
    {
        get => GetValue(ShowPageSizesProperty);
        set => SetValue(ShowPageSizesProperty, value);
    }

    /// <summary>What shows over the rows when there are none. Defaults to "Nothing to show."; empty shows nothing.</summary>
    public string EmptyText
    {
        get => GetValue(EmptyTextProperty);
        set => SetValue(EmptyTextProperty, value);
    }

    /// <summary>
    /// Whether rows are locked until unlocked, when the table can be edited (<c>IsReadOnly</c> is false): a locked
    /// row's cells don't go into edit mode. Rows are unlocked with their lock button (see
    /// <see cref="ShowLockButtons"/>) or <see cref="UnlockRow"/>. Defaults to false. Turning it on or off locks every row.
    /// </summary>
    public bool LockRows
    {
        get => GetValue(LockRowsProperty);
        set => SetValue(LockRowsProperty, value);
    }

    /// <summary>
    /// Whether <see cref="LockRows"/> adds <see cref="LockColumn"/>, a first column with a lock button on each row.
    /// Defaults to true; turn it off to lock and unlock rows from code, e.g. from your own button.
    /// </summary>
    public bool ShowLockButtons
    {
        get => GetValue(ShowLockButtonsProperty);
        set => SetValue(ShowLockButtonsProperty, value);
    }

    /// <summary>
    /// The column of lock buttons, added as the first column while <see cref="LockRows"/> and
    /// <see cref="ShowLockButtons"/> are on and the table can be edited. Move it with its <c>DisplayIndex</c>, or
    /// style it, e.g. its <c>Width</c>.
    /// </summary>
    public DataGridColumn LockColumn { get; }

    /// <summary>
    /// Whether a row may be unlocked, or null (the default) to allow every row. A row it refuses keeps a disabled
    /// lock button, and <see cref="UnlockRow"/> returns false for it.
    /// </summary>
    public Func<object, bool>? CanUnlockRow { get; set; }

    /// <summary>
    /// Style classes for a row, space-separated, or null; e.g. <c>item =&gt; ((Issue)item).IsClosed ? "muted" : null</c>.
    /// Applied as rows load, so set it before the rows, or call <see cref="RefreshRowClasses"/> after. Unlocked rows
    /// also have the <c>unlocked</c> class.
    /// </summary>
    public Func<object, string?>? RowClass { get; set; }

    /// <summary>The page shown, from 1.</summary>
    public int Page
    {
        get;
        private set => SetAndRaise(PageProperty, ref field, value);
    } = 1;

    /// <summary>How many pages there are, or null while a <see cref="Source"/> hasn't said (it's known on the last page).</summary>
    public int? PageCount
    {
        get;
        private set => SetAndRaise(PageCountProperty, ref field, value);
    } = 1;

    /// <summary>How many rows there are across all pages, or null if the <see cref="Source"/> doesn't say.</summary>
    public int? TotalCount
    {
        get;
        private set => SetAndRaise(TotalCountProperty, ref field, value);
    }

    /// <summary>Whether there's a page after this one.</summary>
    public bool HasMore
    {
        get;
        private set => SetAndRaise(HasMoreProperty, ref field, value);
    }

    /// <summary>Whether a page is loading from the <see cref="Source"/>.</summary>
    public bool IsLoading
    {
        get;
        private set => SetAndRaise(IsLoadingProperty, ref field, value);
    }

    /// <summary>Why the last load from the <see cref="Source"/> failed, or null if it didn't.</summary>
    public Error? Error
    {
        get;
        private set => SetAndRaise(ErrorProperty, ref field, value);
    }

    /// <inheritdoc />
    protected override Type StyleKeyOverride => typeof(DataGrid);

    private bool IsRemote => Source is not null && _pageItems is not null && ReferenceEquals(ItemsSource, _pageItems);

    /// <summary>
    /// Shows the rows again: a <see cref="Source"/> loads the page shown again, and rows from <c>ItemsSource</c> are
    /// sorted and filtered again. The task completes when the page has loaded, failed (see <see cref="Error"/>) or
    /// been replaced by another load; it doesn't throw.
    /// </summary>
    /// <returns>The load.</returns>
    public Task RefreshAsync()
    {
        if (IsRemote)
        {
            return LoadAsync(Page);
        }

        EndEdit();
        CollectionView?.Refresh();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Goes to a page, committing any edit first. Pages under 1 go to the first, and past <see cref="PageCount"/> to
    /// the last. A <see cref="Source"/> that can't jump to pages is read forward from the last page the table has a
    /// cursor for. Doesn't throw.
    /// </summary>
    /// <param name="page">The page, from 1.</param>
    /// <returns>The load; already complete for rows from <c>ItemsSource</c>.</returns>
    public Task GoToPageAsync(int page)
    {
        if (IsRemote)
        {
            return LoadAsync(PageCount is int count ? Math.Min(page, count) : page);
        }

        if (CollectionView is DataGridCollectionView { PageSize: > 0 } view)
        {
            EndEdit();
            view.MoveToPage(Math.Clamp(page, 1, PageCount ?? 1) - 1);
        }

        return Task.CompletedTask;
    }

    /// <summary>Whether a row is unlocked for editing. See <see cref="LockRows"/>.</summary>
    /// <param name="item">The row's item.</param>
    /// <returns>True if it's been unlocked and not locked again.</returns>
    public bool IsRowUnlocked(object item) => _unlocked.Contains(item);

    /// <summary>Unlocks a row for editing, as its lock button does. See <see cref="LockRows"/>.</summary>
    /// <param name="item">The row's item.</param>
    /// <returns>False if <see cref="CanUnlockRow"/> refused it.</returns>
    public bool UnlockRow(object item)
    {
        if (CanUnlockRow?.Invoke(item) == false)
        {
            return false;
        }

        if (_unlocked.Add(item))
        {
            OnLockChanged();
        }

        return true;
    }

    /// <summary>Locks a row again, committing its edit, or cancelling it if it doesn't commit.</summary>
    /// <param name="item">The row's item.</param>
    public void LockRow(object item)
    {
        if (ReferenceEquals(_editingItem, item))
        {
            EndEdit();
        }

        if (_unlocked.Remove(item))
        {
            OnLockChanged();
        }
    }

    /// <summary>Locks every row, committing any edit first.</summary>
    public void LockAllRows()
    {
        if (_editingItem is not null)
        {
            EndEdit();
        }

        if (_unlocked.Count > 0)
        {
            _unlocked.Clear();
            OnLockChanged();
        }
    }

    /// <summary>Applies <see cref="RowClass"/> to the rows shown again, e.g. after the rows' items changed.</summary>
    public void RefreshRowClasses()
    {
        foreach (DataGridRow row in _rows)
        {
            ApplyRowClass(row);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ItemsSourceProperty && !_settingItemsSource && Source is not null)
        {
            // Rows set directly replace the source.
            _pageItems = null;
            SetCurrentValue(SourceProperty, null);
        }

        base.OnPropertyChanged(change);
        if (change.Property == SourceProperty)
        {
            OnSourceChanged();
        }
        else if (change.Property == CollectionViewProperty)
        {
            OnCollectionViewChanged();
        }
        else if (change.Property == PageSizeProperty)
        {
            OnPageSizeChanged(change.GetOldValue<int>());
        }
        else if (change.Property == PageSizesProperty)
        {
            UpdatePageSizes();
        }
        else if (change.Property == ShowPagerProperty)
        {
            ApplyViewPageSize();
            UpdateFromView();
        }
        else if (change.Property == ShowPageSizesProperty || change.Property == EmptyTextProperty)
        {
            UpdatePager();
        }
        else if (change.Property == LockRowsProperty)
        {
            _unlocked.Clear();
            OnLockingChanged();
        }
        else if (change.Property == IsReadOnlyProperty || change.Property == ShowLockButtonsProperty)
        {
            OnLockingChanged();
        }
    }

    /// <inheritdoc />
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);
        (_pager.Parent as Panel)?.Children.Remove(_pager);
        (_overlay.Parent as Panel)?.Children.Remove(_overlay);

        // The rows presenter sits in the template's grid: the overlay goes over it, and the pager in a new last row.
        Control? presenter = e.NameScope.Find<Control>("PART_RowsPresenter");
        if (presenter?.Parent is not Grid grid)
        {
            return;
        }

        Grid.SetRow(_overlay, Grid.GetRow(presenter));
        Grid.SetColumn(_overlay, Grid.GetColumn(presenter));
        Grid.SetColumnSpan(_overlay, Grid.GetColumnSpan(presenter));
        grid.Children.Add(_overlay);

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(_pager, grid.RowDefinitions.Count - 1);
        Grid.SetColumnSpan(_pager, Math.Max(1, grid.ColumnDefinitions.Count));
        grid.Children.Add(_pager);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        Listen();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        Listen();
    }

    /// <inheritdoc />
    protected override void OnLoadingRow(DataGridRowEventArgs e)
    {
        base.OnLoadingRow(e);
        _rows.Add(e.Row);
        ApplyRowClass(e.Row);
    }

    /// <inheritdoc />
    protected override void OnUnloadingRow(DataGridRowEventArgs e)
    {
        base.OnUnloadingRow(e);
        _rows.Remove(e.Row);
    }

    /// <inheritdoc />
    protected override void OnBeginningEdit(DataGridBeginningEditEventArgs e)
    {
        if (LockRows && e.Row.DataContext is { } item && !IsRowUnlocked(item))
        {
            e.Cancel = true;
            return;
        }

        base.OnBeginningEdit(e);
        if (!e.Cancel)
        {
            _editingItem = e.Row.DataContext;
        }
    }

    /// <inheritdoc />
    protected override void OnPreparingCellForEdit(DataGridPreparingCellForEditEventArgs e)
    {
        _editingCell = true;
        base.OnPreparingCellForEdit(e);
    }

    /// <inheritdoc />
    protected override void OnCellEditEnding(DataGridCellEditEndingEventArgs e)
    {
        base.OnCellEditEnding(e);
        if (!e.Cancel && e.EditAction == DataGridEditAction.Commit && e.Column is TableGridColumn column
            && e.Row.DataContext is { } item)
        {
            // TableGridColumn edits have no binding: the column's Edit applies the text, and a throw keeps the cell open.
            e.Cancel = !column.CommitEdit(item, e.EditingElement);
        }
    }

    /// <inheritdoc />
    protected override void OnCellEditEnded(DataGridCellEditEndedEventArgs e)
    {
        _editingCell = false;
        base.OnCellEditEnded(e);
    }

    /// <inheritdoc />
    protected override void OnRowEditEnded(DataGridRowEditEndedEventArgs e)
    {
        _editingCell = false;
        _editingItem = null;
        base.OnRowEditEnded(e);
    }

    /// <inheritdoc />
    protected override void OnColumnSorting(DataGridColumnEventArgs e)
    {
        if (IsRemote && Source is { CanSort: false })
        {
            e.Handled = true;
            return;
        }

        base.OnColumnSorting(e);
    }

    /// <inheritdoc />
    protected override void OnAutoGeneratingColumn(DataGridAutoGeneratingColumnEventArgs e)
    {
        if (Equals(e.Column.Header, e.PropertyName))
        {
            e.Column.Header = WordBoundary().Replace(e.PropertyName, " ");
        }

        base.OnAutoGeneratingColumn(e);
    }

    /// <summary>Raises <see cref="ItemActivated"/>.</summary>
    internal void Activate(object item) => ItemActivated?.Invoke(this, new TableGridItemEventArgs(item));

    /// <summary>Whether a cell of the row could go into edit mode now.</summary>
    internal bool CanEditRow(object item) => !IsReadOnly && (!LockRows || IsRowUnlocked(item))
        && Columns.Any(column => column.IsVisible && !column.IsReadOnly);

    /// <summary>The row's style classes, as loading the row sets them. Internal for tests.</summary>
    internal void ApplyRowClass(DataGridRow row)
    {
        if (_rowClasses.TryGetValue(row, out string[]? previous))
        {
            row.Classes.RemoveAll(previous);
        }

        string[] classes = row.DataContext is { } item && RowClass?.Invoke(item) is { } names
            ? names.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            : [];
        row.Classes.AddRange(classes);
        _rowClasses.AddOrUpdate(row, classes);
        row.Classes.Set("unlocked", row.DataContext is { } shown && IsRowUnlocked(shown));
    }

    /// <summary>The pager's state, for tests.</summary>
    internal (bool First, bool Previous, bool Next, bool Last, string PageText, string PageCountText, string Range, string? Message, bool Retry) PagerState =>
        (_pager.ButtonFirst.IsEnabled, _pager.ButtonPrevious.IsEnabled, _pager.ButtonNext.IsEnabled, _pager.ButtonLast.IsEnabled,
            _pager.TextPage.Text ?? string.Empty, _pager.TextPageCount.Text ?? string.Empty, _pager.TextRange.Text ?? string.Empty,
            _messagePanel.IsVisible ? _message.Text : null, _retry.IsVisible);

    /// <summary>Whether the pager is in the template, for tests.</summary>
    internal bool HasPager => _pager.Parent is not null;

    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();

    private static string? BindingPath(DataGridColumn column) => (column as DataGridBoundColumn)?.Binding switch
    {
        CompiledBinding compiled => compiled.Path?.ToString(),
        ReflectionBinding reflection => reflection.Path,
        _ => null,
    };

    /// <summary>The path the grid's own sort uses for a column: its SortMemberPath, else its binding's path.</summary>
    private static string? SortPath(DataGridColumn column) =>
        string.IsNullOrEmpty(column.SortMemberPath) ? BindingPath(column) : column.SortMemberPath;

    /// <summary>Commits the edit in progress, or cancels it if it doesn't commit.</summary>
    private void EndEdit()
    {
        if (!CommitEdit(DataGridEditingUnit.Row, true))
        {
            CancelEdit(DataGridEditingUnit.Row);
        }
    }

    private void OnLockingChanged()
    {
        PostEnsureLockColumn();
        OnLockChanged();
    }

    private void OnLockChanged()
    {
        foreach (DataGridRow row in _rows)
        {
            row.Classes.Set("unlocked", row.DataContext is { } item && IsRowUnlocked(item));
        }

        LockChanged?.Invoke();
    }

    /// <summary>Adds or removes the lock column once the current changes are done; Columns can't change while it's raising.</summary>
    private void PostEnsureLockColumn()
    {
        if (_lockColumnPosted)
        {
            return;
        }

        _lockColumnPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _lockColumnPosted = false;
            bool wanted = !IsReadOnly && LockRows && ShowLockButtons;
            bool present = Columns.Contains(LockColumn);
            if (wanted && !present)
            {
                Columns.Insert(0, LockColumn);
            }
            else if (!wanted && present)
            {
                Columns.Remove(LockColumn);
            }
        });
    }

    private void OnSourceChanged()
    {
        _loadCancellation?.Cancel();
        _loadVersion++;
        _pendingPage = null;
        _failedPage = null;
        _cursors.Clear();
        _sort = default;
        _unlocked.Clear();
        IsLoading = false;
        Error = null;

        if (Source is { } source)
        {
            _pageItems = source.CreatePageItems();
            SetItemsSource(_pageItems);
            _seenChanges = source.ChangeCount;
            Page = 1;
            RequestLoad(1);
        }
        else if (_pageItems is not null)
        {
            if (ReferenceEquals(ItemsSource, _pageItems))
            {
                SetItemsSource(null);
            }

            _pageItems = null;
        }

        Listen();
        UpdateFromView();
    }

    private void SetItemsSource(IEnumerable? items)
    {
        _settingItemsSource = true;
        try
        {
            SetCurrentValue(ItemsSourceProperty, items);
        }
        finally
        {
            _settingItemsSource = false;
        }
    }

    private void OnCollectionViewChanged()
    {
        if (_view is not null)
        {
            _view.CollectionChanged -= View_OnCollectionChanged;
            if (_view is INotifyPropertyChanged oldNotifier)
            {
                oldNotifier.PropertyChanged -= View_OnPropertyChanged;
            }

            _view.SortDescriptions?.CollectionChanged -= SortDescriptions_OnCollectionChanged;
        }

        _view = CollectionView;
        if (_view is not null)
        {
            _view.CollectionChanged += View_OnCollectionChanged;
            if (_view is INotifyPropertyChanged notifier)
            {
                notifier.PropertyChanged += View_OnPropertyChanged;
            }

            _view.SortDescriptions?.CollectionChanged += SortDescriptions_OnCollectionChanged;
        }

        if (!IsRemote)
        {
            _unlocked.Clear();
            OnLockChanged();
        }

        ApplyViewPageSize();
        UpdateFromView();
    }

    /// <summary>Pages rows from ItemsSource through the grid's own collection view.</summary>
    private void ApplyViewPageSize()
    {
        if (IsRemote || CollectionView is not DataGridCollectionView view)
        {
            return;
        }

        int size = ShowPager ? PageSize : 0;
        if (view.PageSize != size)
        {
            EndEdit();
            view.PageSize = size;
        }
    }

    private void OnPageSizeChanged(int oldSize)
    {
        UpdatePageSizes();
        if (IsRemote)
        {
            int first = (Page - 1) * _shownPageSize;
            _cursors.Clear();
            RequestLoad(Source!.CanJumpToPage ? first / PageSize + 1 : 1);
        }
        else if (CollectionView is DataGridCollectionView { PageSize: > 0 } view)
        {
            int first = Math.Max(view.PageIndex, 0) * oldSize;
            ApplyViewPageSize();
            view.MoveToPage(first / PageSize);
            UpdateFromView();
        }
    }

    private void View_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateFromView();

    private void View_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(DataGridCollectionView.PageIndex) or nameof(DataGridCollectionView.ItemCount)
            or nameof(DataGridCollectionView.PageSize))
        {
            UpdateFromView();
        }
    }

    /// <summary>Reads the paging state of rows from ItemsSource off the grid's collection view.</summary>
    private void UpdateFromView()
    {
        if (IsRemote)
        {
            UpdatePager();
            return;
        }

        IDataGridCollectionView? view = CollectionView;
        int total;
        if (view is DataGridCollectionView paged)
        {
            total = paged.ItemCount;
            int size = paged.PageSize;
            if (size > 0)
            {
                int count = Math.Max(1, (total + size - 1) / size);
                int index = Math.Max(paged.PageIndex, 0);
                if (index >= count)
                {
                    PostMoveToLastPage();
                }

                Page = Math.Min(index, count - 1) + 1;
                PageCount = count;
                _shownPageSize = size;
                _rowsShown = Math.Clamp(total - (Page - 1) * size, 0, size);
            }
            else
            {
                Page = 1;
                PageCount = 1;
                _rowsShown = total;
            }
        }
        else
        {
            total = view?.Cast<object>().Count() ?? 0;
            Page = 1;
            PageCount = 1;
            _rowsShown = total;
        }

        TotalCount = view is null ? null : total;
        HasMore = Page < (PageCount ?? Page);
        UpdatePager();
    }

    /// <summary>Moves back to the last page after rows were removed from under the one shown.</summary>
    private void PostMoveToLastPage()
    {
        if (_pageFixPosted)
        {
            return;
        }

        _pageFixPosted = true;
        Dispatcher.UIThread.Post(() =>
        {
            _pageFixPosted = false;
            if (!IsRemote && CollectionView is DataGridCollectionView { PageSize: > 0 } view)
            {
                int count = Math.Max(1, (view.ItemCount + view.PageSize - 1) / view.PageSize);
                if (view.PageIndex >= count)
                {
                    EndEdit();
                    view.MoveToPage(count - 1);
                }
            }
        });
    }

    private void SortDescriptions_OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_rewritingSort || _sortPosted || !IsRemote)
        {
            return;
        }

        // The header changes the descriptions inside a deferred refresh; read them once it's done.
        _sortPosted = true;
        Dispatcher.UIThread.Post(ApplyRemoteSort);
    }

    /// <summary>
    /// Turns the grid's sort into a sort for the source: the grid only shows the order the source returns, and the
    /// first sort description becomes the request's sort.
    /// </summary>
    private void ApplyRemoteSort()
    {
        _sortPosted = false;
        if (!IsRemote || CollectionView is not { SortDescriptions: { } descriptions } view)
        {
            return;
        }

        DataGridSortDescription? description = descriptions.FirstOrDefault();
        DataGridColumn? column = description switch
        {
            DataGridComparerSortDescription comparer => Columns.FirstOrDefault(c => ReferenceEquals(c.CustomSortComparer, comparer.SourceComparer)),
            { HasPropertyPath: true } path => Columns.FirstOrDefault(c => SortPath(c) == path.PropertyPath),
            _ => null,
        };
        string? key = column is null ? description?.PropertyPath : SortKey(column) ?? description?.PropertyPath;
        bool descending = description?.Direction == ListSortDirection.Descending;
        DataGridSortDescription? keepOrder = description switch
        {
            KeepOrderPathSort or KeepOrderComparerSort => description,
            DataGridComparerSortDescription comparer => new KeepOrderComparerSort(comparer.SourceComparer, description.Direction),
            { HasPropertyPath: true } => new KeepOrderPathSort(description.PropertyPath, description.Direction),
            _ => null,
        };
        if (descriptions.Count != (keepOrder is null ? 0 : 1) || !ReferenceEquals(keepOrder, description))
        {
            _rewritingSort = true;
            try
            {
                using (view.DeferRefresh())
                {
                    descriptions.Clear();
                    if (keepOrder is not null)
                    {
                        descriptions.Add(keepOrder);
                    }
                }
            }
            finally
            {
                _rewritingSort = false;
            }
        }

        if (key is null)
        {
            column = null;
            descending = false;
        }

        if (_sort != (column, key, descending))
        {
            _sort = (column, key, descending);
            _cursors.Clear();
            RequestLoad(1);
        }
    }

    /// <summary>What a source sorts by for a column: its SortMemberPath, else its binding's path, else its header text.</summary>
    private static string? SortKey(DataGridColumn column) => SortPath(column) ?? column.Header switch
    {
        string text when text.Length > 0 => text,
        _ => null,
    };

    private void Listen()
    {
        TableSource? source = _attached && IsRemote ? Source : null;
        if (ReferenceEquals(source, _listening))
        {
            return;
        }

        if (_listening is not null)
        {
            _listening.Changed -= Source_OnChanged;
        }

        _listening = source;
        if (source is not null)
        {
            source.Changed += Source_OnChanged;
            if (source.ChangeCount != _seenChanges)
            {
                // It changed while the table wasn't showing.
                _seenChanges = source.ChangeCount;
                RequestLoad(Page);
            }
        }
    }

    private void Source_OnChanged(bool reset) => Dispatcher.UIThread.Post(() =>
    {
        if (!IsRemote)
        {
            return;
        }

        _seenChanges = Source!.ChangeCount;
        if (reset)
        {
            _cursors.Clear();
        }

        RequestLoad(reset ? 1 : Page);
    });

    /// <summary>Loads a page once the current property changes are done, so setting several loads once.</summary>
    private void RequestLoad(int page)
    {
        bool scheduled = _pendingPage is not null;
        _pendingPage = page;
        if (!scheduled)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_pendingPage is int pending)
                {
                    _ = LoadAsync(pending);
                }
            });
        }
    }

    private async Task LoadAsync(int page)
    {
        _pendingPage = null;
        _loadCancellation?.Cancel();
        CancellationTokenSource cancellation = new();
        _loadCancellation = cancellation;
        int version = ++_loadVersion;
        page = Math.Max(1, page);

        if (!IsRemote)
        {
            return;
        }

        TableSource source = Source!;
        IsLoading = true;
        Error = null;
        UpdatePager();
        try
        {
            bool showedLast = false;
            while (true)
            {
                // A cursor-based source is read forward from the last page there's a cursor for.
                int start = source.CanJumpToPage ? page : _cursors.Keys.Where(known => known <= page).Append(1).Max();
                int pageSize = PageSize;
                (DataGridColumn? sortColumn, string? sortKey, bool sortDescending) = _sort;
                ItemPage<object> loaded = ItemPage<object>.Empty;
                int loadedPage = start;
                for (int current = start; ; current++)
                {
                    TablePageRequest request = new()
                    {
                        Page = current,
                        PageSize = pageSize,
                        Cursor = _cursors.GetValueOrDefault(current),
                        SortColumn = sortColumn,
                        SortKey = sortKey,
                        SortDescending = sortDescending,
                    };
                    Result<ItemPage<object>> result = await source.LoadAsync(request, cancellation.Token);
                    if (version != _loadVersion)
                    {
                        return;
                    }

                    if (!result.TryGetValue(out loaded, out Error? error))
                    {
                        ShowError(error!, page);
                        return;
                    }

                    if (loaded.NextCursor is { } next)
                    {
                        _cursors[current + 1] = next;
                    }

                    loadedPage = current;
                    if (current >= page || !loaded.HasMore)
                    {
                        break;
                    }
                }

                // Past the end, e.g. after rows were removed: show the last page instead.
                if (loaded.Items.Count == 0 && loadedPage > 1 && !showedLast)
                {
                    showedLast = true;
                    page = Math.Min(loaded.PageCount ?? loadedPage - 1, loadedPage - 1);
                    continue;
                }

                Show(loaded, loadedPage, pageSize);
                return;
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer load replaced this one.
        }
        catch (Exception exception)
        {
            if (version == _loadVersion)
            {
                ShowError(Error.FromException(exception), page);
            }
        }
    }

    private void Show(ItemPage<object> page, int number, int pageSize)
    {
        EndEdit();
        _failedPage = null;
        _shownPageSize = pageSize;
        _rowsShown = page.Items.Count;
        _unlocked.Clear();
        _pageItems!.ReplaceAll(page.Items);
        OnLockChanged();
        Page = number;
        HasMore = page.HasMore;
        TotalCount = page.TotalCount;
        PageCount = page.PageCount ?? (page.HasMore ? null : number);
        Error = null;
        IsLoading = false;
        if (page.Items.Count > 0)
        {
            ScrollIntoView(page.Items[0], null!);
        }

        UpdatePager();
    }

    private void ShowError(Error error, int page)
    {
        EndEdit();
        _failedPage = page;
        _rowsShown = 0;
        _unlocked.Clear();
        _pageItems?.ReplaceAll([]);
        Error = error;
        IsLoading = false;
        UpdatePager();
    }

    private void UpdatePageSizes()
    {
        _updatingPageSizes = true;
        _pager.ComboPageSize.ItemsSource = PageSizes.Append(PageSize).Where(size => size >= 1).Distinct().Order().ToList();
        _pager.ComboPageSize.SelectedItem = PageSize;
        _updatingPageSizes = false;
    }

    private void UpdatePager()
    {
        bool remote = IsRemote;
        bool canJump = !remote || Source!.CanJumpToPage;
        CultureInfo culture = CultureInfo.CurrentCulture;
        _pager.IsVisible = ShowPager;
        _pager.PageSizePanel.IsVisible = ShowPageSizes;
        _pager.ButtonFirst.IsEnabled = Page > 1;
        _pager.ButtonPrevious.IsEnabled = Page > 1;
        _pager.ButtonNext.IsEnabled = HasMore;
        _pager.ButtonLast.IsEnabled = canJump && PageCount is int count && Page < count;
        _pager.ButtonRefresh.IsEnabled = remote || CollectionView is not null;
        _pager.TextPage.IsReadOnly = !canJump;
        _pager.TextPage.Text = Page.ToString(culture);
        _pager.TextPageCount.Text = PageCount is int pages ? string.Format(culture, "of {0:N0}", pages) : string.Empty;
        _pager.TextRange.Text = Range(culture);
        _progress.IsVisible = IsLoading;
        _messagePanel.IsVisible = Error is not null || (!IsLoading && _rowsShown == 0 && !string.IsNullOrEmpty(EmptyText));
        _message.Text = Error?.Message ?? EmptyText;
        _retry.IsVisible = Error is not null;
    }

    private string Range(CultureInfo culture)
    {
        if (_rowsShown == 0)
        {
            return string.Empty;
        }

        int first = (Page - 1) * _shownPageSize + 1;
        int last = first + _rowsShown - 1;
        return TotalCount is int total
            ? string.Format(culture, "{0:N0}–{1:N0} of {2:N0}", first, last, total)
            : string.Format(culture, "{0:N0}–{1:N0}", first, last);
    }

    private void CommitPageText()
    {
        if (int.TryParse(_pager.TextPage.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out int page) && page != Page)
        {
            _ = GoToPageAsync(page);
        }
        else
        {
            _pager.TextPage.Text = Page.ToString(CultureInfo.CurrentCulture);
        }
    }

    private void TableGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        Visual? source = e.Source as Visual;
        if (_editingCell || source?.FindAncestorOfType<Button>(includeSelf: true) is not null)
        {
            return;
        }

        if (source?.FindAncestorOfType<DataGridRow>(includeSelf: true)?.DataContext is { } item)
        {
            Activate(item);
        }
    }

    private void TableGrid_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !_editingCell && SelectedItem is { } item && !CanEditRow(item)
            && (e.Source as Visual)?.FindAncestorOfType<TableGridPager>(includeSelf: true) is null)
        {
            e.Handled = true;
            Activate(item);
        }
    }

    private void TextPage_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            CommitPageText();
        }
    }

    private void ComboPageSize_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingPageSizes && _pager.ComboPageSize.SelectedItem is int size)
        {
            PageSize = size;
        }
    }

    /// <summary>A sort by path that keeps the order the source returned: the header shows it, the source did it.</summary>
    private sealed class KeepOrderPathSort(string propertyPath, ListSortDirection direction) : DataGridSortDescription
    {
        public override string PropertyPath => propertyPath;

        public override ListSortDirection Direction => direction;

        public override IComparer<object> Comparer { get; } = Comparer<object>.Create((_, _) => 0);

        public override DataGridSortDescription SwitchSortDirection() => new KeepOrderPathSort(propertyPath, Flip(direction));
    }

    /// <summary>A column comparer's sort that keeps the order the source returned.</summary>
    private sealed class KeepOrderComparerSort(IComparer comparer, ListSortDirection direction)
        : DataGridComparerSortDescription(comparer, direction)
    {
        public override IComparer<object> Comparer { get; } = Comparer<object>.Create((_, _) => 0);

        public override DataGridSortDescription SwitchSortDirection() => new KeepOrderComparerSort(SourceComparer, Flip(Direction));
    }

    private static ListSortDirection Flip(ListSortDirection direction) =>
        direction == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
}
