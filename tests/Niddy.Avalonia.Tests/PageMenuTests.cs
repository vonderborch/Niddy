using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Niddy.Avalonia.PageSystem;
using DockSide = Avalonia.Controls.Dock;

namespace Niddy.Avalonia.Tests;

public class PageMenuTests
{
    private static (PageMenu Menu, PageView View, Window Window) Show(double width = 800, Action<PageMenu>? configure = null, bool wrap = false)
    {
        var view = new PageView { StartingPage = typeof(HomePage), AppDataContext = new TestAppDataContext() };
        var menu = wrap ? new PageMenu { Content = view } : new PageMenu { Target = view };
        configure?.Invoke(menu);
        var window = new Window { Width = width, Height = 600, Content = wrap ? menu : new DockPanel { Children = { menu, view } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (menu, view, window);
    }

    private static IReadOnlyList<Type> Items(PageMenu menu) => [.. menu.Items.Select(i => i.PageType)];

    private static IEnumerable<PageMenuItem> All(IEnumerable<PageMenuItem> items) =>
        items.SelectMany(i => All(i.Children).Prepend(i));

    /// <summary>The item for <typeparamref name="TPage"/>, under <paramref name="under"/> if given, anywhere in the tree.</summary>
    private static PageMenuItem Item<TPage>(PageMenu menu, Type? under = null) =>
        All(menu.Items.Where(i => i.Depth == 0)).First(i => i.PageType == typeof(TPage) && (under is null || i.Parent?.PageType == under));

    private static Control? Shown(PageView view) => view.FindControl<TransitioningContentControl>("PageContent")!.Content as Control;

    private static void Click(Window window, Visual target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static Control Container(PageMenu menu, PageMenuItem item) => menu.List.ContainerFromItem(item)!;

    [AvaloniaFact]
    public void ListsTopLevelPagesAndHighlightsTheCurrentOne()
    {
        var (menu, _, _) = Show();

        Assert.Equal(PageRegistry.GetPages().Select(p => p.PageType), Items(menu));
        Assert.Contains(typeof(HomePage), Items(menu));
        Assert.Equal(typeof(HomePage), menu.SelectedItem?.PageType);
        Assert.Same(menu.SelectedItem, menu.List.SelectedItem);
    }

    [AvaloniaFact]
    public void ItemsCarryTheRegistrationsIcon()
    {
        var (menu, _, _) = Show();

        Assert.NotNull(Item<HomePage>(menu).Icon);
        Assert.Same(PageRegistry.Find(HomePage.PageId)!.Icon, Item<HomePage>(menu).Icon);
        Assert.Null(Item<DetailsPage>(menu).Icon);
    }

    [AvaloniaFact]
    public void PickingAPageNavigatesTheTargetWithoutTheBackStack()
    {
        var (menu, view, _) = Show();

        menu.SelectedItem = Item<DetailsPage>(menu);

        Assert.IsType<DetailsPage>(Shown(view));
        Assert.False(view.CanGoBack);
        Assert.Equal(typeof(DetailsPage), menu.SelectedItem?.PageType);
    }

    [AvaloniaFact]
    public void PickingAPageCanAddToTheBackStack()
    {
        var (menu, view, _) = Show(configure: m => m.AddToBackStack = true);

        menu.SelectedItem = Item<DetailsPage>(menu);

        Assert.True(view.CanGoBack);
    }

    [AvaloniaFact]
    public void HighlightFollowsNavigationAndGoingBack()
    {
        var (menu, view, _) = Show();

        view.NavigateTo<GuardedPage>();
        Assert.Equal(typeof(GuardedPage), menu.SelectedItem?.PageType);

        view.GoBack();
        Assert.Equal(typeof(HomePage), menu.SelectedItem?.PageType);

        view.Push(new PushedPage("Unregistered"));
        Assert.Null(menu.SelectedItem);
        Assert.Null(menu.List.SelectedItem);
    }

    [AvaloniaFact]
    public void ASectionStaysHighlightedWhileOneOfItsChildPagesIsShown()
    {
        var (menu, view, _) = Show();

        // ItemPage has two parents in the menu; the one it was reached from is highlighted.
        view.PageControlDataContext.NavigateToChild<ItemPage>();
        Assert.IsType<ItemPage>(Shown(view));
        Assert.Equal(typeof(HomePage), menu.SelectedItem?.PageType);

        view.NavigateTo<DetailsPage>();
        view.PageControlDataContext.NavigateToChild<ItemPage>();
        Assert.Equal(typeof(DetailsPage), menu.SelectedItem?.PageType);
    }

    [AvaloniaFact]
    public void TappingTheHighlightedSectionReturnsToIt()
    {
        var (menu, view, window) = Show();
        view.PageControlDataContext.NavigateToChild<ItemPage>();

        Click(window, Container(menu, Item<HomePage>(menu)));

        Assert.IsType<HomePage>(Shown(view));
    }

    [AvaloniaFact]
    public void TappingAnItemNavigates()
    {
        var (menu, view, window) = Show();

        Click(window, Container(menu, Item<DetailsPage>(menu)));

        Assert.IsType<DetailsPage>(Shown(view));
    }

    [AvaloniaFact]
    public void RefusedNavigationKeepsTheCurrentPageHighlighted()
    {
        var (menu, view, _) = Show();
        view.NavigateTo<GuardedPage>();
        ((GuardedPage)Shown(view)!).Block = true;

        menu.SelectedItem = Item<DetailsPage>(menu);

        Assert.IsType<GuardedPage>(Shown(view));
        Assert.Equal(typeof(GuardedPage), menu.SelectedItem?.PageType);
        Assert.Same(menu.SelectedItem, menu.List.SelectedItem);
    }

    [AvaloniaFact]
    public void ListsTheChildPagesOfAGivenParent()
    {
        var (menu, view, _) = Show(configure: m => m.ParentPage = typeof(SettingsPage));
        view.NavigateTo<SettingsPage>();
        var children = ((SettingsPage)Shown(view)!).Children;
        menu.Target = children;

        Assert.Equal([typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage), typeof(AboutPage)], Items(menu));
        Assert.Equal(typeof(GeneralSettingsPage), menu.SelectedItem?.PageType);

        menu.SelectedItem = Item<AdvancedSettingsPage>(menu);
        Assert.IsType<AdvancedSettingsPage>(Shown(children));
        Assert.IsType<SettingsPage>(Shown(view));
    }

    [AvaloniaFact]
    public void ChangingTheParentChangesTheItems()
    {
        var (menu, _, _) = Show(configure: m => m.ParentPage = typeof(SettingsPage));
        Assert.Contains(typeof(GeneralSettingsPage), Items(menu));

        menu.ParentPage = null;

        Assert.Equal(PageRegistry.GetPages().Select(p => p.PageType), Items(menu));
    }

    [AvaloniaFact]
    public void PagesRegisteredLaterAreAdded()
    {
        var (menu, _, _) = Show(configure: m => m.ParentPage = typeof(LateParentPage));
        Assert.Empty(Items(menu));

        PageRegistry.Register<LateChildPage>(parents: [typeof(LateParentPage)]);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal([typeof(LateChildPage)], Items(menu));
    }

    // ── Style and placement ─────────────────────────────────────────────────────────

    private static StackPanel ItemsPanel(PageMenu menu) => Assert.IsType<StackPanel>(menu.List.ItemsPanelRoot);

    [AvaloniaFact]
    public void NarrowWindowsUseTheNarrowStyleAndPlacement()
    {
        var (menu, _, window) = Show(width: 800);
        Assert.Equal(PageMenuStyle.VerticalStrip, menu.ActualStyle);
        Assert.Equal(ScreenPlacement.TopLeft, menu.ActualPlacement);
        Assert.Equal(DockSide.Left, DockPanel.GetDock(menu));
        Assert.Contains(":verticalstrip", menu.Classes);

        window.Width = 400;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(PageMenuStyle.NavigationBar, menu.ActualStyle);
        Assert.Equal(ScreenPlacement.BottomCenter, menu.ActualPlacement);
        Assert.Equal(DockSide.Bottom, DockPanel.GetDock(menu));
        Assert.Contains(":navigationbar", menu.Classes);
        Assert.DoesNotContain(":verticalstrip", menu.Classes);
        Assert.IsType<UniformGrid>(menu.List.ItemsPanelRoot);
    }

    [AvaloniaFact]
    public void WithoutNarrowSettingsTheStyleAndPlacementAreKept()
    {
        var (menu, _, _) = Show(width: 400, configure: m =>
        {
            m.Placement = ScreenPlacement.CenterRight;
            m.NarrowStyle = null;
            m.NarrowPlacement = null;
        });

        Assert.Equal(PageMenuStyle.VerticalStrip, menu.ActualStyle);
        Assert.Equal(ScreenPlacement.CenterRight, menu.ActualPlacement);
        Assert.Equal(DockSide.Right, DockPanel.GetDock(menu));

        // Each narrow setting falls back on its own.
        menu.NarrowStyle = PageMenuStyle.HorizontalStrip;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(PageMenuStyle.HorizontalStrip, menu.ActualStyle);
        Assert.Equal(ScreenPlacement.CenterRight, menu.ActualPlacement);
        Assert.Equal(DockSide.Top, DockPanel.GetDock(menu));
    }

    [AvaloniaTheory]
    [InlineData(ScreenPlacement.TopLeft, DockSide.Left, VerticalAlignment.Top)]
    [InlineData(ScreenPlacement.CenterLeft, DockSide.Left, VerticalAlignment.Center)]
    [InlineData(ScreenPlacement.BottomLeft, DockSide.Left, VerticalAlignment.Bottom)]
    [InlineData(ScreenPlacement.TopRight, DockSide.Right, VerticalAlignment.Top)]
    [InlineData(ScreenPlacement.CenterRight, DockSide.Right, VerticalAlignment.Center)]
    [InlineData(ScreenPlacement.BottomRight, DockSide.Right, VerticalAlignment.Bottom)]
    [InlineData(ScreenPlacement.TopCenter, DockSide.Left, VerticalAlignment.Top)]
    [InlineData(ScreenPlacement.BottomCenter, DockSide.Left, VerticalAlignment.Bottom)]
    public void AVerticalStripDocksToTheSideAndAlignsItsItems(ScreenPlacement placement, DockSide side, VerticalAlignment alignment)
    {
        var (menu, _, _) = Show(configure: m => m.Placement = placement);

        Assert.Equal(side, DockPanel.GetDock(menu));
        Assert.Equal(Orientation.Vertical, ItemsPanel(menu).Orientation);
        Assert.Equal(alignment, ItemsPanel(menu).VerticalAlignment);
    }

    [AvaloniaTheory]
    [InlineData(ScreenPlacement.TopLeft, DockSide.Top, HorizontalAlignment.Left)]
    [InlineData(ScreenPlacement.TopCenter, DockSide.Top, HorizontalAlignment.Center)]
    [InlineData(ScreenPlacement.TopRight, DockSide.Top, HorizontalAlignment.Right)]
    [InlineData(ScreenPlacement.BottomLeft, DockSide.Bottom, HorizontalAlignment.Left)]
    [InlineData(ScreenPlacement.BottomRight, DockSide.Bottom, HorizontalAlignment.Right)]
    [InlineData(ScreenPlacement.CenterLeft, DockSide.Top, HorizontalAlignment.Left)]
    [InlineData(ScreenPlacement.CenterRight, DockSide.Top, HorizontalAlignment.Right)]
    public void AHorizontalStripDocksToTheTopOrBottomAndAlignsItsItems(ScreenPlacement placement, DockSide side, HorizontalAlignment alignment)
    {
        var (menu, _, _) = Show(configure: m =>
        {
            m.Style = PageMenuStyle.HorizontalStrip;
            m.Placement = placement;
        });

        Assert.Contains(":horizontalstrip", menu.Classes);
        Assert.Equal(side, DockPanel.GetDock(menu));
        Assert.Equal(Orientation.Horizontal, ItemsPanel(menu).Orientation);
        Assert.Equal(alignment, ItemsPanel(menu).HorizontalAlignment);
    }

    [AvaloniaTheory]
    [InlineData(ScreenPlacement.TopRight, DockSide.Top)]
    [InlineData(ScreenPlacement.BottomLeft, DockSide.Bottom)]
    [InlineData(ScreenPlacement.CenterLeft, DockSide.Bottom)]
    public void ANavigationBarSpansTheTopOrBottom(ScreenPlacement placement, DockSide side)
    {
        var (menu, _, _) = Show(configure: m =>
        {
            m.Style = PageMenuStyle.NavigationBar;
            m.Placement = placement;
        });

        Assert.Equal(side, DockPanel.GetDock(menu));
        Assert.IsType<UniformGrid>(menu.List.ItemsPanelRoot);
        Assert.Equal(56, Container(menu, Item<HomePage>(menu)).MinHeight);
    }

    [AvaloniaFact]
    public void DockSetOnTheMenuWins()
    {
        var (menu, _, _) = Show(width: 400, configure: m => DockPanel.SetDock(m, DockSide.Right));

        Assert.Equal(ScreenPlacement.BottomCenter, menu.ActualPlacement);
        Assert.Equal(DockSide.Right, DockPanel.GetDock(menu));
    }

    [AvaloniaFact]
    public void MobileModeUsesTheNarrowStyleAndTouchSizedItems()
    {
        var (menu, _, _) = Show(width: 1000, configure: m => m.IsMobile = true);
        Assert.Equal(PageMenuStyle.NavigationBar, menu.ActualStyle);
        Assert.Equal(56, Container(menu, Item<HomePage>(menu)).MinHeight);

        menu.NarrowStyle = PageMenuStyle.VerticalStrip;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(48, Container(menu, Item<HomePage>(menu)).MinHeight);

        menu.IsMobile = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, Container(menu, Item<HomePage>(menu)).MinHeight);
    }

    [AvaloniaTheory]
    [InlineData(PageMenuStyle.VerticalStrip, ScreenPlacement.CenterLeft)]
    [InlineData(PageMenuStyle.VerticalStrip, ScreenPlacement.TopRight)]
    [InlineData(PageMenuStyle.HorizontalStrip, ScreenPlacement.TopCenter)]
    [InlineData(PageMenuStyle.NavigationBar, ScreenPlacement.BottomCenter)]
    public void ContentIsArrangedAroundTheMenu(PageMenuStyle style, ScreenPlacement placement)
    {
        var (menu, view, window) = Show(wrap: true, configure: m =>
        {
            m.Style = style;
            m.Placement = placement;
        });

        var list = menu.List.Bounds;
        var content = view.Bounds;
        Assert.False(list.Intersects(content));
        switch (DockPanel.GetDock(menu.List))
        {
            case DockSide.Left: Assert.True(list.Right <= content.Left); break;
            case DockSide.Right: Assert.True(list.Left >= content.Right); break;
            case DockSide.Top: Assert.True(list.Bottom <= content.Top); break;
            default: Assert.True(list.Top >= content.Bottom); break;
        }
        Assert.Equal(window.Bounds.Size, menu.Bounds.Size);

        // The content's page view is the default target.
        menu.SelectedItem = Item<DetailsPage>(menu);
        Assert.IsType<DetailsPage>(Shown(view));
    }

    [AvaloniaTheory]
    [InlineData(ScreenPlacement.TopLeft, HorizontalAlignment.Left, PlacementMode.BottomEdgeAlignedLeft)]
    [InlineData(ScreenPlacement.TopCenter, HorizontalAlignment.Center, PlacementMode.Bottom)]
    [InlineData(ScreenPlacement.TopRight, HorizontalAlignment.Right, PlacementMode.BottomEdgeAlignedRight)]
    [InlineData(ScreenPlacement.BottomLeft, HorizontalAlignment.Left, PlacementMode.TopEdgeAlignedLeft)]
    [InlineData(ScreenPlacement.BottomCenter, HorizontalAlignment.Center, PlacementMode.Top)]
    [InlineData(ScreenPlacement.BottomRight, HorizontalAlignment.Right, PlacementMode.TopEdgeAlignedRight)]
    [InlineData(ScreenPlacement.CenterLeft, HorizontalAlignment.Center, PlacementMode.Right)]
    [InlineData(ScreenPlacement.CenterRight, HorizontalAlignment.Center, PlacementMode.Left)]
    public void TheMenuButtonSitsAtThePlacement(ScreenPlacement placement, HorizontalAlignment alignment, PlacementMode flyoutPlacement)
    {
        var (menu, view, window) = Show(wrap: true, configure: m =>
        {
            m.Style = PageMenuStyle.MenuButton;
            m.Placement = placement;
        });

        Assert.Contains(":menubutton", menu.Classes);
        Assert.True(menu.MenuButton.IsVisible && TopLevel.GetTopLevel(menu.MenuButton) == window);
        Assert.Null(TopLevel.GetTopLevel(menu.List));
        Assert.Equal(alignment, menu.MenuButton.HorizontalAlignment);
        Assert.Equal(flyoutPlacement, menu.MenuFlyout.Placement);
        var button = menu.MenuButton.TranslatePoint(default, window)!.Value;
        var page = view.TranslatePoint(default, window)!.Value;
        switch (placement)
        {
            case ScreenPlacement.CenterLeft: Assert.True(button.X < page.X); break;
            case ScreenPlacement.CenterRight: Assert.True(button.X > page.X + view.Bounds.Width - 1); break;
            default: Assert.Equal(placement.ToString().StartsWith("Top"), button.Y < page.Y); break;
        }
    }

    [AvaloniaFact]
    public void TheMenuButtonOpensTheMenuAndPickingClosesIt()
    {
        var (menu, view, window) = Show(wrap: true, configure: m => m.Style = PageMenuStyle.MenuButton);

        Click(window, menu.MenuButton);
        Assert.True(menu.MenuFlyout.IsOpen);
        Assert.NotNull(TopLevel.GetTopLevel(menu.List));

        var details = Container(menu, Item<DetailsPage>(menu));
        var root = TopLevel.GetTopLevel(details)!;
        var point = details.TranslatePoint(new Point(10, 10), root)!.Value;
        if (root is Window popupWindow)
        {
            popupWindow.MouseDown(point, MouseButton.Left);
            popupWindow.MouseUp(point, MouseButton.Left);
        }
        else
        {
            menu.SelectedItem = Item<DetailsPage>(menu);
            menu.MenuFlyout.Hide();
        }
        Dispatcher.UIThread.RunJobs();

        Assert.IsType<DetailsPage>(Shown(view));
        Assert.False(menu.MenuFlyout.IsOpen);
    }

    [AvaloniaFact]
    public void SwitchingStyleClosesTheMenu()
    {
        var (menu, _, window) = Show(wrap: true, configure: m =>
        {
            m.Style = PageMenuStyle.MenuButton;
            m.Placement = ScreenPlacement.BottomRight;
        });
        Click(window, menu.MenuButton);
        Assert.True(menu.MenuFlyout.IsOpen);

        menu.Style = PageMenuStyle.VerticalStrip;
        Dispatcher.UIThread.RunJobs();

        Assert.False(menu.MenuFlyout.IsOpen);
        Assert.Same(window, TopLevel.GetTopLevel(menu.List));
        Assert.Equal(DockSide.Right, DockPanel.GetDock(menu.List));
    }

    // ── Tree ────────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void TreeModeExpandsTheCurrentPage()
    {
        var (menu, _, _) = Show(configure: m => m.Mode = PageMenuMode.Tree);

        Assert.Contains(":tree", menu.Classes);
        var home = Item<HomePage>(menu);
        Assert.True(home.HasChildren);
        Assert.True(home.IsExpanded);
        var index = menu.Items.IndexOf(home);
        Assert.Equal(typeof(ItemPage), menu.Items[index + 1].PageType);
        Assert.Equal(1, menu.Items[index + 1].Depth);
        Assert.Same(home, menu.Items[index + 1].Parent);
    }

    [AvaloniaFact]
    public void ExpandingAndCollapsingShowsAndHidesChildren()
    {
        var (menu, view, _) = Show(configure: m => m.Mode = PageMenuMode.Tree);
        var settings = Item<SettingsPage>(menu);
        Assert.False(settings.IsExpanded);
        Assert.DoesNotContain(typeof(GeneralSettingsPage), Items(menu));

        settings.IsExpanded = true;
        var index = menu.Items.IndexOf(settings);
        Assert.Equal(
            [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage), typeof(AboutPage)],
            menu.Items.Skip(index + 1).Take(3).Select(i => i.PageType)
        );

        settings.IsExpanded = false;
        Assert.DoesNotContain(typeof(GeneralSettingsPage), Items(menu));
        Assert.IsType<HomePage>(Shown(view));
    }

