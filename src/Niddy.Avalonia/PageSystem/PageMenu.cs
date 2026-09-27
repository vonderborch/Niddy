using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.Utilities;
using DockSide = Avalonia.Controls.Dock;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     A navigation menu for a <see cref="PageView"/>: lists the child pages of <see cref="ParentPage"/> (or the
///     top-level pages) with their icons, highlights the one being shown, and navigates <see cref="Target"/> when one
///     is picked. It updates as pages are registered, e.g. when a plugin loads.
///     <para>
///         <see cref="Style"/> is how it looks (a vertical or horizontal strip, a navigation bar, or a menu button
///         that opens a flyout) and <see cref="Placement"/> is where, on any of the 8 positions around the page; the
///         placement picks the edge and where along it the items sit, as <see cref="PageMenuStyle"/> describes. On
///         phones and narrow windows it switches to <see cref="NarrowStyle"/> and <see cref="NarrowPlacement"/> (a
///         bottom navigation bar by default). Styles can target the <c>:verticalstrip</c>, <c>:horizontalstrip</c>,
///         <c>:navigationbar</c>, <c>:menubutton</c> and <c>:tree</c> pseudo-classes.
///     </para>
///     <para>
///         Set <see cref="Content"/> to the page view (or whatever holds it) and the menu arranges itself around it.
///         Without content, put the menu in a <c>DockPanel</c> next to the page view: it docks itself to match its
///         placement unless <c>DockPanel.Dock</c> is set on it.
///     </para>
///     <para>
///         With <see cref="Mode"/> set to <see cref="PageMenuMode.Tree"/>, a vertical strip or the menu button's flyout shows
///         the whole page tree as expandable items and navigates nested page views to reach a page deep in the tree.
///     </para>
///     Usage in AXAML:
///     <code>
///         &lt;pageSystem:PageMenu Style="VerticalStrip" Placement="TopLeft" Mode="Tree"&gt;
///             &lt;pageSystem:PageView StartingPage="{x:Type local:HomePage}" /&gt;
///         &lt;/pageSystem:PageMenu&gt;
///     </code>
/// </summary>
public class PageMenu : Control
{
    /// <summary>Defines the <see cref="Content"/> property.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<PageMenu, Control?>(nameof(Content));

    /// <summary>Defines the <see cref="Target"/> property.</summary>
    public static readonly StyledProperty<PageView?> TargetProperty =
        AvaloniaProperty.Register<PageMenu, PageView?>(nameof(Target));

    /// <summary>Defines the <see cref="ParentPage"/> property.</summary>
    public static readonly StyledProperty<Type?> ParentPageProperty =
        AvaloniaProperty.Register<PageMenu, Type?>(nameof(ParentPage));

    /// <summary>Defines the <see cref="Style"/> property.</summary>
    public static readonly StyledProperty<PageMenuStyle> StyleProperty =
        AvaloniaProperty.Register<PageMenu, PageMenuStyle>(nameof(Style));

    /// <summary>Defines the <see cref="NarrowStyle"/> property.</summary>
    public static readonly StyledProperty<PageMenuStyle?> NarrowStyleProperty =
        AvaloniaProperty.Register<PageMenu, PageMenuStyle?>(nameof(NarrowStyle), PageMenuStyle.NavigationBar);

    /// <summary>Defines the <see cref="Placement"/> property.</summary>
    public static readonly StyledProperty<ScreenPlacement> PlacementProperty =
        AvaloniaProperty.Register<PageMenu, ScreenPlacement>(nameof(Placement), ScreenPlacement.TopLeft);

    /// <summary>Defines the <see cref="NarrowPlacement"/> property.</summary>
    public static readonly StyledProperty<ScreenPlacement?> NarrowPlacementProperty =
        AvaloniaProperty.Register<PageMenu, ScreenPlacement?>(nameof(NarrowPlacement), ScreenPlacement.BottomCenter);

    /// <summary>Defines the <see cref="NarrowWidth"/> property.</summary>
    public static readonly StyledProperty<double> NarrowWidthProperty =
        AvaloniaProperty.Register<PageMenu, double>(nameof(NarrowWidth), 600);

    /// <summary>Defines the <see cref="Mode"/> property.</summary>
    public static readonly StyledProperty<PageMenuMode> ModeProperty =
        AvaloniaProperty.Register<PageMenu, PageMenuMode>(nameof(Mode));

    /// <summary>Defines the <see cref="AddToBackStack"/> property.</summary>
    public static readonly StyledProperty<bool> AddToBackStackProperty =
        AvaloniaProperty.Register<PageMenu, bool>(nameof(AddToBackStack));

    /// <summary>Defines the <see cref="ItemTemplate"/> property.</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<PageMenu, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>Defines the <see cref="ActualStyle"/> property.</summary>
    public static readonly DirectProperty<PageMenu, PageMenuStyle> ActualStyleProperty =
        AvaloniaProperty.RegisterDirect<PageMenu, PageMenuStyle>(nameof(ActualStyle), menu => menu.ActualStyle);

    /// <summary>Defines the <see cref="ActualPlacement"/> property.</summary>
    public static readonly DirectProperty<PageMenu, ScreenPlacement> ActualPlacementProperty =
        AvaloniaProperty.RegisterDirect<PageMenu, ScreenPlacement>(nameof(ActualPlacement), menu => menu.ActualPlacement);

    /// <summary>Defines the <see cref="SelectedItem"/> property.</summary>
    public static readonly DirectProperty<PageMenu, PageMenuItem?> SelectedItemProperty =
        AvaloniaProperty.RegisterDirect<PageMenu, PageMenuItem?>(
            nameof(SelectedItem),
            menu => menu.SelectedItem,
            (menu, item) => menu.SelectedItem = item
        );

    // Deep enough for any real menu; guards against runaway trees.
    private const int MaxDepth = 16;
    private const double Indent = 16;

    private static readonly Geometry MenuGeometry =
        Geometry.Parse("M3,6H21V8H3V6M3,11H21V13H3V11M3,16H21V18H3V16Z");

    private static readonly FuncTemplate<Panel?> BarPanel = new(() => new UniformGrid { Rows = 1 });

    private readonly ObservableCollection<PageMenuItem> _items = [];
    private readonly HashSet<string> _expanded = [];
    private readonly Dictionary<PageId, bool> _hostsChildren = [];
    private readonly List<PageControlDataContext> _watched = [];
    private readonly DockPanel _frame = new();
    private readonly PageMenuList _list;
    private readonly Panel _buttonBar = new();
    private readonly Button _button;
    private readonly Border _flyoutHost = new();
    private readonly Flyout _flyout;

    private IReadOnlyList<PageMenuItem> _roots = [];
    private (PageMenuStyle, bool, IDataTemplate?)? _templateKey;
    private (PageMenuStyle, ScreenPlacement)? _panelKey;
    private bool _isMobile;
    private bool _layoutApplied;
    private bool _syncing;
    private bool _navigating;
    private TopLevel? _topLevel;
    private IDisposable? _dock;
    private IDisposable? _flyoutBackRegistration;

    /// <summary>Creates a menu for <see cref="Target"/>.</summary>
    public PageMenu()
    {
        Items = new ReadOnlyObservableCollection<PageMenuItem>(_items);

        _list = new PageMenuList(this) { ItemsSource = _items, SelectionMode = SelectionMode.Single };
        _list.SelectionChanged += OnListSelectionChanged;
        _list.AddHandler(TappedEvent, OnListTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        _list.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);

        _flyout = new Flyout { Content = _flyoutHost };
        _flyout.Opened += OnFlyoutOpened;
        _flyout.Closed += OnFlyoutClosed;
        _button = new Button
        {
            Content = new PathIcon { Data = MenuGeometry, Width = 20, Height = 20 },
            Margin = new Thickness(8),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Flyout = _flyout,
        };
        AutomationProperties.SetName(_button, "Menu");
        ToolTip.SetTip(_button, "Menu");
        _buttonBar.Children.Add(_button);

        LogicalChildren.Add(_frame);
        VisualChildren.Add(_frame);
        ApplyLayout(force: true);
    }

    /// <summary>
    ///     The page area the menu arranges itself around: usually the <see cref="PageView"/>, or a panel holding it.
    ///     Without content, the menu is just the menu itself, to put in a layout yourself.
    /// </summary>
    [Content]
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    ///     The page view the menu navigates and follows. If not set, <see cref="Content"/> if it's a page view, or
    ///     else the <see cref="NiddyApp"/>'s page view.
    /// </summary>
    public PageView? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>
    ///     The type of the page whose child pages the menu lists, e.g. <c>{x:Type local:SettingsPage}</c>, or null (the
    ///     default) for the top-level pages. It doesn't have to match the <see cref="Target"/>'s parent page.
    /// </summary>
    public Type? ParentPage
    {
        get => GetValue(ParentPageProperty);
        set => SetValue(ParentPageProperty, value);
    }

    /// <summary>
    ///     How the menu shows its items: a vertical or horizontal strip, a navigation bar, or a menu button. Defaults
    ///     to <see cref="PageMenuStyle.VerticalStrip"/>, a sidebar.
    /// </summary>
    public PageMenuStyle Style
    {
        get => GetValue(StyleProperty);
        set => SetValue(StyleProperty, value);
    }

    /// <summary>
    ///     How the menu shows its items in mobile mode or when the window is narrower than <see cref="NarrowWidth"/>.
    ///     Defaults to <see cref="PageMenuStyle.NavigationBar"/>; null always uses <see cref="Style"/>.
    /// </summary>
    public PageMenuStyle? NarrowStyle
    {
        get => GetValue(NarrowStyleProperty);
        set => SetValue(NarrowStyleProperty, value);
    }

    /// <summary>
    ///     Where the menu goes, on any of the 8 positions around the page. It picks the edge <see cref="Style"/>
    ///     docks to and where along that edge the items sit, as <see cref="PageMenuStyle"/> describes. Defaults to
    ///     <see cref="ScreenPlacement.TopLeft"/>: a sidebar on the left with its items from the top.
    /// </summary>
    public ScreenPlacement Placement
    {
        get => GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>
    ///     Where the menu goes in mobile mode or when the window is narrower than <see cref="NarrowWidth"/>. Defaults
    ///     to <see cref="ScreenPlacement.BottomCenter"/>, within thumb reach; null always uses <see cref="Placement"/>.
    /// </summary>
    public ScreenPlacement? NarrowPlacement
    {
        get => GetValue(NarrowPlacementProperty);
        set => SetValue(NarrowPlacementProperty, value);
    }

    /// <summary>
    ///     The window width below which <see cref="NarrowStyle"/> and <see cref="NarrowPlacement"/> are used. Defaults
    ///     to 600.
    /// </summary>
    public double NarrowWidth
    {
        get => GetValue(NarrowWidthProperty);
        set => SetValue(NarrowWidthProperty, value);
    }

    /// <summary>
    ///     Whether the menu lists one level of pages or the whole tree. Defaults to <see cref="PageMenuMode.Flat"/>.
    ///     The tree shows in a vertical strip or the menu button's flyout; a horizontal strip or navigation bar lists
    ///     the top level only.
    /// </summary>
    public PageMenuMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    /// <summary>
    ///     Whether picking a page adds the current one to the back stack. Defaults to false, like tabs: going back
    ///     doesn't retrace every switch.
    /// </summary>
    public bool AddToBackStack
    {
        get => GetValue(AddToBackStackProperty);
        set => SetValue(AddToBackStackProperty, value);
    }

    /// <summary>
    ///     The template for each <see cref="PageMenuItem"/>. If not set, each shows its icon and display name, with
    ///     indentation and an expander in a tree.
    /// </summary>
    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    /// <summary>The style in use: <see cref="Style"/> or <see cref="NarrowStyle"/>.</summary>
    public PageMenuStyle ActualStyle
    {
        get;
        private set => SetAndRaise(ActualStyleProperty, ref field, value);
    }

    /// <summary>The placement in use: <see cref="Placement"/> or <see cref="NarrowPlacement"/>.</summary>
    public ScreenPlacement ActualPlacement
    {
        get;
        private set => SetAndRaise(ActualPlacementProperty, ref field, value);
    } = ScreenPlacement.TopLeft;

    /// <summary>The items shown: the top level, plus the children of expanded items in a tree.</summary>
    public ReadOnlyObservableCollection<PageMenuItem> Items { get; }

    /// <summary>
    ///     The item for the page being shown, or for its nearest ancestor in the menu. If it's inside a collapsed
    ///     item, that item is highlighted instead. Setting it navigates to the item's page.
    /// </summary>
    public PageMenuItem? SelectedItem
    {
        get;
        set
        {
            if (_syncing)
            {
                SetAndRaise(SelectedItemProperty, ref field, value);
                return;
            }
            if (value is not null && !ReferenceEquals(value, field))
                Navigate(value);
        }
    }

    /// <summary>Whether a phone layout is forced, regardless of <see cref="NiddyApp.CurrentMode"/>.</summary>
    internal bool IsMobile
    {
        get => _isMobile;
        set
        {
            _isMobile = value;
            ApplyLayout(force: true);
        }
    }

    internal ListBox List => _list;
    internal Button MenuButton => _button;
    internal Flyout MenuFlyout => _flyout;

    /// <summary>The page view the menu navigates: <see cref="Target"/>, the content, or the app's page view.</summary>
    private PageView? View => Target ?? Content as PageView ?? NiddyApp.Current?.ShellOrNull?.Pages;

    private bool Touch => _isMobile || NiddyApp.CurrentMode == AppMode.Mobile;

    private bool Narrow => Touch || _topLevel is { } topLevel && topLevel.Bounds.Width > 0 && topLevel.Bounds.Width < NarrowWidth;

    private bool Vertical => ActualStyle is PageMenuStyle.VerticalStrip or PageMenuStyle.MenuButton;

    private bool TreeShown => Mode == PageMenuMode.Tree && Vertical;

    internal double ItemMinHeight => ActualStyle == PageMenuStyle.NavigationBar ? 56 : Touch ? 48 : 0;

    internal HorizontalAlignment ItemContentAlignment => Vertical ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        PageRegistry.PagesChanged += OnPagesChanged;
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        PageRegistry.PagesChanged -= OnPagesChanged;
        Unwatch();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        if (_topLevel is not null)
            _topLevel.SizeChanged += OnTopLevelSizeChanged;
        ApplyLayout(force: true);
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_topLevel is not null)
            _topLevel.SizeChanged -= OnTopLevelSizeChanged;
        _topLevel = null;
        _flyout.Hide();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty)
        {
            if (change.OldValue is Control oldContent)
                _frame.Children.Remove(oldContent);
            if (change.NewValue is Control newContent)
                _frame.Children.Add(newContent);
            if (((ILogical)this).IsAttachedToLogicalTree)
                Refresh();
        }
        else if (change.Property == TargetProperty)
        {
            if (((ILogical)this).IsAttachedToLogicalTree)
                Refresh();
        }
        else if (change.Property == ParentPageProperty)
        {
            Refresh();
        }
        else if (change.Property == ModeProperty)
        {
            ApplyLayout(force: true);
            Refresh();
        }
        else if (change.Property == StyleProperty
                 || change.Property == NarrowStyleProperty
                 || change.Property == PlacementProperty
                 || change.Property == NarrowPlacementProperty
                 || change.Property == NarrowWidthProperty
                 || change.Property == ItemTemplateProperty)
        {
            ApplyLayout(force: true);
        }
    }

    // Pages can be registered from any thread, e.g. when a plugin assembly loads.
    private void OnPagesChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Refresh);

    private void OnTopLevelSizeChanged(object? sender, SizeChangedEventArgs e) => ApplyLayout();

    /// <summary>Rebuilds the items, e.g. after pages are registered or <see cref="ParentPage"/> changes.</summary>
    private void Refresh()
    {
        var parent = ParentPage is { } parentPage ? PageId.Of(parentPage) : null;
        _roots = Build(parent, null, []);
        WatchViews();
        Sync();
    }

    private IReadOnlyList<PageMenuItem> Build(PageId? parentId, PageMenuItem? parent, HashSet<PageId> path)
    {
        var items = new List<PageMenuItem>();
        foreach (var registration in PageRegistry.GetPages(parentId))
        {
            if (path.Contains(registration.PageId))
                continue;
            var item = new PageMenuItem(registration, parent, parentId, OnExpandedChanged);
            if (Mode == PageMenuMode.Tree && item.Depth + 1 < MaxDepth)
            {
                path.Add(registration.PageId);
                item.Children = Build(registration.PageId, item, path);
                path.Remove(registration.PageId);
            }
            item.SetExpanded(_expanded.Contains(item.Key));
            items.Add(item);
        }
        return items;
    }

    private IEnumerable<PageMenuItem> AllItems()
    {
        var stack = new Stack<PageMenuItem>(_roots.Reverse());
        while (stack.TryPop(out var item))
        {
            yield return item;
            for (var i = item.Children.Count - 1; i >= 0; i--)
                stack.Push(item.Children[i]);
        }
    }

    private void OnExpandedChanged(PageMenuItem item)
    {
        if (item.IsExpanded)
            _expanded.Add(item.Key);
        else
            _expanded.Remove(item.Key);
        UpdateItems();
    }

    /// <summary>Updates <see cref="Items"/> to the visible items, and the highlight to match.</summary>
    private void UpdateItems()
    {
        var visible = new List<PageMenuItem>();
        AddVisible(_roots);
        _syncing = true;
        try
        {
            // Insert and remove rather than replace, so expanding keeps the scroll position and highlight.
            for (var i = 0; i < visible.Count; i++)
            {
                if (i < _items.Count && ReferenceEquals(_items[i], visible[i]))
                    continue;
                var existing = IndexOf(visible[i], i + 1);
                if (existing > i)
                {
                    for (var k = existing - 1; k >= i; k--)
                        _items.RemoveAt(k);
                }
                else
                {
                    _items.Insert(i, visible[i]);
                }
            }
            while (_items.Count > visible.Count)
                _items.RemoveAt(_items.Count - 1);
        }
        finally
        {
            _syncing = false;
        }
        Highlight();

        void AddVisible(IReadOnlyList<PageMenuItem> items)
        {
            foreach (var item in items)
            {
                visible.Add(item);
                if (TreeShown && item.IsExpanded)
                    AddVisible(item.Children);
            }
        }
    }

    private int IndexOf(PageMenuItem item, int start)
    {
        for (var i = start; i < _items.Count; i++)
        {
            if (ReferenceEquals(_items[i], item))
                return i;
        }
        return -1;
    }

    /// <summary>Highlights the selected item, or its nearest visible ancestor.</summary>
    private void Highlight()
    {
        var visible = SelectedItem;
        while (visible is not null && IndexOf(visible, 0) < 0)
            visible = visible.Parent;
        _syncing = true;
        try
        {
            _list.SelectedItem = visible;
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Follows the target view and the page views nested in its pages, deepest last.</summary>
    private void WatchViews()
    {
        Unwatch();
        var seen = new HashSet<PageView>();
        for (var view = View; view is not null && seen.Count < MaxDepth && seen.Add(view); view = view.PageControlDataContext.ActivePage?.ChildView)
        {
            view.PageControlDataContext.PropertyChanged += OnViewPropertyChanged;
            _watched.Add(view.PageControlDataContext);
        }
    }

    private void Unwatch()
    {
        foreach (var pages in _watched)
            pages.PropertyChanged -= OnViewPropertyChanged;
        _watched.Clear();
    }

    private void OnViewPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_navigating)
            return;
        if (e.PropertyName == nameof(PageControlDataContext.ActivePage))
        {
            WatchViews();
            Sync();
        }
        else if (e.PropertyName is nameof(PageControlDataContext.CurrentPage) or nameof(PageControlDataContext.CurrentParentPage))
        {
            Sync();
        }
    }

    /// <summary>
    ///     Selects the item for the page being shown, in the deepest nested view that has one: the page itself, or
    ///     else its nearest ancestor in the menu, so a section stays highlighted while one of its pages is shown. In
    ///     a tree, the items leading to it are expanded.
    /// </summary>
    private void Sync()
    {
        if (_navigating)
            return;
        var item = FindCurrentItem();
        if (item is not null && Mode == PageMenuMode.Tree)
        {
            for (var ancestor = item; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor.HasChildren && ancestor.SetExpanded(true))
                    _expanded.Add(ancestor.Key);
            }
        }
        _syncing = true;
        try
        {
            SelectedItem = item;
        }
        finally
        {
            _syncing = false;
        }
        UpdateItems();
    }

    private PageMenuItem? FindCurrentItem()
    {
        var all = AllItems().ToList();
        if (all.Count == 0)
            return null;

        for (var v = _watched.Count - 1; v >= 0; v--)
        {
            if (_watched[v] is not { CurrentPage: { } current } pages)
                continue;

            var visited = new HashSet<PageId>();
            var queue = new Queue<(PageRegistration Page, PageId? Parent)>([(current, pages.CurrentParentPage)]);
            while (queue.TryDequeue(out var next))
            {
                if (!visited.Add(next.Page.PageId))
                    continue;
                var matches = all.Where(item => item.PageId == next.Page.PageId).ToList();
                if (matches.Count > 0)
                    return matches.FirstOrDefault(item => item.ParentPage == next.Parent) ?? matches[0];

                // The parent the page was reached from comes first.
                var parents = next.Parent is { } reachedFrom ? next.Page.Parents.Prepend(reachedFrom) : next.Page.Parents;
                foreach (var parent in parents)
                {
                    if (PageRegistry.Find(parent) is { } registration)
                        queue.Enqueue((registration, null));
                }
            }
        }
        return null;
    }

    /// <summary>
    ///     Navigates to an item's page. For an item deep in the tree, each page on the way that shows its children in
    ///     a nested page view is navigated to first, so the page appears where it sits in the tree.
    /// </summary>
    private void Navigate(PageMenuItem item)
    {
        if (View is not { } view)
        {
            Sync();
            return;
        }

        var addToBackStack = AddToBackStack;
        _navigating = true;
        try
        {
            var path = item.Path;
            var refused = false;
            for (var i = 0; i < path.Count - 1; i++)
            {
                var step = path[i];
                var pages = view.PageControlDataContext;
                // The page is already shown here, e.g. reached from another parent: only its place in the tree changes.
                if (pages.CurrentPageId == item.PageId)
                    break;
                if (pages.CurrentPageId != step.PageId)
                {
                    // A page known not to show its children in a view of its own isn't worth visiting on the way.
                    if (_hostsChildren.TryGetValue(step.PageId, out var hosts) && !hosts)
                        continue;
                    if (!pages.NavigateTo(step.PageId, step.ParentPage, addToBackStack))
                    {
                        refused = true;
                        break;
                    }
                    addToBackStack = false;
                }
                var child = pages.ActivePage?.ChildView is { } childView && childView.PageControlDataContext.ParentPage == step.PageId
                    ? childView
                    : null;
                _hostsChildren[step.PageId] = child is not null;
                if (child is not null)
                    view = child;
            }
            if (!refused)
                view.PageControlDataContext.NavigateTo(item.PageId, item.ParentPage, addToBackStack);
        }
        finally
        {
            _navigating = false;
        }

        // A refused navigation (locked, or cancelled by the current page) leaves the highlight on the current page.
        WatchViews();
        Sync();
    }

    private bool IsShowing(PageMenuItem item) => _watched.Any(pages => pages.CurrentPageId == item.PageId);

    private void OnListSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_syncing)
            return;
        if (_list.SelectedItem is PageMenuItem picked)
            Navigate(picked);
        else
            Highlight();
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source || source.FindAncestorOfType<PageMenuChevron>(includeSelf: true) is not null)
            return;
        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { DataContext: PageMenuItem item })
            return;

        // Picking the highlighted item again, while one of its child pages is shown, returns to it.
        if (ReferenceEquals(item, _list.SelectedItem) && !IsShowing(item))
            Navigate(item);
        _flyout.Hide();
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (_list.SelectedItem is not PageMenuItem item)
            return;
        switch (e.Key)
        {
            case Key.Right when TreeShown && item.HasChildren && !item.IsExpanded:
                item.IsExpanded = true;
                e.Handled = true;
                break;
            case Key.Left when TreeShown && item.HasChildren && item.IsExpanded:
                item.IsExpanded = false;
                e.Handled = true;
                break;
            case Key.Left when TreeShown && item.Parent is { } parent:
                // Collapsing the parent moves the highlight to it, without leaving the page.
                parent.IsExpanded = false;
                _list.ContainerFromItem(parent)?.Focus(NavigationMethod.Directional);
                e.Handled = true;
                break;
            case Key.Enter or Key.Space:
                if (!IsShowing(item))
                    Navigate(item);
                _flyout.Hide();
                e.Handled = true;
                break;
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        var width = _topLevel?.Bounds.Width ?? 0;
        _flyoutHost.Width = Narrow && width > 0 ? Math.Min(320, width - 32) : 280;
        _flyoutHost.MaxHeight = _topLevel is { Bounds.Height: > 160 } topLevel ? topLevel.Bounds.Height - 96 : double.PositiveInfinity;
        _flyoutBackRegistration?.Dispose();
        _flyoutBackRegistration = _topLevel is null
            ? null
            : BackNavigation.Register(_topLevel, BackNavigation.DialogPriority, () =>
            {
                _flyout.Hide();
                return true;
            });
        _list.Focus();
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        _flyoutBackRegistration?.Dispose();
        _flyoutBackRegistration = null;
    }

    /// <summary>Arranges the menu for <see cref="ActualStyle"/> and <see cref="ActualPlacement"/>, if they changed.</summary>
    private void ApplyLayout(bool force = false)
    {
        var narrow = Narrow;
        var style = narrow && NarrowStyle is { } narrowStyle ? narrowStyle : Style;
        var placement = narrow && NarrowPlacement is { } narrowPlacement ? narrowPlacement : Placement;
        if (!force && _layoutApplied && style == ActualStyle && placement == ActualPlacement)
            return;
        _layoutApplied = true;
        if (style != ActualStyle || placement != ActualPlacement)
            _flyout.Hide();
        ActualStyle = style;
        ActualPlacement = placement;

        // The placement is read for the style: the edge it docks to, and where along that edge the items sit.
        var side = style switch
        {
            PageMenuStyle.VerticalStrip => placement.IsRight ? DockSide.Right : DockSide.Left,
            PageMenuStyle.HorizontalStrip => placement.IsBottom ? DockSide.Bottom : DockSide.Top,
            PageMenuStyle.NavigationBar => placement.IsTop ? DockSide.Top : DockSide.Bottom,
            _ => placement switch
            {
                ScreenPlacement.CenterLeft => DockSide.Left,
                ScreenPlacement.CenterRight => DockSide.Right,
                _ => placement.IsTop ? DockSide.Top : DockSide.Bottom,
            },
        };

        PseudoClasses.Set(":verticalstrip", style == PageMenuStyle.VerticalStrip);
        PseudoClasses.Set(":horizontalstrip", style == PageMenuStyle.HorizontalStrip);
        PseudoClasses.Set(":navigationbar", style == PageMenuStyle.NavigationBar);
        PseudoClasses.Set(":menubutton", style == PageMenuStyle.MenuButton);
        PseudoClasses.Set(":tree", TreeShown);

        // The list sits beside the content, or in the flyout opened from the menu button.
        _frame.Children.Remove(_list);
        _frame.Children.Remove(_buttonBar);
        _flyoutHost.Child = null;
        if (style == PageMenuStyle.MenuButton)
        {
            var onSide = side is DockSide.Left or DockSide.Right;
            _button.HorizontalAlignment = onSide ? HorizontalAlignment.Center : placement.HorizontalAlignment;
            _button.VerticalAlignment = VerticalAlignment.Center;
            _button.Width = _button.Height = Touch ? 48 : 40;
            _flyout.Placement = (side, placement.HorizontalAlignment) switch
            {
                (DockSide.Left, _) => PlacementMode.Right,
                (DockSide.Right, _) => PlacementMode.Left,
                (DockSide.Top, HorizontalAlignment.Left) => PlacementMode.BottomEdgeAlignedLeft,
                (DockSide.Top, HorizontalAlignment.Right) => PlacementMode.BottomEdgeAlignedRight,
                (DockSide.Top, _) => PlacementMode.Bottom,
                (_, HorizontalAlignment.Left) => PlacementMode.TopEdgeAlignedLeft,
                (_, HorizontalAlignment.Right) => PlacementMode.TopEdgeAlignedRight,
                _ => PlacementMode.Top,
            };
            _flyoutHost.Child = _list;
            DockPanel.SetDock(_buttonBar, side);
            _frame.Children.Insert(0, _buttonBar);
        }
        else
        {
            DockPanel.SetDock(_list, side);
            _frame.Children.Insert(0, _list);
        }

        // The panel is aligned inside the list, so the strip still spans its edge while the items sit at the
        // placement's end of it.
        var panelKey = (style, placement);
        if (_panelKey != panelKey)
        {
            _panelKey = panelKey;
            _list.ItemsPanel = style switch
            {
                PageMenuStyle.VerticalStrip => Panel(Orientation.Vertical, HorizontalAlignment.Stretch, placement.VerticalAlignment),
                PageMenuStyle.HorizontalStrip => Panel(Orientation.Horizontal, placement.HorizontalAlignment, VerticalAlignment.Stretch),
                PageMenuStyle.NavigationBar => BarPanel,
                _ => Panel(Orientation.Vertical, HorizontalAlignment.Stretch, VerticalAlignment.Top),
            };
        }
        _list.MinWidth = style == PageMenuStyle.VerticalStrip ? 180 : 0;
        ScrollViewer.SetHorizontalScrollBarVisibility(
            _list,
            style == PageMenuStyle.HorizontalStrip ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled
        );
        ScrollViewer.SetVerticalScrollBarVisibility(_list, Vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled);

        var templateKey = (style, TreeShown, ItemTemplate);
        if (_templateKey != templateKey)
        {
            _templateKey = templateKey;
            _list.ItemTemplate = ItemTemplate ?? DefaultTemplate(style, TreeShown);
        }
        foreach (var container in _list.GetRealizedContainers())
            _list.StyleContainer(container);

        // Below a local value, so DockPanel.Dock set on the menu wins.
        _dock?.Dispose();
        _dock = SetValue(DockPanel.DockProperty, side, BindingPriority.Template);

        UpdateItems();

        static FuncTemplate<Panel?> Panel(Orientation orientation, HorizontalAlignment horizontal, VerticalAlignment vertical) =>
            new(() => new StackPanel { Orientation = orientation, HorizontalAlignment = horizontal, VerticalAlignment = vertical });
    }

    private static FuncDataTemplate<PageMenuItem> DefaultTemplate(PageMenuStyle style, bool tree)
    {
        if (style == PageMenuStyle.NavigationBar)
        {
            // Icon above a short label, like a phone's navigation bar.
            return new FuncDataTemplate<PageMenuItem>((item, _) => new StackPanel
            {
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new PageIconView { Icon = item?.Icon, IconSize = 24, HorizontalAlignment = HorizontalAlignment.Center },
                    new TextBlock
                    {
                        Text = item?.DisplayName,
                        FontSize = 12,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    },
                },
            });
        }

        return new FuncDataTemplate<PageMenuItem>((item, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions(tree ? "Auto,24,Auto,*" : "Auto,Auto,Auto,*") };
            if (tree && item is not null)
            {
                grid.Children.Add(new Border { Width = item.Depth * Indent });
                if (item.HasChildren)
                    grid.Children.Add(new PageMenuChevron(item) { [Grid.ColumnProperty] = 1 });
            }
            grid.Children.Add(new PageIconView
            {
                Icon = item?.Icon,
                IconSize = 20,
                Margin = item?.Icon is null ? default : new Thickness(tree ? 4 : 0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                [Grid.ColumnProperty] = 2,
            });
            grid.Children.Add(new TextBlock
            {
                Text = item?.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                [Grid.ColumnProperty] = 3,
            });
            return grid;
        });
    }
}

