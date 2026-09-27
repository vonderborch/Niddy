using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Niddy.Avalonia.PageSystem;

namespace Niddy.Avalonia.Tests;

public class PageIconTests
{
    private static readonly PageIconSource Any = PageIconSource.FromData("M0,0 L1,1", 0);
    private static readonly PageIconSource Data16 = PageIconSource.FromData("M0,0 L16,16", 16);
    private static readonly PageIconSource Data24 = PageIconSource.FromData("M0,0 L24,24", 24);
    private static readonly PageIconSource Png32 = PageIconSource.FromUri("avares://App/icon32.png", 32);
    private static readonly PageIconSource Png64 = PageIconSource.FromUri("avares://App/icon64.png", 64);
    private static readonly PageIconSource PngAny = PageIconSource.FromUri("avares://App/icon.png");

    [Fact]
    public void ASourceMadeForTheSizeWins()
    {
        var icon = new PageIcon(Any, Data16, Data24, Png32);

        Assert.Same(Data16, icon.GetSource(16));
        Assert.Same(Data24, icon.GetSource(24));
        Assert.Same(Png32, new PageIcon(Any, Png32).GetSource(16, scaling: 2));
    }

    [Fact]
    public void ScalableSourcesComeBeforeBitmaps()
    {
        Assert.Same(Any, new PageIcon(Png32, Any, Data16).GetSource(20));
        Assert.Same(Data24, new PageIcon(Png32, Data16, Data24).GetSource(21));
        Assert.Same(Data24, new PageIcon(Data16, Data24).GetSource(20));
    }

    [Fact]
    public void BitmapsAreScaledDownRatherThanUp()
    {
        var icon = new PageIcon(Png32, Png64);

        Assert.Same(Png32, icon.GetSource(20));
        Assert.Same(Png64, icon.GetSource(20, scaling: 2));
        Assert.Same(Png64, icon.GetSource(100));
        Assert.Same(PngAny, new PageIcon(Png32, PngAny).GetSource(48));
    }

    [Fact]
    public void IconsAreEqualByTheirSources()
    {
        Assert.Equal(new PageIcon(Data16, Png32), new PageIcon(PageIconSource.FromData("M0,0 L16,16", 16), Png32));
        Assert.NotEqual(new PageIcon(Data16, Png32), new PageIcon(Png32, Data16));
        Assert.NotEqual(PageIconSource.FromData("M0,0 L16,16", 16), PageIconSource.FromData("M0,0 L16,16", 24));
        Assert.Throws<ArgumentException>(() => new PageIcon());
    }

    [Fact]
    public void GeneratedIconsHaveEverySize()
    {
        var icon = PageRegistry.Find(HomePage.PageId)!.Icon!;

        Assert.Equal([16d, 24d], icon.Sources.Select(s => s.Size));
        Assert.Equal("M0,0 L24,0 24,24 0,24Z", icon.GetSource(24).Data);
        Assert.Equal("M0,0 L16,0 16,16 0,16Z", icon.GetSource(16).Data);
    }

    [AvaloniaFact]
    public void TheViewTakesItsSizeAndPicksTheSourceForIt()
    {
        var view = new PageIconView { Icon = new PageIcon(Data16, Data24), IconSize = 24, Foreground = Brushes.Black };
        var window = new Window { Content = new StackPanel { Children = { view } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(new Size(24, 24), view.DesiredSize);
        Assert.Same(Data24, view.CurrentSource);

        view.IconSize = 16;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Size(16, 16), view.DesiredSize);
        Assert.Same(Data16, view.CurrentSource);

        view.Icon = null;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(default, view.DesiredSize);
        Assert.Null(view.CurrentSource);
    }

    [AvaloniaFact]
    public void ResourceIconsAreLookedUp()
    {
        var geometry = Geometry.Parse("M0,0 L10,10");
        var view = new PageIconView { Icon = new PageIcon(PageIconSource.FromResource("TestIcon")) };
        view.Resources["TestIcon"] = geometry;
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Same(geometry, view.CurrentSource!.Resolve(view));
    }
}