    [AvaloniaFact]
    public void TheExpanderDoesntNavigate()
    {
        var (menu, view, window) = Show(configure: m => m.Mode = PageMenuMode.Tree);
        var settings = Item<SettingsPage>(menu);
        var chevron = Container(menu, settings).GetVisualDescendants().OfType<PageMenuChevron>().Single();

        Click(window, chevron);

        Assert.True(settings.IsExpanded);
        Assert.IsType<HomePage>(Shown(view));
        Assert.Equal(typeof(HomePage), menu.SelectedItem?.PageType);
    }

    [AvaloniaFact]
    public void ArrowKeysExpandAndCollapse()
    {
        var (menu, view, window) = Show(configure: m => m.Mode = PageMenuMode.Tree);
        view.NavigateTo<SettingsPage>();
        var settings = Item<SettingsPage>(menu);
        Assert.True(settings.IsExpanded);
        Assert.Equal(typeof(GeneralSettingsPage), menu.SelectedItem?.PageType);
        Container(menu, menu.SelectedItem!).Focus();

        // Left on a child collapses its parent, which takes the highlight without leaving the page.
        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.False(settings.IsExpanded);
        Assert.Same(settings, menu.List.SelectedItem);
        Assert.IsType<SettingsPage>(Shown(view));

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(settings.IsExpanded);

        window.KeyPressQwerty(PhysicalKey.ArrowLeft, RawInputModifiers.None);
        Assert.False(settings.IsExpanded);

        window.KeyPressQwerty(PhysicalKey.ArrowRight, RawInputModifiers.None);
        Assert.True(settings.IsExpanded);
    }

