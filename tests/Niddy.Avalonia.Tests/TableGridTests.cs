using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Niddy.Avalonia.Tables;
using Niddy.Core.Collections;
using Niddy.Core.Results;

namespace Niddy.Avalonia.Tests;

public class TableGridTests
{
    private sealed record Person(string Name, int Age, DateTime? CreatedAt = null);

    private sealed class Editable(string name, int quantity)
    {
        public string Name { get; set; } = name;

        public int Quantity { get; set; } = quantity;
    }

    private static List<Person> People(int count) =>
        Enumerable.Range(1, count).Select(i => new Person($"P{i:000}", 100 - i)).ToList();

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static TableGrid Shown(TableGrid grid)
    {
        Window window = new() { Width = 900, Height = 700 };
        // The test app has no DataGrid theme, and without one the grid shows no rows. The theme has to be loaded
        // before the grid is added.
        window.Styles.Add(new StyleInclude((Uri?)null)
        {
            Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        });
        window.Show();
        window.Content = grid;
        Pump();
        return grid;
    }

    /// <summary>The rows on the page shown.</summary>
    private static List<object> Rows(TableGrid grid) => grid.CollectionView?.Cast<object>().ToList() ?? [];

    private static List<string> Names(TableGrid grid) => Rows(grid).Cast<Person>().Select(p => p.Name).ToList();

    private static TableGridPager Pager(TableGrid grid) => grid.GetVisualDescendants().OfType<TableGridPager>().Single();

    private static void Key(Control target, Key key) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    private static PagedTableSource<Person> Pages(int total, List<TablePageRequest>? requests = null, bool canSort = false) =>
        new((request, _) =>
        {
            requests?.Add(request);
            List<Person> people = People(total);
            if (request.SortKey is not null)
            {
                people = request.SortDescending ? people.OrderByDescending(p => p.Name).ToList() : people.OrderBy(p => p.Name).ToList();
            }

            List<Person> page = people.Skip(request.Skip).Take(request.PageSize).ToList();
            return Task.FromResult<Result<ItemPage<Person>>>(new ItemPage<Person>
            {
                Items = page,
                HasMore = request.Skip + page.Count < total,
                TotalCount = total,
                PageCount = Math.Max(1, (total + request.PageSize - 1) / request.PageSize),
            });
        }, canSort: canSort);

    // ─── In memory ───

