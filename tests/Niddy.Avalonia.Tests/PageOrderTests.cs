using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;

namespace Niddy.Avalonia.Tests;

/// <summary>A parent whose children the order tests register, so other tests' pages are untouched.</summary>
public sealed class SortParentPage : TrackingPage;

public sealed class SortAlphaPage : TrackingPage;

public sealed class SortBravoPage : TrackingPage;

public sealed class SortCharliePage : TrackingPage;

public sealed class SortZuluPage : TrackingPage;

/// <summary>A NiddyApp that is initialized by hand, never run.</summary>
public sealed class SortOptionsApp(Action<NiddyAppOptions> configure) : NiddyApp
{
    protected override void Configure(NiddyAppOptions options)
    {
        options.StartingPage = HomePage.PageId;
        options.UseFluentTheme = false;
        options.RememberWindowState = false;
        options.HandleUnhandledExceptions = false;
        configure(options);
    }
}

[Collection(typeof(GlobalStateCollection))]
public class PageOrderTests
{
    /// <summary>
    ///     Registers the parent's children. Declared order: Charlie, Alpha (named by the parent), then Bravo and Zulu
    ///     (naming the parent, in registration order). Zulu sorts first by its sort key.
    /// </summary>
    private static PageId Parent()
    {
        PageRegistry.Register<SortParentPage>(children: [typeof(SortCharliePage), typeof(SortAlphaPage)], topLevel: false);
        PageRegistry.Register<SortAlphaPage>(displayName: "Alpha");
        PageRegistry.Register<SortCharliePage>(displayName: "Charlie");
        PageRegistry.Register<SortBravoPage>(displayName: "Bravo", parents: [typeof(SortParentPage)]);
        PageRegistry.Register<SortZuluPage>(displayName: "Zulu", parents: [typeof(SortParentPage)], sortKey: "0");
        return SortParentPage.PageId;
    }

    private static IReadOnlyList<Type> Children() => [.. PageRegistry.GetPages(Parent()).Select(p => p.PageType)];

    /// <summary>Runs <paramref name="test"/>, then puts the registry's sort back.</summary>
    private static void WithSort(Action test)
    {
        try
        {
            test();
        }
        finally
        {
            PageRegistry.Sort = PageRegistry.Alphabetical;
            PageRegistry.SortDescending = false;
        }
    }

    [AvaloniaFact]
    public void PagesAreAlphabeticalBySortKeyByDefault()
    {
        Assert.Equal([typeof(SortZuluPage), typeof(SortAlphaPage), typeof(SortBravoPage), typeof(SortCharliePage)], Children());

        IReadOnlyList<PageRegistration> topLevel = PageRegistry.GetPages();
        Assert.Equal(topLevel.OrderBy(p => p.SortKey, StringComparer.CurrentCultureIgnoreCase), topLevel);
    }

    [AvaloniaFact]
    public void SortKeyDefaultsToTheDisplayName()
    {
        Parent();
        Assert.Equal("Alpha", PageRegistry.Find<SortAlphaPage>()!.SortKey);
        Assert.Equal("0", PageRegistry.Find<SortZuluPage>()!.SortKey);
        Assert.Equal("Zulu", PageRegistry.Find<SortZuluPage>()!.DisplayName);
    }

    [AvaloniaFact]
    public void RegisteringAgainWithADifferentSortKeyThrows()
    {
        Parent();
        Assert.Throws<InvalidOperationException>(() =>
            PageRegistry.Register<SortZuluPage>(displayName: "Zulu", parents: [typeof(SortParentPage)], sortKey: "1"));
    }

    [AvaloniaFact]
    public void SortDescendingReversesTheOrder() => WithSort(() =>
    {
        PageRegistry.SortDescending = true;
        Assert.Equal([typeof(SortCharliePage), typeof(SortBravoPage), typeof(SortAlphaPage), typeof(SortZuluPage)], Children());
    });

    [AvaloniaFact]
    public void DeclaredOrderKeepsTheOrderPagesWereDeclaredIn() => WithSort(() =>
    {
        PageRegistry.Sort = PageRegistry.DeclaredOrder;
        Assert.Equal([typeof(SortCharliePage), typeof(SortAlphaPage), typeof(SortBravoPage), typeof(SortZuluPage)], Children());
    });

    [AvaloniaFact]
    public void ACustomSortIsUsedAndTiesKeepTheDeclaredOrder() => WithSort(() =>
    {
        // Longest name first; Alpha and Bravo tie, and stay in declared order.
        PageRegistry.Sort = (a, b) => b.DisplayName.Length.CompareTo(a.DisplayName.Length);
        Assert.Equal([typeof(SortCharliePage), typeof(SortAlphaPage), typeof(SortBravoPage), typeof(SortZuluPage)], Children());

        PageRegistry.SortDescending = true;
        Assert.Equal([typeof(SortZuluPage), typeof(SortAlphaPage), typeof(SortBravoPage), typeof(SortCharliePage)], Children());
    });

    [AvaloniaFact]
    public void SettingTheSortToNullThrows() =>
        Assert.Throws<ArgumentNullException>(() => PageRegistry.Sort = null!);

    [AvaloniaFact]
    public void ChangingTheSortReordersAnOpenMenu() => WithSort(() =>
    {
        PageId parent = Parent();
        PageMenu menu = new() { ParentPage = parent.PageType };
        new Window { Content = menu }.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(typeof(SortZuluPage), menu.Items[0].PageType);

        int changes = 0;
        EventHandler onChanged = (_, _) => changes++;
        PageRegistry.PagesChanged += onChanged;
        try
        {
            PageRegistry.SortDescending = true;
            PageRegistry.SortDescending = true; // Unchanged: no event.
        }
        finally
        {
            PageRegistry.PagesChanged -= onChanged;
        }
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, changes);
        Assert.Equal(
            [typeof(SortCharliePage), typeof(SortBravoPage), typeof(SortAlphaPage), typeof(SortZuluPage)],
            menu.Items.Select(i => i.PageType)
        );
    });

    [AvaloniaFact]
    public void NiddyAppAppliesThePageSortOptions() => WithSort(() =>
    {
        Comparison<PageRegistration> byType = (a, b) => string.CompareOrdinal(a.PageType.Name, b.PageType.Name);
        new SortOptionsApp(o =>
        {
            o.PageSort = byType;
            o.PageSortDescending = true;
        }).Initialize();

        Assert.Same(byType, PageRegistry.Sort);
        Assert.True(PageRegistry.SortDescending);

        new SortOptionsApp(_ => { }).Initialize();
        Assert.Equal(PageRegistry.Alphabetical, PageRegistry.Sort);
        Assert.False(PageRegistry.SortDescending);
    });
}
