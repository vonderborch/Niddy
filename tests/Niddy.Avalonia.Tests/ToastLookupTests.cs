using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Niddy.Avalonia.Hosting;
using Niddy.Avalonia.PageSystem;
using Niddy.Avalonia.Toasts;

namespace Niddy.Avalonia.Tests;

[Collection(typeof(GlobalStateCollection))]
public class ToastLookupTests
{
    /// <summary>Holds its child as a visual child only, like a template part: the child has no logical parent.</summary>
    private sealed class VisualOnlyHolder : Control
    {
        public VisualOnlyHolder(Control child) => VisualChildren.Add(child);
    }

    [AvaloniaFact]
    public void TheHostItselfIsFound()
    {
        ToastHost host = new();
        Assert.Same(host, ToastHost.FindInVisualTree(host));
    }

    [AvaloniaFact]
    public void AHostThatIsOnlyAVisualAncestorIsFound()
    {
        Border inner = new();
        ToastHost host = new() { Content = new VisualOnlyHolder(inner) };
        new Window { Content = host }.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Null(inner.Parent);
        Assert.Same(host, ToastHost.FindInVisualTree(inner));
    }

    [AvaloniaFact]
    public void AControlWithoutAHostUsesTheAppsHost()
    {
        AppShell shell = new(new NiddyAppOptions { StartingPage = HomePage.PageId, AppDataContext = new TestAppDataContext() });
        new Window { Content = shell }.Show();
        Func<ToastHost?> original = Toast.AppHost;
        Toast.AppHost = () => shell.Toasts;
        try
        {
            // E.g. a page that isn't attached yet.
            Toast.Show(new Border(), "Saved");
            Toast.ShowProgress(new Border(), "Exporting");
        }
        finally
        {
            Toast.AppHost = original;
        }

        Assert.Equal(["Exporting", "Saved"], shell.Toasts.Toasts.Select(t => t.Message).Order());
    }

    [AvaloniaFact]
    public void AControlWithoutAHostThrowsWhenTheAppIsntANiddyApp()
    {
        Assert.Null(Toast.AppHost());
        Assert.Throws<InvalidOperationException>(() => Toast.Show(new Border(), "Saved"));
        Assert.Throws<InvalidOperationException>(() => Toast.ShowProgress(new Border(), "Exporting"));
    }
}
