using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Niddy.Avalonia.PageSystem;
using Niddy.TestFixtures.ExternalPages;

namespace Niddy.Avalonia.Tests;

public class PageViewTests
{
    private static (PageView View, TestAppDataContext App) Show(Type? startingPage = null)
    {
        var app = new TestAppDataContext();
        var view = new PageView { StartingPage = startingPage ?? typeof(HomePage), AppDataContext = app };
        new Window { Content = view }.Show();
        return (view, app);
    }

    private static Control? Shown(PageView view) => view.FindControl<TransitioningContentControl>("PageContent")!.Content as Control;

    // ── Data contexts ───────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void PagesGetTheAppPageControlAndTheirOwnDataContexts()
    {
        var (view, app) = Show();

        var home = Assert.IsType<HomePage>(Shown(view));
        Assert.Same(app, home.AppDataContext);
        Assert.Same(view.PageControlDataContext, home.PageControlDataContext);
        Assert.Same(home.PageDataContext, home.DataContext);
        Assert.Equal(HomePage.PageId, home.Registration!.PageId);
        Assert.Equal("Home page", view.PageControlDataContext.CurrentPageName);
    }

    [AvaloniaFact]
    public void PlainPagesGetTheAppDataContextAsTheirDataContext()
    {
        var (view, app) = Show(typeof(PlainPage));

        Assert.Same(app, Assert.IsType<PlainPage>(Shown(view)).DataContext);
    }

    [AvaloniaFact]
    public void AxamlPagesBindToTheirPageDataContext()
    {
        var (view, _) = Show(typeof(XamlPage));

        var page = Assert.IsType<XamlPage>(Shown(view));
        Assert.Equal("from xaml", page.FindControl<TextBlock>("Label")!.Text);
    }

    [AvaloniaFact]
    public void TypedPageWithoutAnAppDataContextExplainsWhatIsMissing()
    {
        var view = new PageView();
        new Window { Content = view }.Show();

        var error = Assert.Throws<InvalidOperationException>(() => view.NavigateTo<DetailsPage>());

        Assert.Contains(nameof(TestAppDataContext), error.Message);
        Assert.Null(view.PageControlDataContext.ActivePage);
    }

    [AvaloniaFact]
    public void DataContextsThrowBeforeThePageIsShown()
    {
        var page = new DetailsPage();

        Assert.Throws<InvalidOperationException>(() => page.AppDataContext);
        Assert.Throws<InvalidOperationException>(() => page.PageDataContext);
        Assert.Throws<InvalidOperationException>(() => page.PageControlDataContext);
    }

    // ── Navigation ─────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void GoBackRestoresTheSameInstance()
    {
        var (view, _) = Show();
        var home = (HomePage)Shown(view)!;

        Assert.True(view.NavigateTo<DetailsPage>());
        var details = Assert.IsType<DetailsPage>(Shown(view));
        Assert.True(view.CanGoBack);

        Assert.True(view.GoBack());
        Assert.Same(home, Shown(view));
        Assert.Equal(2, home.NavigatedToCount);
        Assert.Equal(NavigationMode.Back, home.LastNavigatedTo!.Mode);
        Assert.False(view.CanGoBack);
        Assert.True(details.IsClosed);
        Assert.True(details.PageDataContext.IsDisposed);
    }

    [AvaloniaFact]
    public void NavigatingWithoutBackStackClosesThePreviousPage()
    {
        var (view, _) = Show();
        var home = (HomePage)Shown(view)!;

        view.NavigateTo(DetailsPage.PageId, addToBackStack: false);

        Assert.False(view.CanGoBack);
        Assert.False(view.GoBack());
        Assert.Equal(["to", "from", "closed"], home.Events);
    }

    [AvaloniaFact]
    public void NavigatingToTheCurrentPageDoesNothingUnlessThereIsAParameter()
    {
        var (view, _) = Show();
        var home = Shown(view);

        Assert.False(view.NavigateTo<HomePage>());
        Assert.Same(home, Shown(view));

        Assert.True(view.NavigateTo(HomePage.PageId, parameter: 42));
        Assert.NotSame(home, Shown(view));
    }

