using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;
using Niddy.Avalonia.Toast;

namespace Niddy.Avalonia.Tests;

public class HostingTests
{
    [AvaloniaFact]
    public void ShellPutsPagesUnderToastsUnderDialogs()
    {
        var shell = new AppShell(
            new NiddyAppOptions { StartingPage = HomePage.PageId, AppDataContext = new TestAppDataContext(), ToastPlacement = ScreenPlacement.TopCenter },
            AppMode.Desktop
        );
        new Window { Content = shell }.Show();

        Assert.Same(shell.Toasts, ToastHost.FindInVisualTree(shell.Pages));
        Assert.Same(shell.Dialogs, DialogOverlayHost.FindInVisualTree(shell.Pages));
        Assert.Same(shell.Dialogs, DialogOverlayHost.FindInVisualTree(shell.Toasts));
        Assert.Equal(ScreenPlacement.TopCenter, shell.Toasts.Placement);
        Assert.IsType<HomePage>(shell.Pages.FindControl<TransitioningContentControl>("PageContent")!.Content);
    }

    [AvaloniaFact]
    public void LayoutWrapsThePageView()
    {
        var options = new NiddyAppOptions
        {
            StartingPage = HomePage.PageId,
            Layout = pages => new DockPanel { Children = { new TextBlock { Text = "Nav" }, pages } },
        };
        var shell = new AppShell(options, AppMode.Desktop);

        Assert.IsType<DockPanel>(shell.Toasts.Content);
        Assert.Same(shell.Toasts.Content, shell.Pages.Parent);
    }

    [AvaloniaFact]
    public void ShellPassesTheAppDataContextToPages()
    {
        var app = new TestAppDataContext();
        var shell = new AppShell(new NiddyAppOptions { StartingPage = HomePage.PageId, AppDataContext = app }, AppMode.Desktop);
        new Window { Content = shell }.Show();

        Assert.Same(app, Assert.IsType<HomePage>(shell.Pages.PageControlDataContext.ActivePage).AppDataContext);
    }

    [AvaloniaFact]
    public void MobileModeUsesTheNarrowToastPlacement()
    {
        var options = new NiddyAppOptions { StartingPage = HomePage.PageId };

        var mobile = new AppShell(options, AppMode.Mobile).Toasts;
        Assert.Equal(ScreenPlacement.BottomRight, mobile.Placement);
        Assert.Equal(ScreenPlacement.BottomCenter, mobile.ActualPlacement);
        Assert.Equal(ScreenPlacement.BottomRight, new AppShell(options, AppMode.Desktop).Toasts.ActualPlacement);

        options.ToastPlacement = ScreenPlacement.TopLeft;
        options.ToastNarrowPlacement = null;
        Assert.Equal(ScreenPlacement.TopLeft, new AppShell(options, AppMode.Mobile).Toasts.ActualPlacement);
    }

    [Theory]
    [InlineData(AppMode.Desktop, null, AppMode.Desktop)]
    [InlineData(AppMode.Mobile, "desktop", AppMode.Mobile)]
    [InlineData(AppMode.Auto, "desktop", AppMode.Desktop)]
    [InlineData(AppMode.Auto, null, AppMode.Desktop)] // Tests run on a desktop OS.
    public void ModeIsExplicitOrDecidedByTheLifetime(AppMode requested, string? lifetime, AppMode expected)
    {
        IApplicationLifetime? appLifetime = lifetime switch
        {
            "desktop" => new ClassicDesktopStyleApplicationLifetime(),
            _ => null,
        };

        Assert.Equal(expected, NiddyApp.ResolveMode(requested, appLifetime));
    }

    [AvaloniaFact]
    public void FluentStylesLoad()
    {
        foreach (var style in NiddyApp.CreateFluentStyles().OfType<StyleInclude>())
            Assert.NotNull(style.Loaded);
    }
}
