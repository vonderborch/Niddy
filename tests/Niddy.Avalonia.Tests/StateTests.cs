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
        {
            Directory.Delete(_directory, recursive: true);
        }
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
        Window window = new() { MinWidth = 500 };

        WindowMemory.Restore(window, new WindowPlacement(400, 700, State: WindowState.Maximized));

        Assert.Equal(500, window.Width);
        Assert.Equal(700, window.Height);
        Assert.Equal(WindowState.Maximized, window.WindowState);
    }

    [AvaloniaFact]
    public void RestoreIgnoresMinimized()
    {
        Window window = new();
        WindowMemory.Restore(window, new WindowPlacement(640, 480, State: WindowState.Minimized));
        Assert.Equal(WindowState.Normal, window.WindowState);
    }

    [AvaloniaFact]
    public void WindowSizesAreSavedAndRestored()
    {
        using (UiStateStore store = NewStore())
        {
            Window window = new() { Width = 640, Height = 480 };
            using IDisposable memory = WindowMemory.Attach(window, "Main", store);
            window.Show();
            window.Width = 900;
            window.Height = 700;
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            window.Close();
            Assert.Equal(900, store.GetWindow("Main")!.Width);
        }

        using (UiStateStore store = NewStore())
        {
            Window window = new() { Width = 100, Height = 100 };
            using IDisposable memory = WindowMemory.Attach(window, "Main", store);
            Assert.Equal(900, window.Width);
            Assert.Equal(700, window.Height);
        }
    }

    [AvaloniaFact]
    public void WindowsWithoutAStoreAreLeftAlone()
    {
        UiStateStore? previous = UiStateStore.Default;
        UiStateStore.Default = null;
        try
        {
            Window window = new() { Width = 640 };
            using IDisposable memory = WindowMemory.Attach(window, "Main");
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
        using (UiStateStore store = NewStore())
        {
            Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("200,*") };
            using IDisposable memory = LayoutMemory.Attach(grid, "Split", store);
            grid.ColumnDefinitions[0].Width = new GridLength(320);
            store.Flush();
        }

        using (UiStateStore store = NewStore())
        {
            Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("200,*") };
            using IDisposable memory = LayoutMemory.Attach(grid, "Split", store);
            Assert.Equal(new GridLength(320), grid.ColumnDefinitions[0].Width);
            Assert.Equal(new GridLength(1, GridUnitType.Star), grid.ColumnDefinitions[1].Width);
        }
    }

    [AvaloniaFact]
    public void GridSizesAreIgnoredWhenTheColumnsChanged()
    {
        using UiStateStore store = NewStore();
        store.SetLayout("Split/columns", ["320", "*"]);

        Grid grid = new() { ColumnDefinitions = new ColumnDefinitions("200,*,100") };
        using IDisposable memory = LayoutMemory.Attach(grid, "Split", store);

        Assert.Equal(new GridLength(200), grid.ColumnDefinitions[0].Width);
    }

    [AvaloniaFact]
    public void ProportionsAreSavedAndRestored()
    {
        using (UiStateStore store = NewStore())
        {
            Border left = new();
            ProportionalStackPanel panel = new() { Children = { left, new ProportionalStackPanelSplitter(), new Border() } };
            using IDisposable memory = LayoutMemory.Attach(panel, "Panes", store);
            ProportionalStackPanel.SetProportion(left, 0.25);
            ProportionalStackPanel.SetProportion(panel.Children[2], 0.75);
            store.Flush();
        }

        using (UiStateStore store = NewStore())
        {
            Border left = new();
            ProportionalStackPanel panel = new() { Children = { left, new ProportionalStackPanelSplitter(), new Border() } };
            using IDisposable memory = LayoutMemory.Attach(panel, "Panes", store);
            Assert.Equal(0.25, ProportionalStackPanel.GetProportion(left));
        }
    }

    [AvaloniaFact]
    public void OtherControlsAreNotSupported()
    {
        using UiStateStore store = NewStore();
        Assert.Throws<NotSupportedException>(() => LayoutMemory.Attach(new StackPanel(), "x", store));
    }

    [Fact]
    public void DisposingSavesPendingChanges()
    {
        string path = Path.Combine(_directory, "ui-state.json");
        using (UiStateStore store = new(path, TimeSpan.FromHours(1)))
        {
            store.SetLayout("Split", ["1*", "2*"]);
            Assert.True(store.HasPendingChanges);
        }

        using UiStateStore reloaded = new(path);
        Assert.Equal(["1*", "2*"], reloaded.GetLayout("Split"));
    }

    [Fact]
    public void AFailedSaveIsReportedNotThrown()
    {
        // A directory where the file should be makes every save fail.
        string path = Path.Combine(_directory, "ui-state.json");
        Directory.CreateDirectory(path);
        using UiStateStore store = new(path, TimeSpan.FromHours(1));
        store.SetLayout("Split", ["1*"]);

        // Without an error handler the debouncer would rethrow on the thread pool and take the test host down.
        store.Flush();
        Assert.False(store.HasPendingChanges);
    }
}
