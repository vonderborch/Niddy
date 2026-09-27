using Niddy.Avalonia.PageSystem;

namespace Niddy.Avalonia.Tests;

public sealed class TestAppDataContext
{
    public string Name { get; init; } = "App";
}

public sealed class TrackingDataContext : IDisposable
{
    public bool IsDisposed { get; private set; }

    public void Dispose() => IsDisposed = true;
}

/// <summary>A page that records its lifecycle calls.</summary>
public abstract class TrackingPage : Page<TestAppDataContext, TrackingDataContext>
{
    public List<string> Events { get; } = new();

    public PageNavigationEventArgs? LastNavigatedTo { get; private set; }

    public bool IsClosed { get; private set; }

    public int NavigatedToCount => Events.Count(e => e == "to");

    protected override void OnNavigatedTo(PageNavigationEventArgs e)
    {
        Events.Add("to");
        LastNavigatedTo = e;
    }

    protected override void OnNavigatedFrom(PageNavigationEventArgs e) => Events.Add("from");

    protected override void OnClosed()
    {
        Events.Add("closed");
        IsClosed = true;
    }
}

[PageRegistration(DisplayName = "Home page", Children = [typeof(ItemPage)])]
[PageIcon("M0,0 L16,0 16,16 0,16Z", Size = 16)]
[PageIcon("M0,0 L24,0 24,24 0,24Z", Size = 24)]
public sealed class HomePage : TrackingPage;

[PageRegistration(Shortcut = "Primary+2")]
public sealed class DetailsPage : TrackingPage;

/// <summary>A child page with two parents: <see cref="HomePage"/> lists it as a child, and it names <see cref="DetailsPage"/>.</summary>
[PageRegistration(Parents = [typeof(DetailsPage)])]
public sealed class ItemPage : TrackingPage;

/// <summary>A kept-alive page with its own page view showing its child pages.</summary>
[PageRegistration(KeepAlive = true, Children = [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage)])]
public sealed class SettingsPage : TrackingPage
{
    public SettingsPage() => Content = Children = new PageView { StartingPage = typeof(GeneralSettingsPage) };

    public PageView Children { get; }
}

[PageRegistration]
public sealed class GeneralSettingsPage : TrackingPage;

/// <summary>Declared from both ends; it is still listed once.</summary>
[PageRegistration(Parents = [typeof(SettingsPage)])]
public sealed class AdvancedSettingsPage : TrackingPage;

/// <summary>A child page also listed with the top-level pages.</summary>
[PageRegistration(Parents = [typeof(SettingsPage)], TopLevel = true)]
public sealed class AboutPage : TrackingPage;

/// <summary>A page that can refuse to be left, like one with unsaved changes.</summary>
[PageRegistration]
public sealed class GuardedPage : TrackingPage
{
    public bool Block { get; set; }

    public PageNavigatingFromEventArgs? Refused { get; private set; }

    protected override void OnNavigatingFrom(PageNavigatingFromEventArgs e)
    {
        if (!Block)
            return;
        e.Cancel = true;
        Refused = e;
    }
}

/// <summary>A page without typed data contexts, hidden from the top-level pages although it has no parents.</summary>
[PageRegistration(TopLevel = false)]
public sealed class PlainPage : Page;

/// <summary>A page registered explicitly by the tests rather than by the generator.</summary>
public sealed class ManualPage : TrackingPage;

/// <summary>A page registered explicitly with a factory.</summary>
public sealed class FactoryPage(string title) : TrackingPage
{
    public string Title { get; } = title;
}

/// <summary>A page whose registration always fails, because it would be its own ancestor.</summary>
public sealed class CyclePage : TrackingPage;

/// <summary>A page that is only pushed, never registered.</summary>
public sealed class PushedPage(string title) : TrackingPage
{
    public string Title { get; } = title;
}

/// <summary>An unregistered parent page, so a test can register a child of it without touching other tests' pages.</summary>
public sealed class LateParentPage : TrackingPage;

/// <summary>A page registered late by a test, like a plugin page.</summary>
public sealed class LateChildPage : TrackingPage;
