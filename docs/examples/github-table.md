# A GitHub table

A table of a repository's issues, and one of a project's items, each loaded a page at a time as the user pages. It uses [TableGrid](../Niddy.Avalonia/Tables/TableGrid.md), [TableGridColumn](../Niddy.Avalonia/Tables/TableGridColumn.md), [PagedTableSource\<T>](../Niddy.Avalonia/Tables/PagedTableSource.md), [GitHubIssues](../Niddy.Core/GitHub/GitHubIssues.md#paging-yourself), [GitHubProjects](../Niddy.Core/GitHub/GitHubProjects.md#paging-yourself) and [UriLauncher](../Niddy.Avalonia/UriLauncher.md).

The app owns the paging: each time the user moves to a page, sorts or changes the page size, the table calls the source's function with a [`TablePageRequest`](../Niddy.Avalonia/Tables/TableSource.md#tablepagerequest). The function reads that one page from GitHub.

## Issues, by page number

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:niddy="https://github.com/vonderborch/Niddy"
             x:Class="MyApp.IssuesView">
    <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,8">
            <ComboBox x:Name="ComboState" SelectedIndex="0" SelectionChanged="ComboState_OnSelectionChanged">
                <ComboBoxItem>Open</ComboBoxItem>
                <ComboBoxItem>Closed</ComboBoxItem>
                <ComboBoxItem>All</ComboBoxItem>
            </ComboBox>
        </StackPanel>
        <niddy:TableGrid x:Name="TableIssues" PageSize="30" EmptyText="No issues." />
    </DockPanel>
</UserControl>
```

```csharp
using Avalonia.Controls;
using Niddy.Avalonia;
using Niddy.Avalonia.Tables;
using Niddy.Core.Collections;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace MyApp;

public partial class IssuesView : UserControl
{
    private const string Repository = "vonderborch/Niddy";
    private readonly GitHubReadOptions _read;
    private readonly PagedTableSource<GitHubIssueInfo> _source;
    private GitHubStateFilter _state = GitHubStateFilter.Open;

    public IssuesView(GitHubReadOptions read)
    {
        InitializeComponent();
        _read = read;

        // GitHub sorts issues by created, updated or comments, so only those columns are sortable. SortMemberPath is
        // what the request's SortKey carries.
        TableIssues.Columns.Add(new TableGridColumn<GitHubIssueInfo>("#", issue => issue.Number)
        {
            Format = issue => $"#{issue.Number}",
            Width = new DataGridLength(70),
            CanUserSort = false,
        });
        TableIssues.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Title", issue => issue.Title)
        {
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            MinWidth = 200,
            CanUserSort = false,
        });
        TableIssues.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Labels", issue => issue.Labels) { CanUserSort = false });
        TableIssues.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Comments", issue => issue.CommentCount)
        {
            SortMemberPath = nameof(GitHubIssueSort.Comments),
        });
        TableIssues.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Created", issue => issue.CreatedAt)
        {
            SortMemberPath = nameof(GitHubIssueSort.Created),
        });
        TableIssues.Columns.Add(new TableGridColumn<GitHubIssueInfo>("Updated", issue => issue.UpdatedAt)
        {
            SortMemberPath = nameof(GitHubIssueSort.Updated),
        });
        TableIssues.RowClass = item => ((GitHubIssueInfo)item).State == GitHubItemState.Closed ? "muted" : null;

        _source = new PagedTableSource<GitHubIssueInfo>(LoadPageAsync, canSort: true);
        TableIssues.Source = _source;
        TableIssues.ItemActivated += TableIssues_OnItemActivated;
    }

    private Task<Result<ItemPage<GitHubIssueInfo>>> LoadPageAsync(TablePageRequest request,
        CancellationToken cancellationToken)
    {
        GitHubIssueFilter filter = new()
        {
            State = _state,
            Sort = Enum.TryParse(request.SortKey, out GitHubIssueSort sort) ? sort : GitHubIssueSort.Created,
            Ascending = request.SortKey is not null && !request.SortDescending,
        };
        return GitHubIssues.ListPageAsync(Repository, filter, request.Page, request.PageSize, _read,
            cancellationToken: cancellationToken);
    }

    private void ComboState_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _state = ComboState.SelectedIndex switch
        {
            1 => GitHubStateFilter.Closed,
            2 => GitHubStateFilter.All,
            _ => GitHubStateFilter.Open,
        };
        _source?.Reset();   // back to page 1 with the new filter (null while InitializeComponent runs)
    }

    private async void TableIssues_OnItemActivated(object? sender, TableGridItemEventArgs e)
    {
        if (e.Item is GitHubIssueInfo { Url: { } url })
        {
            await UriLauncher.TryLaunchAsync(this, url.AbsoluteUri);
        }
    }
}
```

```xml
<!-- App.axaml: closed issues greyed out -->
<Style Selector="DataGridRow.muted">
    <Setter Property="Opacity" Value="0.6" />
</Style>
```

GitHub's issue pages say how many pages there are (from the `Link` header) but not how many issues, so the pager shows "page 2 of 7" and "31–60". Pull requests are left out after a page is read, so a page can hold fewer than 30. Set `IncludePullRequests` for full pages.

## Project items, by cursor

GitHub's GraphQL API reads the page after one it has read, so the source is created with `canJumpToPage: false`. The table passes each page's `NextCursor` back as the next request's `Cursor`, and keeps the cursors it has seen so Previous and First work. The page box is read-only and Last is off. GitHub gives the total, so the pager still shows "of N".

```csharp
TableGrid items = new() { PageSize = 50 };
items.Columns.Add(new TableGridColumn<GitHubProjectItem>("Title", item => item.Title)
{
    Width = new DataGridLength(1, DataGridLengthUnitType.Star),
    NullText = "(no access)",
});
items.Columns.Add(new TableGridColumn<GitHubProjectItem>("Type", item => item.Type));
items.Columns.Add(new TableGridColumn<GitHubProjectItem>("Status", item => item.GetField("Status")));
items.Columns.Add(new TableGridColumn<GitHubProjectItem>("Repository", item => item.Repository));

items.Source = new PagedTableSource<GitHubProjectItem>(
    (request, cancellationToken) => GitHubProjects.ListItemsPageAsync("acme", 5, read, request.PageSize,
        request.Cursor, cancellationToken: cancellationToken),
    canJumpToPage: false);
```

The source isn't created with `canSort: true`, because this API doesn't sort, so clicking a header does nothing.

## Notes

- **The DataGrid theme.** `NiddyApp` adds it. Without `NiddyApp`, add `avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml` to the app's styles, or the table shows no rows.
- **Rate limits.** Each page is one request. Set a `CacheDirectory` on `GitHubReadOptions` so that going back to a page, or Refresh within `CacheDuration`, is served from disk.
- **Search** works the same way through `GitHubIssues.SearchPageAsync(query, request.Page, request.PageSize, read, cancellationToken: cancellationToken)`. Only the first 1,000 results can be read, and `PageCount` stops there. Search is limited to 30 requests a minute, so don't refresh it on every keystroke: call `Reset()` from a [`Debouncer`](../Niddy.Core/Threading/Debouncer.md).
- **The table is read-only** by default, so double-tapping or pressing Enter on a row raises `ItemActivated` rather than editing a cell.
- **Errors** (no network, a bad token, the rate limit) show in the table with a Retry button. Nothing throws.

## See also

- [TableGrid](../Niddy.Avalonia/Tables/TableGrid.md), [ItemPage\<T>](../Niddy.Core/Collections/ItemPage.md)
- [GitHubReadOptions](../Niddy.Core/GitHub/GitHubReadOptions.md)