/// <summary>The list inside a <see cref="PageMenu"/>, styled as a <see cref="ListBox"/>.</summary>
internal sealed class PageMenuList(PageMenu menu) : ListBox
{
    protected override Type StyleKeyOverride => typeof(ListBox);

    protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
    {
        base.PrepareContainerForItemOverride(container, item, index);
        StyleContainer(container);
    }

    internal void StyleContainer(Control container)
    {
        container.MinHeight = menu.ItemMinHeight;
        if (((container as ContentControl)?.Content ?? container.DataContext) is PageMenuItem { Registration.Shortcut: { } shortcut } item)
            ToolTip.SetTip(container, $"{item.DisplayName} ({PageShortcut.Format(shortcut)})");
        else
            container.ClearValue(ToolTip.TipProperty);
        if (container is ContentControl content)
        {
            content.HorizontalContentAlignment = menu.ItemContentAlignment;
            content.VerticalContentAlignment = VerticalAlignment.Center;
        }
    }
}

/// <summary>Expands and collapses a tree item in a <see cref="PageMenu"/>, without picking it.</summary>
internal sealed class PageMenuChevron : Button
{
    private static readonly Geometry Collapsed = Geometry.Parse("M8.59,16.58L13.17,12L8.59,7.41L10,6L16,12L10,18L8.59,16.58Z");
    private static readonly Geometry Expanded = Geometry.Parse("M7.41,8.58L12,13.17L16.59,8.58L18,10L12,16L6,10L7.41,8.58Z");

    private readonly PathIcon _icon = new() { Width = 12, Height = 12 };

    public PageMenuChevron(PageMenuItem item)
    {
        Item = item;
        Content = _icon;
        Width = Height = 24;
        Padding = default;
        BorderThickness = default;
        Background = Brushes.Transparent;
        Focusable = false;
        HorizontalContentAlignment = HorizontalAlignment.Center;
        VerticalContentAlignment = VerticalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        Update();
    }

    internal PageMenuItem Item { get; }

    protected override Type StyleKeyOverride => typeof(Button);

    protected override void OnClick()
    {
        base.OnClick();
        Item.IsExpanded = !Item.IsExpanded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Item.PropertyChanged += OnItemPropertyChanged;
        Update();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Item.PropertyChanged -= OnItemPropertyChanged;
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e) => Update();

    private void Update()
    {
        _icon.Data = Item.IsExpanded ? Expanded : Collapsed;
        AutomationProperties.SetName(this, Item.IsExpanded ? $"Collapse {Item.DisplayName}" : $"Expand {Item.DisplayName}");
    }
}