    [AvaloniaFact]
    public void ParametersReachTheNewPage()
    {
        var (view, _) = Show();

        view.NavigateTo(DetailsPage.PageId, parameter: "item-7");

        var details = (DetailsPage)Shown(view)!;
        Assert.Equal("item-7", details.LastNavigatedTo!.Parameter);
        Assert.Equal(HomePage.PageId, details.LastNavigatedTo.From!.PageId);
        Assert.Equal(NavigationMode.Forward, details.LastNavigatedTo.Mode);
    }

    [AvaloniaFact]
    public void UnknownPageIdThrows()
    {
        var (view, _) = Show();

        Assert.Throws<ArgumentOutOfRangeException>(() => view.NavigateTo<PushedPage>());
    }

    [AvaloniaFact]
    public void BackStackIsTrimmedToItsMaximumDepth()
    {
        var (view, _) = Show();
        var home = (HomePage)Shown(view)!;
        view.PageControlDataContext.MaxBackStackDepth = 1;

        view.NavigateTo<DetailsPage>();
        view.NavigateTo<GuardedPage>();

        Assert.True(home.IsClosed);
        Assert.True(view.GoBack());
        Assert.IsType<DetailsPage>(Shown(view));
        Assert.False(view.CanGoBack);
    }

    [AvaloniaFact]
    public void ChangingTheStartingPageResetsTheView()
    {
        var (view, _) = Show();
        view.NavigateTo<DetailsPage>();

        view.StartingPage = typeof(GuardedPage);

        Assert.IsType<GuardedPage>(Shown(view));
        Assert.False(view.CanGoBack);
    }

    [AvaloniaFact]
    public void NavigatedEventReportsEachNavigation()
    {
        var (view, _) = Show();
        var events = new List<PageNavigationEventArgs>();
        view.Navigated += (_, e) => events.Add(e);

        view.NavigateTo<DetailsPage>();
        view.GoBack();

        Assert.Equal([NavigationMode.Forward, NavigationMode.Back], events.Select(e => e.Mode));
        Assert.Equal(DetailsPage.PageId, events[0].To!.PageId);
        Assert.Equal(HomePage.PageId, events[1].To!.PageId);
    }

