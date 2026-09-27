using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Threading;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.Toast;

namespace Niddy.Avalonia.Tests;

public class ToastHostTests
{
    private static (ToastHost Host, Window Window) Show(double width = 800, Action<ToastHost>? configure = null)
    {
        var host = new ToastHost { Content = new Border() };
        configure?.Invoke(host);
        var window = new Window { Width = width, Height = 600, Content = host };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (host, window);
    }

    private static ItemsControl List(ToastHost host) => host.FindControl<ItemsControl>("ToastList")!;

    [AvaloniaTheory]
    [InlineData(ScreenPlacement.TopLeft, HorizontalAlignment.Left, VerticalAlignment.Top)]
    [InlineData(ScreenPlacement.TopCenter, HorizontalAlignment.Center, VerticalAlignment.Top)]
    [InlineData(ScreenPlacement.TopRight, HorizontalAlignment.Right, VerticalAlignment.Top)]
    [InlineData(ScreenPlacement.CenterLeft, HorizontalAlignment.Left, VerticalAlignment.Center)]
    [InlineData(ScreenPlacement.CenterRight, HorizontalAlignment.Right, VerticalAlignment.Center)]
    [InlineData(ScreenPlacement.BottomLeft, HorizontalAlignment.Left, VerticalAlignment.Bottom)]
    [InlineData(ScreenPlacement.BottomCenter, HorizontalAlignment.Center, VerticalAlignment.Bottom)]
    [InlineData(ScreenPlacement.BottomRight, HorizontalAlignment.Right, VerticalAlignment.Bottom)]
    public void ToastsAreShownAtThePlacement(ScreenPlacement placement, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var (host, _) = Show(configure: h => h.Placement = placement);

        Assert.Equal(placement, host.ActualPlacement);
        Assert.Equal(horizontal, List(host).HorizontalAlignment);
        Assert.Equal(vertical, List(host).VerticalAlignment);
    }

    [AvaloniaFact]
    public void NarrowHostsUseTheNarrowPlacement()
    {
        var (host, window) = Show(configure: h => h.Placement = ScreenPlacement.TopRight);
        Assert.Equal(ScreenPlacement.TopRight, host.ActualPlacement);

        window.Width = 400;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ScreenPlacement.BottomCenter, host.ActualPlacement);
        Assert.Equal(HorizontalAlignment.Center, List(host).HorizontalAlignment);

        host.NarrowPlacement = null;
        Assert.Equal(ScreenPlacement.TopRight, host.ActualPlacement);

        host.NarrowPlacement = ScreenPlacement.TopCenter;
        window.Width = 800;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(ScreenPlacement.TopRight, host.ActualPlacement);
    }

    [AvaloniaTheory]
    [InlineData(ScreenPlacement.TopCenter, true)]
    [InlineData(ScreenPlacement.BottomRight, false)]
    [InlineData(ScreenPlacement.CenterLeft, false)]
    public void TheNewestToastIsNearestTheEdge(ScreenPlacement placement, bool newestFirst)
    {
        var (host, _) = Show(configure: h => h.Placement = placement);
        var first = new ToastItem("first", ToastType.Info, TimeSpan.FromMinutes(1));
        var second = new ToastItem("second", ToastType.Info, TimeSpan.FromMinutes(1));

        host.AddToast(first);
        host.AddToast(second);

        var items = List(host).Items.Cast<ToastItem>().ToList();
        Assert.Equal(newestFirst ? [second, first] : [first, second], items);
    }
}