    [AvaloniaFact]
    public void PickingADeepItemNavigatesTheNestedView()
    {
        var (menu, view, _) = Show(configure: m => m.Mode = PageMenuMode.Tree);
        Item<SettingsPage>(menu).IsExpanded = true;

        menu.SelectedItem = Item<AdvancedSettingsPage>(menu, under: typeof(SettingsPage));

        var settings = Assert.IsType<SettingsPage>(Shown(view));
        Assert.IsType<AdvancedSettingsPage>(Shown(settings.Children));
        Assert.False(view.CanGoBack);
        Assert.Same(Item<AdvancedSettingsPage>(menu, under: typeof(SettingsPage)), menu.SelectedItem);
        Assert.Same(menu.SelectedItem, menu.List.SelectedItem);

        // Navigating the nested view moves the highlight.
        settings.Children.NavigateTo<GeneralSettingsPage>();
        Assert.Same(Item<GeneralSettingsPage>(menu, under: typeof(SettingsPage)), menu.SelectedItem);
    }

    [AvaloniaFact]
    public void TheItemUnderTheParentItWasReachedFromIsSelected()
    {
        var (menu, view, _) = Show(configure: m => m.Mode = PageMenuMode.Tree);

        view.NavigateTo<DetailsPage>();
        view.PageControlDataContext.NavigateToChild<ItemPage>();
        Assert.Same(Item<ItemPage>(menu, under: typeof(DetailsPage)), menu.SelectedItem);
        Assert.True(Item<DetailsPage>(menu).IsExpanded);

        // The same page under its other parent: only where it sits in the tree changes.
        Item<HomePage>(menu).IsExpanded = true;
        var item = view.PageControlDataContext.ActivePage;
        menu.SelectedItem = Item<ItemPage>(menu, under: typeof(HomePage));
        Assert.Same(item, view.PageControlDataContext.ActivePage);
        Assert.Equal(HomePage.PageId, view.PageControlDataContext.CurrentParentPage);
        Assert.Same(Item<ItemPage>(menu, under: typeof(HomePage)), menu.SelectedItem);
    }

    [AvaloniaFact]
    public void CollapsingHighlightsTheCollapsedAncestor()
    {
        var (menu, view, _) = Show(configure: m => m.Mode = PageMenuMode.Tree);
        view.NavigateTo<DetailsPage>();
        view.PageControlDataContext.NavigateToChild<ItemPage>();
        var details = Item<DetailsPage>(menu);

        details.IsExpanded = false;

        Assert.Same(details, menu.List.SelectedItem);
        Assert.Equal(typeof(ItemPage), menu.SelectedItem?.PageType);
    }

    [AvaloniaFact]
    public void BarsListOnlyTheTopLevelOfATree()
    {
        var (menu, _, _) = Show(width: 400, configure: m => m.Mode = PageMenuMode.Tree);

        Assert.Equal(ScreenPlacement.BottomCenter, menu.ActualPlacement);
        Assert.DoesNotContain(":tree", menu.Classes);
        Assert.All(menu.Items, item => Assert.Equal(0, item.Depth));
        Assert.True(Item<HomePage>(menu).HasChildren);
    }
}