    // Bug 3: bindings to CurrentPageId used to update before the new page was shown.
    [AvaloniaFact]
    public void PropertiesChangeAfterTheNewPageIsShown()
    {
        var (view, _) = Show();
        Control? shownWhenChanged = null;
        view.PageControlDataContext.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PageControlDataContext.CurrentPageId))
                shownWhenChanged = Shown(view);
        };

        view.NavigateTo<DetailsPage>();

        Assert.IsType<DetailsPage>(shownWhenChanged);
    }

    // ── Push ────────────────────────────────────────────────────────────────────────

    // Bug 2: after pushing a page, navigating to the page below it used to be ignored as "already shown".
    [AvaloniaFact]
    public void PushedPagesHaveNoIdAndCanBeLeftForThePreviousPage()
    {
        var (view, app) = Show();

        Assert.True(view.Push(new PushedPage("One-off")));
        var pushed = Assert.IsType<PushedPage>(Shown(view));
        Assert.Null(view.PageControlDataContext.CurrentPageId);
        Assert.Null(pushed.Registration);
        Assert.Same(app, pushed.AppDataContext);

        Assert.True(view.NavigateTo<HomePage>());
        Assert.IsType<HomePage>(Shown(view));
    }

    // ── Guards ──────────────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void LockBlocksEveryNavigation()
    {
        var (view, _) = Show();
        view.NavigateTo<DetailsPage>();
        var details = Shown(view);
        view.PageControlDataContext.LockToCurrentPage = true;

        Assert.False(view.NavigateTo<GuardedPage>());
        Assert.False(view.Push(new PushedPage("Blocked")));
        Assert.False(view.GoBack());
        Assert.Same(details, Shown(view));
    }

    [AvaloniaFact]
    public void PageCanCancelLeavingAndContinueLater()
    {
        var (view, _) = Show();
        view.NavigateTo<GuardedPage>();
        var guarded = (GuardedPage)Shown(view)!;
        guarded.Block = true;

        Assert.False(view.NavigateTo(DetailsPage.PageId, parameter: "later"));
        Assert.Same(guarded, Shown(view));
        Assert.False(view.GoBack());

        // E.g. after the user confirms discarding changes. The last refused navigation was going back.
        Assert.True(guarded.Refused!.Continue());
        Assert.IsType<HomePage>(Shown(view));
    }

    [AvaloniaFact]
    public void ContinuingAfterNavigatingElsewhereDoesNothing()
    {
        var (view, _) = Show();
        view.NavigateTo<GuardedPage>();
        var guarded = (GuardedPage)Shown(view)!;
        guarded.Block = true;
        view.NavigateTo<DetailsPage>();
        var refused = guarded.Refused!;

        guarded.Block = false;
        view.GoBack();

        Assert.False(refused.Continue());
        Assert.IsType<HomePage>(Shown(view));
    }

    // ── Keep alive and nesting ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void KeptAlivePagesAreReusedAndNeverClosed()
    {
        var (view, _) = Show();

        view.NavigateTo(SettingsPage.PageId, addToBackStack: false);
        var settings = (SettingsPage)Shown(view)!;
        view.NavigateTo(HomePage.PageId, addToBackStack: false);
        view.NavigateTo(SettingsPage.PageId, addToBackStack: false);

        Assert.Same(settings, Shown(view));
        Assert.False(settings.IsClosed);
        Assert.Same(settings.PageDataContext, settings.DataContext);
    }

    [AvaloniaFact]
    public void NestedPageViewShowsTheChildPagesOfItsPage()
    {
        var (view, app) = Show();

        view.NavigateTo<SettingsPage>();
        var settings = (SettingsPage)Shown(view)!;
        var children = settings.Children;

        Assert.Equal(SettingsPage.PageId, children.PageControlDataContext.ParentPage);
        Assert.Equal(
            [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage), typeof(AboutPage)],
            children.PageControlDataContext.AvailablePages.Select(p => p.PageType)
        );
        var general = Assert.IsType<GeneralSettingsPage>(Shown(children));
        Assert.Same(app, general.AppDataContext);

        children.NavigateTo<AdvancedSettingsPage>();
        Assert.IsType<AdvancedSettingsPage>(Shown(children));
        Assert.IsType<SettingsPage>(Shown(view));
    }

    [AvaloniaFact]
    public void PageViewInAPageWithoutChildPagesShowsTopLevelPages()
    {
        var (view, _) = Show(typeof(PlainPage));
        var nested = new PageView { StartingPage = HomePage.PageId };

        ((PlainPage)Shown(view)!).Content = nested;

        Assert.Null(nested.PageControlDataContext.ParentPage);
        Assert.IsType<HomePage>(Shown(nested));
    }

    // ── Relative navigation ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void NavigateToChildDrillsDownAndNavigateToParentComesBack()
    {
        var (view, _) = Show();
        var pages = view.PageControlDataContext;

        Assert.True(pages.NavigateToChild<ItemPage>());
        Assert.IsType<ItemPage>(Shown(view));

        Assert.True(pages.NavigateToParent());
        Assert.IsType<HomePage>(Shown(view));
        Assert.False(pages.NavigateToParent()); // Top-level pages have no parent.
    }

    [AvaloniaFact]
    public void NavigateToParentReturnsToTheParentThePageWasReachedFrom()
    {
        var (view, _) = Show(typeof(DetailsPage));
        var pages = view.PageControlDataContext;

        pages.NavigateToChild<ItemPage>();
        Assert.True(pages.NavigateToParent());
        Assert.IsType<DetailsPage>(Shown(view));

        // Reached directly, a page with several parents doesn't know which one to return to.
        pages.NavigateTo<ItemPage>();
        Assert.False(pages.NavigateToParent());
        Assert.IsType<ItemPage>(Shown(view));
    }

    [AvaloniaFact]
    public void NavigateToChildUsesThePagesOwnPageView()
    {
        var (view, _) = Show();
        view.NavigateTo<SettingsPage>();
        var settings = (SettingsPage)Shown(view)!;

        Assert.True(view.PageControlDataContext.NavigateToChild<AdvancedSettingsPage>());

        Assert.Same(settings, Shown(view));
        Assert.IsType<AdvancedSettingsPage>(Shown(settings.Children));
        // The parent is already shown around the nested view.
        Assert.False(settings.Children.PageControlDataContext.NavigateToParent());
    }

    [AvaloniaFact]
    public void NavigateToChildThrowsForPagesThatArentChildren()
    {
        var (view, _) = Show();

        var error = Assert.Throws<InvalidOperationException>(() => view.PageControlDataContext.NavigateToChild<GeneralSettingsPage>());

        Assert.Contains("Home page", error.Message);
        Assert.IsType<HomePage>(Shown(view));
    }

    [AvaloniaFact]
    public void NavigateToSiblingMovesBetweenPagesWithTheSameParent()
    {
        var (view, _) = Show();
        view.NavigateTo<SettingsPage>();
        var children = ((SettingsPage)Shown(view)!).Children;

        Assert.True(children.PageControlDataContext.NavigateToSibling<AboutPage>());
        Assert.IsType<AboutPage>(Shown(children));
        Assert.Throws<InvalidOperationException>(() => children.PageControlDataContext.NavigateToSibling<ItemPage>());
    }

    [AvaloniaFact]
    public void TopLevelPagesAreSiblings()
    {
        var (view, _) = Show();

        Assert.True(view.PageControlDataContext.NavigateToSibling<AboutPage>());
        Assert.IsType<AboutPage>(Shown(view));
        // A child page of another page, not top-level.
        Assert.Throws<InvalidOperationException>(() => view.PageControlDataContext.NavigateToSibling<ItemPage>());
    }

    // ── Registry ────────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryPageTypeHasAnId()
    {
        Assert.Same(PageId.Of<HomePage>(), HomePage.PageId);
        Assert.Equal(PageId.Of(typeof(HomePage)), HomePage.PageId);
        Assert.True(PageId.Of(typeof(HomePage)) == HomePage.PageId);
        Assert.NotEqual<PageId>(HomePage.PageId, DetailsPage.PageId);
        Type type = HomePage.PageId;
        Assert.Equal(typeof(HomePage), type);
        Assert.Throws<ArgumentException>(() => PageId.Of(typeof(string)));
    }

    [AvaloniaFact]
    public void RegistryCombinesParentsAndChildrenFromBothEnds()
    {
        Assert.Equal(
            [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage), typeof(AboutPage)],
            PageRegistry.GetPages(SettingsPage.PageId).Select(p => p.PageType)
        );
        Assert.Equal([typeof(ItemPage)], PageRegistry.GetPages(HomePage.PageId).Select(p => p.PageType));
        Assert.Equal([typeof(ItemPage)], PageRegistry.GetPages(DetailsPage.PageId).Select(p => p.PageType));

        var item = PageRegistry.Find<ItemPage>()!;
        Assert.Equal([typeof(DetailsPage), typeof(HomePage)], item.Parents.Select(p => p.PageType));
        Assert.Equal([typeof(SettingsPage)], PageRegistry.Find<AdvancedSettingsPage>()!.Parents.Select(p => p.PageType));
        Assert.Empty(item.Children);
    }

    [AvaloniaFact]
    public void PagesWithoutParentsAreTopLevelUnlessSetOtherwise()
    {
        Assert.True(HomePage.PageId.Registration!.IsTopLevel);
        Assert.False(ItemPage.PageId.Registration!.IsTopLevel);
        Assert.True(AboutPage.PageId.Registration!.IsTopLevel);
        Assert.False(PlainPage.PageId.Registration!.IsTopLevel);

        var topLevel = PageRegistry.GetPages().Select(p => p.PageType).ToList();
        Assert.Contains(typeof(HomePage), topLevel);
        Assert.Contains(typeof(AboutPage), topLevel);
        Assert.DoesNotContain(typeof(ItemPage), topLevel);
        Assert.DoesNotContain(typeof(PlainPage), topLevel);
        Assert.True(topLevel.IndexOf(typeof(HomePage)) < topLevel.IndexOf(typeof(DetailsPage)));
    }

    [AvaloniaFact]
    public void RegistrationsHaveSettingsAndDefaultDisplayNames()
    {
        Assert.True(PageRegistry.Find<SettingsPage>()!.KeepAlive);
        Assert.Equal("Home page", PageRegistry.Find<HomePage>()!.DisplayName);
        Assert.Equal("GeneralSettings", PageRegistry.Find<GeneralSettingsPage>()!.DisplayName);
    }

    [AvaloniaFact]
    public void PagesCanBeRegisteredExplicitly()
    {
        var registration = PageRegistry.Register<ManualPage>(displayName: "Manual page", parents: [typeof(GuardedPage)]);
        var (view, _) = Show(typeof(GuardedPage));

        Assert.Same(registration, PageRegistry.Register<ManualPage>(displayName: "Manual page", parents: [typeof(GuardedPage)]));
        Assert.Equal([registration], PageRegistry.GetPages(GuardedPage.PageId));
        Assert.True(view.PageControlDataContext.NavigateToChild<ManualPage>());
        Assert.IsType<ManualPage>(Shown(view));
    }

    [AvaloniaFact]
    public void PagesCanBeRegisteredWithAFactory()
    {
        PageRegistry.Register(() => new FactoryPage("From factory"));
        var (view, _) = Show(typeof(FactoryPage));

        Assert.Equal("From factory", Assert.IsType<FactoryPage>(Shown(view)).Title);
    }

    [AvaloniaFact]
    public void RegisteringAPageAgainWithDifferentSettingsThrows()
    {
        var error = Assert.Throws<InvalidOperationException>(() => PageRegistry.Register<HomePage>(keepAlive: true));

        Assert.Contains(nameof(HomePage), error.Message);
        Assert.False(PageRegistry.Find<HomePage>()!.KeepAlive);
    }

    [AvaloniaFact]
    public void RegisteringACycleThrowsAndLeavesThePageUnregistered()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => PageRegistry.Register<CyclePage>(parents: [typeof(ItemPage)], children: [typeof(HomePage)])
        );

        Assert.Contains("CyclePage > HomePage > ItemPage > CyclePage", error.Message);
        Assert.Null(PageRegistry.Find<CyclePage>());
    }

    [AvaloniaFact]
    public void ParentsAndChildrenMustBePages()
    {
        Assert.Throws<ArgumentException>(() => PageRegistry.Register<CyclePage>(parents: [typeof(string)]));
        Assert.Null(PageRegistry.Find<CyclePage>());
    }

    [AvaloniaFact]
    public void PagesInReferencedProjectsAreRegistered()
    {
        var registration = PageRegistry.Find<ExternalPage>();

        Assert.NotNull(registration);
        Assert.Equal("External page", registration.DisplayName);
    }

    [AvaloniaFact]
    public void PagesInAssembliesLoadedAtRuntimeAreRegisteredWhenTheyLoad()
    {
        var (view, _) = Show();
        Assert.DoesNotContain(PageRegistry.GetAllPages(), p => p.PageType.Name == "PluginPage");

        var path = typeof(PageViewTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "PluginPagesPath").Value!;
        var assembly = Assembly.LoadFrom(Path.GetFullPath(path));
        Dispatcher.UIThread.RunJobs();

        var pluginPage = PageId.Of(assembly.GetType("Niddy.TestFixtures.PluginPages.PluginPage")!);
        Assert.NotNull(PageRegistry.Find(pluginPage));
        Assert.Contains(pluginPage, view.PageControlDataContext.AvailablePages.Select(p => p.PageId));
        Assert.True(view.NavigateTo(pluginPage));
        Assert.Equal("PluginPage", Shown(view)!.GetType().Name);
    }
}
