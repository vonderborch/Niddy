using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Dock.Controls.ProportionalStackPanel;
using Niddy.Avalonia.State;

namespace Niddy.Avalonia.Tests;

public sealed class StateTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "niddy-state-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private UiStateStore NewStore() => new(Path.Combine(_directory, "ui-state.json"), TimeSpan.FromMilliseconds(1));

    private static readonly PixelRect[] OneScreen = [new(0, 0, 1920, 1080)];

    [Theory]
    [InlineData(100, 100, true)]
    [InlineData(-700, 100, true)] // Mostly off the left edge, but the title bar's right end is grabbable.
    [InlineData(-790, 100, false)]
    [InlineData(100, 1075, false)] // Title bar below the working area.
    [InlineData(100, -30, false)] // Title bar above the top.
    [InlineData(2500, 100, false)] // On a screen that's gone.
    public void WindowsMustHaveAGrabbableTitleBar(int x, int y, bool visible) =>
        Assert.Equal(visible, WindowMemory.IsVisible(new PixelPoint(x, y), new PixelSize(800, 600), OneScreen));

    [Fact]
    public void WindowsCanBeOnAnyScreen() =>
        Assert.True(WindowMemory.IsVisible(new PixelPoint(2500, 100), new PixelSize(800, 600),
            [new PixelRect(0, 0, 1920, 1080), new PixelRect(1920, 0, 2560, 1440)]));

    [AvaloniaFact]
    public void RestoreAppliesSizeAndState()
    {
        var window = new Window { MinWidth = 500 };

        WindowMemory.Restore(window, new WindowPlacement(400, 700, State: WindowState.Maximized));

        Assert.Equal(500, window.Width);
        Assert.Equal(700, window.Height);
        Assert.Equal(WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void RestoreIgnoresMinimized()
    {
        var window = new Window();
        WindowMemory.Restore(window, new WindowPlacement(640, 480, State: WindowState.Minimized));
        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void WindowSizesAreSavedAndRestored()
    {
        using (var store = NewStore())
        {
            var window = new Window { Width = 640, Height = 480 };
            using var memory = WindowMemory.Attach(window, "Main", store);
            window.Show();
            window.Width = 900;
            window.Height = 700;
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Close();
            Assert.Equal(900, store.GetWindow("Main")!.Width);
        }

        using (var store = NewStore())
        {
            var window = new Window { Width = 100, Height = 100 };
            using var memory = WindowMemory.Attach(window, "Main", store);
            Assert.Equal(900, window.Width);
            Assert.Equal(700, window.Height);
        }
    }

    [AvaloniaFact]
    public void WindowsWithoutAStoreAreLeftAlone()
    {
        var previous = UiStateStore.Default;
        UiStateStore.Default = null;
        try
        {
            var window = new Window { Width = 640 };
            using var memory = WindowMemory.Attach(window, "Main");
            Assert.Equal(640, window.Width);
        }
        finally
        {
            UiStateStore.Default = previous;
        }
    }

    [AvaloniaFact]
    public void GridSizesAreSavedAndRestored()
    {
        using (var store = NewStore())
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
            using var memory = LayoutMemory.Attach(grid, "Split", store);
            grid.ColumnDefinitions[0].Width = new GridLength(320);
            store.Flush();
        }

        using (var store = NewStore())
        {
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*") };
            using var memory = LayoutMemory.Attach(grid, "Split", store);
            Assert.Equal(new GridLength(320), grid.ColumnDefinitions[0].Width);
            Assert.Equal(new GridLength(1, GridUnitType.Star), grid.ColumnDefinitions[1].Width);
        }
    }

    [AvaloniaFact]
    public void GridSizesAreIgnoredWhenTheColumnsChanged()
    {
        using var store = NewStore();
        store.SetLayout("Split/columns", ["320", "*"]);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("200,*,100") };
        using var memory = LayoutMemory.Attach(grid, "Split", store);

        Assert.Equal(new GridLength(200), grid.ColumnDefinitions[0].Width);
    }

    [AvaloniaFact]
    public void ProportionsAreSavedAndRestored()
    {
        using (var store = NewStore())
        {
            var left = new Border();
            var panel = new ProportionalStackPanel { Children = { left, new ProportionalStackPanelSplitter(), new Border() } };
            using var memory = LayoutMemory.Attach(panel, "Panes", store);
            ProportionalStackPanel.SetProportion(left, 0.25);
            ProportionalStackPanel.SetProportion(panel.Children[2], 0.75);
            store.Flush();
        }

        using (var store = NewStore())
        {
            var left = new Border();
            var panel = new ProportionalStackPanel { Children = { left, new ProportionalStackPanelSplitter(), new Border() } };
            using var memory = LayoutMemory.Attach(panel, "Panes", store);
            Assert.Equal(0.25, ProportionalStackPanel.GetProportion(left));
        }
    }

    [AvaloniaFact]
    public void OtherControlsAreNotSupported()
    {
        using var store = NewStore();
        Assert.Throws<NotSupportedException>(() => LayoutMemory.Attach(new StackPanel(), "x", store));
    }
}