    [AvaloniaFact]
    public async Task Items_ArePagedAndCounted()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 25, ItemsSource = People(60) });

        Assert.True(grid.IsReadOnly);
        Assert.True(grid.HasPager);
        Assert.Equal(25, Rows(grid).Count);
        Assert.Equal(1, grid.Page);
        Assert.Equal(3, grid.PageCount);
        Assert.Equal(60, grid.TotalCount);
        Assert.True(grid.HasMore);
        Assert.Equal("1–25 of 60", grid.PagerState.Range);
        Assert.False(grid.PagerState.First);
        Assert.True(grid.PagerState.Last);

        await grid.GoToPageAsync(3);
        Assert.Equal(3, grid.Page);
        Assert.Equal(10, Rows(grid).Count);
        Assert.Equal("P051", Names(grid)[0]);
        Assert.False(grid.HasMore);
        Assert.Equal("51–60 of 60", grid.PagerState.Range);
        Assert.Equal("3", grid.PagerState.PageText);
        Assert.Equal("of 3", grid.PagerState.PageCountText);
        Assert.False(grid.PagerState.Next);

        await grid.GoToPageAsync(99);
        Assert.Equal(3, grid.Page);
        await grid.GoToPageAsync(-4);
        Assert.Equal(1, grid.Page);
    }

    [AvaloniaFact]
    public void PagerButtons_MovePages()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 10, ItemsSource = People(35) });
        TableGridPager pager = Pager(grid);

        Click(pager.ButtonNext);
        Pump();
        Assert.Equal(2, grid.Page);

        Click(pager.ButtonLast);
        Pump();
        Assert.Equal(4, grid.Page);
        Assert.Equal(5, Rows(grid).Count);

        Click(pager.ButtonPrevious);
        Pump();
        Assert.Equal(3, grid.Page);

        Click(pager.ButtonFirst);
        Pump();
        Assert.Equal(1, grid.Page);
    }

    [AvaloniaFact]
    public void PageBox_GoesToTheTypedPage()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 10, ItemsSource = People(50) });
        TextBox box = Pager(grid).TextPage;

        box.Text = "4";
        Key(box, global::Avalonia.Input.Key.Enter);
        Pump();
        Assert.Equal(4, grid.Page);

        box.Text = "nope";
        Key(box, global::Avalonia.Input.Key.Enter);
        Pump();
        Assert.Equal(4, grid.Page);
        Assert.Equal("4", box.Text);
    }

    [AvaloniaFact]
    public void PageSizeBox_ListsTheSizesAndChangesThePageSize()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 30, PageSizes = [10, 50], ItemsSource = People(100) });
        ComboBox combo = Pager(grid).ComboPageSize;

        Assert.Equal([10, 30, 50], combo.ItemsSource!.Cast<int>());
        Assert.Equal(30, combo.SelectedItem);

        combo.SelectedItem = 50;
        Pump();
        Assert.Equal(50, grid.PageSize);
        Assert.Equal(50, Rows(grid).Count);
    }

    [AvaloniaFact]
    public async Task ChangingPageSize_KeepsTheFirstRowShown()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 10, ItemsSource = People(100) });
        await grid.GoToPageAsync(4);
        Assert.Equal("P031", Names(grid)[0]);

        grid.PageSize = 25;
        Pump();

        Assert.Equal(2, grid.Page);
        Assert.Equal("P026", Names(grid)[0]);
        Assert.Equal(4, grid.PageCount);
    }

    [AvaloniaFact]
    public void PageSize_UnderOneIsRejected()
    {
        TableGrid grid = new();

        Assert.Throws<ArgumentException>(() => grid.PageSize = 0);
    }

    [AvaloniaFact]
    public void WithoutThePager_EveryRowShows()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 10, ShowPager = false, ItemsSource = People(35) });

        Assert.Equal(35, Rows(grid).Count);
        Assert.Equal(1, grid.PageCount);
        Assert.False(Pager(grid).IsVisible);

        grid.ShowPager = true;
        Pump();
        Assert.Equal(10, Rows(grid).Count);
        Assert.Equal(4, grid.PageCount);
    }

    [AvaloniaFact]
    public async Task Sorting_SortsEveryRowNotJustThePage()
    {
        TableGridColumn<Person> age = new("Age", p => p.Age);
        TableGrid grid = new() { PageSize = 5, AutoGenerateColumns = false, ItemsSource = People(12) };
        grid.Columns.Add(new TableGridColumn<Person>("Name", p => p.Name));
        grid.Columns.Add(age);
        Shown(grid);
        await grid.GoToPageAsync(2);

        age.Sort(ListSortDirection.Ascending);
        Pump();

        // The youngest is on the last page unsorted.
        Assert.Equal("P012", Names(grid)[0]);
        Assert.Equal(1, grid.Page);

        age.Sort(ListSortDirection.Descending);
        Pump();
        Assert.Equal("P001", Names(grid)[0]);
    }

    [AvaloniaFact]
    public void Filter_RecountsThePages()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 10, ItemsSource = People(30) });

        grid.CollectionView!.Filter = p => ((Person)p).Age % 2 == 0;
        Pump();

        Assert.Equal(15, grid.TotalCount);
        Assert.Equal(2, grid.PageCount);
        Assert.Equal("1–10 of 15", grid.PagerState.Range);
    }

    [AvaloniaFact]
    public void ObservableItems_UpdateTheCounts()
    {
        ObservableCollection<Person> people = [.. People(3)];
        TableGrid grid = Shown(new TableGrid { ItemsSource = people });

        people.Add(new Person("New", 1));
        Pump();

        Assert.Equal(4, Rows(grid).Count);
        Assert.Equal(4, grid.TotalCount);
    }

    [AvaloniaFact]
    public async Task RemovingRows_ShowsTheLastPageInsteadOfAnEmptyOne()
    {
        ObservableCollection<Person> people = [.. People(30)];
        TableGrid grid = Shown(new TableGrid { PageSize = 10, ItemsSource = people });
        await grid.GoToPageAsync(3);

        for (int i = 0; i < 15; i++)
        {
            people.RemoveAt(people.Count - 1);
        }

        Pump();

        Assert.Equal(2, grid.Page);
        Assert.Equal(5, Rows(grid).Count);
        Assert.Equal(2, grid.PageCount);
    }

    [AvaloniaFact]
    public void Empty_ShowsTheEmptyText()
    {
        TableGrid grid = Shown(new TableGrid { EmptyText = "No orders.", ItemsSource = new List<Person>() });

        Assert.Equal("No orders.", grid.PagerState.Message);
        Assert.False(grid.PagerState.Retry);
        Assert.Equal(1, grid.PageCount);
        Assert.Equal("", grid.PagerState.Range);

        grid.EmptyText = "";
        Assert.Null(grid.PagerState.Message);
    }

    [AvaloniaFact]
    public void GeneratedColumns_SplitPropertyNames()
    {
        TableGrid grid = Shown(new TableGrid { AutoGenerateColumns = true, ItemsSource = People(2) });

        Assert.Equal(["Name", "Age", "Created At"], grid.Columns.Select(c => c.Header as string));
    }

    [AvaloniaFact]
    public void Enter_ActivatesTheSelectedItem()
    {
        List<Person> people = People(3);
        TableGrid grid = Shown(new TableGrid { ItemsSource = people });
        object? activated = null;
        grid.ItemActivated += (_, e) => activated = e.Item;

        grid.SelectedItem = people[1];
        Pump();
        Key(grid, global::Avalonia.Input.Key.Enter);

        Assert.Same(people[1], activated);
    }

    [AvaloniaFact]
    public void Rows_ShowFormattedCellsAndRowClasses()
    {
        TableGrid grid = new() { AutoGenerateColumns = false, RowClass = item => ((Person)item).Age > 50 ? "old" : null };
        grid.Columns.Add(new TableGridColumn<Person>("Name", p => p.Name));
        grid.Columns.Add(new TableGridColumn<Person>("Age", p => p.Age) { FormatString = "000" });
        grid.ItemsSource = new List<Person> { new("Ada", 36), new("Grace", 85) };
        Shown(grid);
        grid.UpdateLayout();

        List<DataGridRow> rows = grid.GetVisualDescendants().OfType<DataGridRow>().OrderBy(r => r.Index).ToList();
        Assert.Equal(2, rows.Count);
        Assert.DoesNotContain("old", rows[0].Classes);
        Assert.Contains("old", rows[1].Classes);
        List<string?> texts = rows[1].GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Contains("Grace", texts);
        Assert.Contains("085", texts);
    }

    [AvaloniaFact]
    public void RefreshRowClasses_AppliesTheFunctionAgain()
    {
        bool muted = false;
        TableGrid grid = Shown(new TableGrid { RowClass = _ => muted ? "muted" : null, ItemsSource = People(2) });
        grid.UpdateLayout();
        List<DataGridRow> rows = grid.GetVisualDescendants().OfType<DataGridRow>().ToList();
        Assert.All(rows, row => Assert.DoesNotContain("muted", row.Classes));

        muted = true;
        grid.RefreshRowClasses();

        Assert.All(rows, row => Assert.Contains("muted", row.Classes));
    }

    // ─── Editing and locking ───

    private static (TableGrid Grid, List<Editable> Items, TableGridColumn<Editable> Quantity) EditableGrid(bool lockRows)
    {
        List<Editable> items = [new("Bolts", 10), new("Nuts", 20)];
        TableGridColumn<Editable> quantity = new("Quantity", e => e.Quantity) { Edit = (e, text) => e.Quantity = int.Parse(text, CultureInfo.CurrentCulture) };
        TableGrid grid = new() { IsReadOnly = false, LockRows = lockRows, AutoGenerateColumns = false, ItemsSource = items };
        grid.Columns.Add(new TableGridColumn<Editable>("Name", e => e.Name));
        grid.Columns.Add(quantity);
        Shown(grid);
        grid.UpdateLayout();
        return (grid, items, quantity);
    }

    private static bool TryBeginEdit(TableGrid grid, object item, DataGridColumn column)
    {
        grid.SelectedItem = item;
        grid.CurrentColumn = column;
        Pump();
        bool editing = grid.BeginEdit();
        grid.CancelEdit();
        return editing;
    }

    [AvaloniaFact]
    public void ReadOnly_IsTheDefaultAndStopsEditing()
    {
        (TableGrid grid, List<Editable> items, TableGridColumn<Editable> quantity) = EditableGrid(lockRows: false);
        Assert.True(TryBeginEdit(grid, items[0], quantity));

        grid.IsReadOnly = true;

        Assert.False(TryBeginEdit(grid, items[0], quantity));
        Assert.True(new TableGrid().IsReadOnly);
    }

    [AvaloniaFact]
    public void LockedRows_DontEditUntilUnlocked()
    {
        (TableGrid grid, List<Editable> items, TableGridColumn<Editable> quantity) = EditableGrid(lockRows: true);

        Assert.False(TryBeginEdit(grid, items[0], quantity));

        Assert.True(grid.UnlockRow(items[0]));
        Assert.True(grid.IsRowUnlocked(items[0]));
        Assert.True(TryBeginEdit(grid, items[0], quantity));
        Assert.False(TryBeginEdit(grid, items[1], quantity));

        grid.LockRow(items[0]);
        Assert.False(TryBeginEdit(grid, items[0], quantity));

        grid.UnlockRow(items[0]);
        grid.UnlockRow(items[1]);
        grid.LockAllRows();
        Assert.False(grid.IsRowUnlocked(items[0]));
        Assert.False(grid.IsRowUnlocked(items[1]));
    }

    [AvaloniaFact]
    public void LockButtons_AreAddedAsTheFirstColumnAndToggleTheRow()
    {
        (TableGrid grid, List<Editable> items, _) = EditableGrid(lockRows: true);

        Assert.Same(grid.LockColumn, grid.Columns[0]);
        DataGridRow row = grid.GetVisualDescendants().OfType<DataGridRow>().Single(r => ReferenceEquals(r.DataContext, items[1]));
        Button button = row.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "LockButton");

        Click(button);
        Assert.True(grid.IsRowUnlocked(items[1]));
        Assert.Contains("unlocked", row.Classes);
        Assert.Contains("unlocked", button.Classes);

        Click(button);
        Assert.False(grid.IsRowUnlocked(items[1]));
        Assert.DoesNotContain("unlocked", row.Classes);

        grid.ShowLockButtons = false;
        Pump();
        Assert.DoesNotContain(grid.LockColumn, grid.Columns);

        grid.ShowLockButtons = true;
        grid.IsReadOnly = true;
        Pump();
        Assert.DoesNotContain(grid.LockColumn, grid.Columns);
    }

    [AvaloniaFact]
    public void CanUnlockRow_RefusesRowsAndDisablesTheirButtons()
    {
        (TableGrid grid, List<Editable> items, _) = EditableGrid(lockRows: true);
        grid.CanUnlockRow = item => ((Editable)item).Name != "Nuts";
        grid.LockAllRows();

        Assert.False(grid.UnlockRow(items[1]));
        Assert.False(grid.IsRowUnlocked(items[1]));
        Assert.True(grid.UnlockRow(items[0]));
        DataGridRow row = grid.GetVisualDescendants().OfType<DataGridRow>().Single(r => ReferenceEquals(r.DataContext, items[1]));
        Assert.False(row.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "LockButton").IsEnabled);
    }

    [AvaloniaFact]
    public void TurningLockRowsOff_LetsEveryRowEditAndForgetsUnlocks()
    {
        (TableGrid grid, List<Editable> items, TableGridColumn<Editable> quantity) = EditableGrid(lockRows: true);
        grid.UnlockRow(items[0]);

        grid.LockRows = false;
        Pump();

        Assert.False(grid.IsRowUnlocked(items[0]));
        Assert.DoesNotContain(grid.LockColumn, grid.Columns);
        Assert.True(TryBeginEdit(grid, items[1], quantity));
    }

    [AvaloniaFact]
    public void ColumnEdit_AppliesTheTextOrKeepsTheCellOpen()
    {
        Editable bolts = new("Bolts", 10);
        TableGridColumn<Editable> quantity = new("Quantity", e => e.Quantity) { Edit = (e, text) => e.Quantity = int.Parse(text, CultureInfo.InvariantCulture) };

        Assert.True(quantity.CommitEdit(bolts, new TextBox { Text = "42" }));
        Assert.Equal(42, bolts.Quantity);

        TextBox bad = new() { Text = "lots" };
        Assert.False(quantity.CommitEdit(bolts, bad));
        Assert.Equal(42, bolts.Quantity);
        Assert.True(DataValidationErrors.GetHasErrors(bad));
    }

    [AvaloniaFact]
    public void ColumnsWithoutAnEdit_AreReadOnly()
    {
        TableGridColumn<Editable> name = new("Name", e => e.Name);
        TableGridColumn<Editable> quantity = new("Quantity", e => e.Quantity) { Edit = (_, _) => { } };

        Assert.True(name.IsReadOnly);
        name.IsReadOnly = false;
        Assert.True(name.IsReadOnly);
        Assert.False(quantity.IsReadOnly);
        quantity.IsReadOnly = true;
        Assert.True(quantity.IsReadOnly);
    }

    [AvaloniaFact]
    public void Enter_OnAnEditableRow_DoesntActivate()
    {
        (TableGrid grid, List<Editable> items, _) = EditableGrid(lockRows: true);
        int activated = 0;
        grid.ItemActivated += (_, _) => activated++;
        grid.SelectedItem = items[0];
        Pump();

        Key(grid, global::Avalonia.Input.Key.Enter);
        Assert.Equal(1, activated);

        grid.UnlockRow(items[0]);
        Key(grid, global::Avalonia.Input.Key.Enter);
        Assert.Equal(1, activated);
    }

    // ─── A source ───

    [AvaloniaFact]
    public async Task Source_LoadsEachPage()
    {
        List<TablePageRequest> requests = [];
        TableGrid grid = Shown(new TableGrid { PageSize = 50, Source = Pages(1000, requests) });
        Pump();

        Assert.Equal(50, Rows(grid).Count);
        Assert.Equal(20, grid.PageCount);
        Assert.Equal(1000, grid.TotalCount);
        Assert.Equal(string.Format(CultureInfo.CurrentCulture, "1–50 of {0:N0}", 1000), grid.PagerState.Range);

        await grid.GoToPageAsync(3);
        Assert.Equal(3, grid.Page);
        Assert.Equal("P101", Names(grid)[0]);
        Assert.Equal(1, requests[0].Page);
        Assert.Equal(50, requests[0].PageSize);
        Assert.Null(requests[0].SortKey);
        Assert.Equal(3, requests[1].Page);
        Assert.Equal(100, requests[1].Skip);
        Assert.Equal(2, requests.Count);

        await grid.GoToPageAsync(99);
        Assert.Equal(20, grid.Page);
    }

    [AvaloniaFact]
    public void Source_SettingItemsSourceReplacesIt()
    {
        TableGrid grid = Shown(new TableGrid { Source = Pages(10) });
        Pump();

        grid.ItemsSource = People(3);
        Pump();

        Assert.Null(grid.Source);
        Assert.Equal(3, Rows(grid).Count);
    }

    [AvaloniaFact]
    public void Source_SortingAHeaderAsksTheSource()
    {
        List<TablePageRequest> requests = [];
        TableGridColumn<Person> name = new("Name", p => p.Name) { SortMemberPath = "name" };
        TableGrid grid = new() { PageSize = 5, AutoGenerateColumns = false };
        grid.Columns.Add(name);
        grid.Source = Pages(12, requests, canSort: true);
        Shown(grid);
        Pump();

        name.Sort(ListSortDirection.Descending);
        Pump();

        TablePageRequest sorted = requests[^1];
        Assert.Equal(1, sorted.Page);
        Assert.Equal("name", sorted.SortKey);
        Assert.Same(name, sorted.SortColumn);
        Assert.True(sorted.SortDescending);
        // The grid shows the source's order rather than sorting the page again.
        Assert.Equal("P012", Names(grid)[0]);
        Assert.Equal(2, requests.Count);
    }

    [AvaloniaFact]
    public void Source_SortKeyFallsBackToTheHeader()
    {
        List<TablePageRequest> requests = [];
        TableGridColumn<Person> name = new("Name", p => p.Name);
        TableGrid grid = new() { AutoGenerateColumns = false };
        grid.Columns.Add(name);
        grid.Source = Pages(3, requests, canSort: true);
        Shown(grid);
        Pump();

        name.Sort(ListSortDirection.Ascending);
        Pump();

        Assert.Equal("Name", requests[^1].SortKey);
        Assert.False(requests[^1].SortDescending);
    }

    [AvaloniaFact]
    public void Source_ThatCantSort_IgnoresHeaders()
    {
        List<TablePageRequest> requests = [];
        TableGridColumn<Person> name = new("Name", p => p.Name);
        TableGrid grid = new() { AutoGenerateColumns = false };
        grid.Columns.Add(name);
        grid.Source = Pages(3, requests);
        Shown(grid);
        Pump();

        name.Sort(ListSortDirection.Descending);
        Pump();

        Assert.Single(requests);
        Assert.Equal("P001", Names(grid)[0]);
    }

    [AvaloniaFact]
    public void Source_ResetGoesBackToTheFirstPageAndRefreshReloads()
    {
        List<TablePageRequest> requests = [];
        PagedTableSource<Person> source = Pages(100, requests);
        TableGrid grid = Shown(new TableGrid { PageSize = 10, Source = source });
        Pump();
        grid.GoToPageAsync(4);
        Pump();

        source.Refresh();
        Pump();
        Assert.Equal(4, requests[^1].Page);

        source.Reset();
        Pump();
        Assert.Equal(1, requests[^1].Page);
        Assert.Equal(1, grid.Page);
    }

    [AvaloniaFact]
    public void Source_ChangesWhileDetachedReloadOnAttach()
    {
        List<TablePageRequest> requests = [];
        PagedTableSource<Person> source = Pages(10, requests);
        TableGrid grid = Shown(new TableGrid { Source = source });
        Window window = Assert.IsType<Window>(TopLevel.GetTopLevel(grid));
        Pump();
        int loads = requests.Count;

        window.Content = null;
        Pump();
        source.Refresh();
        Pump();
        Assert.Equal(loads, requests.Count);

        window.Content = grid;
        Pump();
        Assert.Equal(loads + 1, requests.Count);
    }

    [AvaloniaFact]
    public void Source_ChangingPageSizeKeepsTheFirstRowShown()
    {
        TableGrid grid = Shown(new TableGrid { PageSize = 10, Source = Pages(100) });
        Pump();
        grid.GoToPageAsync(4);
        Pump();

        grid.PageSize = 25;
        Pump();

        Assert.Equal(2, grid.Page);
        Assert.Equal("P026", Names(grid)[0]);
    }

    [AvaloniaFact]
    public void CursorSource_WalksForwardAndReusesCursors()
    {
        List<(int Page, string? Cursor)> requests = [];
        PagedTableSource<Person> source = new((request, _) =>
        {
            requests.Add((request.Page, request.Cursor));
            return Task.FromResult<Result<ItemPage<Person>>>(new ItemPage<Person>
            {
                Items = [new Person($"Page {request.Page}", request.Page)],
                HasMore = request.Page < 5,
                NextCursor = request.Page < 5 ? $"c{request.Page + 1}" : null,
            });
        }, canJumpToPage: false);
        TableGrid grid = Shown(new TableGrid { PageSize = 1, Source = source });
        Pump();

        Assert.Null(grid.PageCount);
        Assert.True(grid.PagerState.Next);
        Assert.False(grid.PagerState.Last);
        Assert.Equal("", grid.PagerState.PageCountText);
        Assert.True(Pager(grid).TextPage.IsReadOnly);

        grid.GoToPageAsync(3);
        Pump();
        Assert.Equal(3, grid.Page);
        Assert.Equal([(1, null), (2, "c2"), (3, "c3")], requests);

        requests.Clear();
        grid.GoToPageAsync(2);
        Pump();
        Assert.Equal([(2, "c2")], requests);

        requests.Clear();
        grid.GoToPageAsync(5);
        Pump();
        Assert.Equal([(4, "c4"), (5, "c5")], requests);
        Assert.Equal(5, grid.PageCount);
        Assert.False(grid.HasMore);
    }

    [AvaloniaFact]
    public void Failure_ShowsTheErrorAndRetryLoadsAgain()
    {
        int calls = 0;
        PagedTableSource<Person> source = new((_, _) => ++calls == 1
            ? Task.FromResult<Result<ItemPage<Person>>>(new Error("Rate limited"))
            : Task.FromResult<Result<ItemPage<Person>>>(new ItemPage<Person> { Items = People(2) }));
        TableGrid grid = Shown(new TableGrid { Source = source });
        Pump();

        Assert.Equal("Rate limited", grid.Error?.Message);
        Assert.Equal("Rate limited", grid.PagerState.Message);
        Assert.True(grid.PagerState.Retry);
        Assert.Empty(Rows(grid));

        Button retry = grid.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "Retry"));
        Click(retry);
        Pump();

        Assert.Null(grid.Error);
        Assert.Equal(2, Rows(grid).Count);
        Assert.Null(grid.PagerState.Message);
    }

    [AvaloniaFact]
    public void Exception_BecomesTheError()
    {
        PagedTableSource<Person> source = new((_, _) => throw new InvalidOperationException("Offline"));
        TableGrid grid = Shown(new TableGrid { Source = source });
        Pump();

        Assert.Equal("Offline", grid.Error?.Message);
        Assert.False(grid.IsLoading);
    }

    [AvaloniaFact]
    public async Task NewerLoad_ReplacesASlowOne()
    {
        TaskCompletionSource<Result<ItemPage<Person>>> slow = new();
        PagedTableSource<Person> source = new((request, _) => request.Page == 2
            ? slow.Task
            : Task.FromResult<Result<ItemPage<Person>>>(new ItemPage<Person> { Items = [new Person($"Page {request.Page}", 0)], HasMore = true }));
        TableGrid grid = Shown(new TableGrid { Source = source });
        Pump();

        Task second = grid.GoToPageAsync(2);
        Assert.True(grid.IsLoading);
        await grid.GoToPageAsync(3);
        slow.SetResult(new ItemPage<Person> { Items = [new Person("Page 2", 0)], HasMore = true });
        await second;

        Assert.Equal(3, grid.Page);
        Assert.Equal(["Page 3"], Names(grid));
        Assert.False(grid.IsLoading);
    }

    [AvaloniaFact]
    public void Source_UnlocksAreForgottenWhenAnotherPageLoads()
    {
        PagedTableSource<Person> source = Pages(20);
        TableGrid grid = Shown(new TableGrid { PageSize = 5, IsReadOnly = false, LockRows = true, Source = source });
        Pump();
        object first = Rows(grid)[0];
        grid.UnlockRow(first);

        grid.GoToPageAsync(2);
        Pump();

        Assert.False(grid.IsRowUnlocked(first));
    }

    // ─── Columns ───

    [AvaloniaFact]
    public void Column_FormatsValues()
    {
        Person ada = new("Ada", 36, new DateTime(2024, 5, 6));
        TextBlock text = new();

        new TableGridColumn<Person>("Age", p => p.Age) { FormatString = "0000" }.UpdateText(text, ada);
        Assert.Equal("0036", text.Text);
        Assert.Equal(HorizontalAlignment.Right, text.HorizontalAlignment);

        new TableGridColumn<Person>("Name", p => p.Name).UpdateText(text, ada);
        Assert.Equal("Ada", text.Text);
        Assert.Equal(HorizontalAlignment.Left, text.HorizontalAlignment);

        new TableGridColumn<Person>("Created", p => p.CreatedAt) { FormatString = "yyyy-MM-dd" }.UpdateText(text, ada);
        Assert.Equal("2024-05-06", text.Text);

        new TableGridColumn<Person>("Created", _ => null) { NullText = "—" }.UpdateText(text, ada);
        Assert.Equal("—", text.Text);

        new TableGridColumn<Person>("Tags", _ => new[] { "a", "b" }) { Alignment = HorizontalAlignment.Center }.UpdateText(text, ada);
        Assert.Equal("a, b", text.Text);
        Assert.Equal(HorizontalAlignment.Center, text.HorizontalAlignment);

        new TableGridColumn<Person>("Who", p => p.Name) { Format = p => $"{p.Name} ({p.Age})" }.UpdateText(text, ada);
        Assert.Equal("Ada (36)", text.Text);
    }

    [AvaloniaFact]
    public void Column_CellClassesFollowTheRow()
    {
        TableGridColumn<Person> column = new("Age", p => p.Age) { CellClass = p => p.Age > 50 ? "old wise" : "young" };
        TextBlock text = new();

        column.UpdateText(text, new Person("Grace", 85));
        Assert.Contains("old", text.Classes);
        Assert.Contains("wise", text.Classes);

        column.UpdateText(text, new Person("Ada", 36));
        Assert.Equal(["young"], text.Classes);
    }

    [AvaloniaFact]
    public void Column_WorksInAPlainDataGrid()
    {
        DataGrid grid = new() { AutoGenerateColumns = false, ItemsSource = new List<Person> { new("Ada", 36) } };
        grid.Columns.Add(new TableGridColumn<Person>("Name", p => p.Name));
        Window window = new() { Width = 600, Height = 400 };
        window.Styles.Add(new StyleInclude((Uri?)null) { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
        window.Show();
        window.Content = grid;
        Pump();
        grid.UpdateLayout();

        Assert.Contains("Ada", grid.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
    }

    [AvaloniaFact]
    public void DefaultComparer_ComparesNumbersAndTextSensibly()
    {
        IComparer<object?> comparer = TableGridColumn.DefaultComparer;

        Assert.True(comparer.Compare(null, 1) < 0);
        Assert.True(comparer.Compare(2, 10) < 0);
        Assert.True(comparer.Compare(2, 10.5) < 0);
        Assert.Equal(0, comparer.Compare("abc", "ABC"));
    }

    [AvaloniaFact]
    public void Column_CustomComparerSortsInMemory()
    {
        // Sort by name length, longest first when ascending.
        TableGridColumn<Person> name = new("Name", p => p.Name)
        {
            Comparer = Comparer<object?>.Create((a, b) => ((string)b!).Length.CompareTo(((string)a!).Length)),
        };
        TableGrid grid = new() { AutoGenerateColumns = false, ItemsSource = new List<Person> { new("Al", 1), new("Grace", 2), new("Ada", 3) } };
        grid.Columns.Add(name);
        Shown(grid);

        name.Sort(ListSortDirection.Ascending);
        Pump();

        Assert.Equal(["Grace", "Ada", "Al"], Names(grid));
    }
}
