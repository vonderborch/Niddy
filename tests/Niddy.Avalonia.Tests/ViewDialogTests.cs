using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Dialogs.BuiltIn;

namespace Niddy.Avalonia.Tests;

public class ViewDialogTests
{
    private static (DialogOverlayHost Host, TextBlock Inner) ShowOverlayHost()
    {
        TextBlock inner = new();
        DialogOverlayHost host = new() { Content = inner };
        new Window { Width = 1000, Height = 800, Content = host }.Show();
        return (host, inner);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static ViewDialog Shown(DialogOverlayHost host) =>
        Assert.IsType<ViewDialog>(host.FindControl<ContentPresenter>("DialogPresenter")!.Content);

    private static Border Card(DialogOverlayHost host) => host.FindControl<Border>("DialogCard")!;

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>Shows a view in an overlay, lays it out, and returns the dialog with the task to finish it.</summary>
    private static (ViewDialog Dialog, Task<bool> Result) Open(DialogOverlayHost host, TextBlock inner, Control view,
        ViewFit fit = ViewFit.Stretch, bool fill = false, double? contentHeight = null)
    {
        Task<bool> result = Dialog.View.Open(inner, view, "Title", fit: fit, fill: fill, contentHeight: contentHeight,
            displayMode: DialogDisplayMode.Overlay);
        Pump();
        host.UpdateLayout();
        return (Shown(host), result);
    }

    [AvaloniaFact]
    public async Task Control_IsShownUnderTheTitleAndOkReturnsTrue()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        TextBox view = new();

        Task<bool> result = Dialog.View.Open(inner, view, "Rename", "Pick a new name.", displayMode: DialogDisplayMode.Overlay);
        Pump();
        ViewDialog dialog = Shown(host);
        Assert.Same(view, dialog.Body.Content);
        Assert.Equal("Rename", dialog.Title);
        Assert.Equal("Pick a new name.", dialog.Description);
        Assert.True(dialog.ButtonOk.IsVisible);
        Assert.True(dialog.ButtonCancel.IsVisible);
        Assert.Equal(500, Card(host).MaxWidth);

        Click(dialog.ButtonOk);

        Assert.True(await result);
        Assert.Null(view.Parent);
    }

    [AvaloniaFact]
    public async Task Cancel_AndEscape_ReturnFalse()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<bool> cancelled = Dialog.View.Open(inner, new TextBox(), displayMode: DialogDisplayMode.Overlay);
        Pump();
        Click(Shown(host).ButtonCancel);
        Assert.False(await cancelled);

        Task<bool> escaped = Dialog.View.Open(inner, new TextBox(), displayMode: DialogDisplayMode.Overlay);
        Pump();
        Shown(host).RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.False(await escaped);
    }

    [AvaloniaFact]
    public void NullButtonTexts_HideTheButtons_AndWithoutCancelEscapeDoesNothing()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<bool> result = Dialog.View.Open(inner, new TextBox(), okButtonText: null, cancelButtonText: null,
            displayMode: DialogDisplayMode.Overlay);
        Pump();
        ViewDialog dialog = Shown(host);
        Assert.False(dialog.ButtonOk.IsVisible);
        Assert.False(dialog.ButtonCancel.IsVisible);

        dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Pump();
        Assert.False(result.IsCompleted);
    }

    [AvaloniaFact]
    public async Task CanAccept_KeepsTheDialogOpenUntilItAllows()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        TextBox view = new();

        Task<bool> result = Dialog.View.Open(inner, view, canAccept: () => !string.IsNullOrEmpty(view.Text),
            displayMode: DialogDisplayMode.Overlay);
        Pump();
        ViewDialog dialog = Shown(host);

        Click(dialog.ButtonOk);
        Pump();
        Assert.False(result.IsCompleted);

        view.Text = "Ada";
        Click(dialog.ButtonOk);
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Close_FromInsideTheView_ReturnsItsResult()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Button pick = new();
        StackPanel view = new() { Children = { pick } };

        Task<bool> result = Dialog.View.Open(inner, view, canAccept: () => false, displayMode: DialogDisplayMode.Overlay);
        Pump();
        Assert.True(Dialog.View.Close(pick));

        Assert.True(await result);
        Assert.False(Dialog.View.Close(pick));
    }

    [AvaloniaFact]
    public async Task Window_ShowsItsContentWithItsTitleSizeAndDataContext_ThenGetsItBack()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        TextBlock content = new();
        object model = new();
        Window window = new() { Title = "Settings", Width = 400, Height = 300, Content = content, DataContext = model };

        Task<bool> result = Dialog.View.Open(inner, window, displayMode: DialogDisplayMode.Overlay);
        Pump();
        ViewDialog dialog = Shown(host);
        Assert.Same(content, dialog.Body.Content);
        Assert.Null(window.Content);
        Assert.Equal("Settings", dialog.Title);
        Assert.Same(model, content.DataContext);
        Assert.Equal(440, Card(host).MaxWidth);
        Assert.Equal(300, dialog.Area.Height);

        Click(dialog.ButtonOk);

        Assert.True(await result);
        Assert.Same(content, window.Content);
        Assert.Same(window, content.Parent);
        Assert.False(window.IsVisible);
    }

    [AvaloniaFact]
    public async Task Window_ArgumentsOverrideItsTitleAndSize()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Window window = new() { Title = "Settings", Width = 400, Height = 300, Content = new TextBlock() };

        Task<bool> result = Dialog.View.Open(inner, window, "Options", maxWidth: 700, contentHeight: 120,
            displayMode: DialogDisplayMode.Overlay);
        Pump();
        ViewDialog dialog = Shown(host);
        Assert.Equal("Options", dialog.Title);
        Assert.Equal(700, Card(host).MaxWidth);
        Assert.Equal(120, dialog.Area.Height);

        Click(dialog.ButtonCancel);
        Assert.False(await result);
    }

    [AvaloniaFact]
    public async Task OpenWindow_Throws()
    {
        (_, TextBlock inner) = ShowOverlayHost();
        Window window = new() { Content = new TextBlock() };
        window.Show();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Dialog.View.Open(inner, window, displayMode: DialogDisplayMode.Overlay));
        Assert.NotNull(window.Content);
    }

    [AvaloniaFact]
    public async Task ControlAlreadyShown_Throws()
    {
        (_, TextBlock inner) = ShowOverlayHost();

        await Assert.ThrowsAsync<InvalidOperationException>(() => Dialog.View.Open(inner, inner, displayMode: DialogDisplayMode.Overlay));
    }

    [AvaloniaFact]
    public async Task BadContentHeight_LeavesTheWindowAsItWas()
    {
        (_, TextBlock inner) = ShowOverlayHost();
        TextBlock content = new();
        Window window = new() { Content = content };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Dialog.View.Open(inner, window, contentHeight: -1, displayMode: DialogDisplayMode.Overlay));
        Assert.Same(content, window.Content);
    }

    [AvaloniaFact]
    public async Task View_CanBeShownAgain()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        TextBox view = new();

        for (int i = 0; i < 2; i++)
        {
            Task<bool> result = Dialog.View.Open(inner, view, displayMode: DialogDisplayMode.Overlay);
            Pump();
            Assert.Same(view, Shown(host).Body.Content);
            Click(Shown(host).ButtonOk);
            Assert.True(await result);
        }
    }

    [AvaloniaFact]
    public async Task Stretch_GivesTheViewTheAreasWidth()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Border view = new() { Height = 50 };

        (ViewDialog dialog, Task<bool> result) = Open(host, inner, view);
        Assert.Equal(dialog.Area.Bounds.Width, view.Bounds.Width);
        Assert.Same(dialog.Body, dialog.Area.Child);

        Click(dialog.ButtonOk);
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Center_KeepsTheViewsSizeInTheMiddle()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Border view = new() { Width = 100, Height = 50 };

        (ViewDialog dialog, Task<bool> result) = Open(host, inner, view, ViewFit.Center, contentHeight: 150);
        Assert.Equal(new Size(100, 50), view.Bounds.Size);
        Point center = view.TranslatePoint(new Point(50, 25), dialog.Area)!.Value;
        Assert.Equal(dialog.Area.Bounds.Width / 2, center.X, 1);
        Assert.Equal(75, center.Y, 1);

        Click(dialog.ButtonOk);
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Scroll_ScrollsABigViewBothWays_InsideAnAreaCappedAt400()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Border view = new() { Width = 2000, Height = 1000 };

        (ViewDialog dialog, Task<bool> result) = Open(host, inner, view, ViewFit.Scroll);
        ScrollViewer scroller = Assert.IsType<ScrollViewer>(dialog.Area.Child);
        Assert.Same(dialog.Body, scroller.Content);
        Assert.Equal(400, dialog.Area.Bounds.Height);
        Assert.Equal(new Size(2000, 1000), view.Bounds.Size);
        Assert.Equal(new Size(2000, 1000), scroller.Extent);

        Click(dialog.ButtonOk);
        Assert.True(await result);
        Assert.Null(view.Parent);
    }

    [AvaloniaFact]
    public async Task Scroll_FitsASmallView_AndTakesAContentHeight()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        (ViewDialog small, Task<bool> first) = Open(host, inner, new Border { Height = 80 }, ViewFit.Scroll);
        Assert.Equal(80, small.Area.Bounds.Height);
        Click(small.ButtonOk);
        await first;

        (ViewDialog tall, Task<bool> second) = Open(host, inner, new Border { Height = 80 }, ViewFit.Scroll, contentHeight: 600);
        Assert.Equal(600, tall.Area.Bounds.Height);
        Click(tall.ButtonOk);
        await second;
    }

    [AvaloniaFact]
    public async Task Shrink_ScalesABigViewDown_ButNotASmallOneUp()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        (ViewDialog big, Task<bool> first) = Open(host, inner, new Border { Width = 2000, Height = 1000 }, ViewFit.Shrink);
        Viewbox box = Assert.IsType<Viewbox>(big.Area.Child);
        Assert.Equal(big.Area.Bounds.Width, box.Bounds.Width, 1);
        Assert.Equal(big.Area.Bounds.Width / 2, box.Bounds.Height, 1);
        Click(big.ButtonOk);
        await first;

        (ViewDialog small, Task<bool> second) = Open(host, inner, new Border { Width = 100, Height = 50 }, ViewFit.Shrink);
        Viewbox smallBox = Assert.IsType<Viewbox>(small.Area.Child);
        Assert.Equal(new Size(100, 50), smallBox.Bounds.Size);
        Click(small.ButtonOk);
        await second;
    }

    [AvaloniaFact]
    public async Task Scale_ScalesASmallViewUpToTheAreasWidthOr400High()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        (ViewDialog wide, Task<bool> first) = Open(host, inner, new Border { Width = 100, Height = 50 }, ViewFit.Scale);
        Viewbox box = Assert.IsType<Viewbox>(wide.Area.Child);
        Assert.Equal(wide.Area.Bounds.Width, box.Bounds.Width, 1);
        Assert.Equal(wide.Area.Bounds.Width / 2, box.Bounds.Height, 1);
        Click(wide.ButtonOk);
        await first;

        (ViewDialog tall, Task<bool> second) = Open(host, inner, new Border { Width = 50, Height = 100 }, ViewFit.Scale);
        Assert.Equal(400, tall.Area.Bounds.Height, 1);
        Assert.Equal(200, Assert.IsType<Viewbox>(tall.Area.Child).Bounds.Width, 1);
        Click(tall.ButtonOk);
        await second;
    }

    [AvaloniaFact]
    public async Task Fill_TakesTheWholeHost_ThenTheNextDialogFitsAgain()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Border view = new();

        (ViewDialog dialog, Task<bool> result) = Open(host, inner, view, fill: true);
        Assert.Equal(new Size(1000 - 32, 800 - 32), Card(host).Bounds.Size);
        Assert.Equal(dialog.Area.Bounds.Size, view.Bounds.Size);
        Assert.True(view.Bounds.Height > 600, $"The view is only {view.Bounds.Height} high.");
        Point buttons = dialog.ButtonOk.TranslatePoint(default, Card(host))!.Value;
        Assert.True(buttons.Y > 700, "The buttons should be at the bottom of the card.");
        Click(dialog.ButtonOk);
        Assert.True(await result);

        (ViewDialog next, Task<bool> second) = Open(host, inner, new Border { Height = 50 });
        Assert.Equal(300, Card(host).Bounds.Width);   // centered again, at its minimum width for a view that doesn't ask for more
        Assert.Equal(50, next.Area.Bounds.Height);
        Assert.True(Card(host).Bounds.Height < 300);
        Click(next.ButtonOk);
        await second;
    }

    [AvaloniaFact]
    public async Task Fill_WithScroll_ScrollsInsideTheWholeArea()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        (ViewDialog dialog, Task<bool> result) = Open(host, inner, new Border { Width = 2000, Height = 2000 }, ViewFit.Scroll, fill: true);
        Assert.True(dialog.Area.Bounds.Height > 600, $"The area is only {dialog.Area.Bounds.Height} high.");
        Assert.Equal(dialog.Area.Bounds.Size, Assert.IsType<ScrollViewer>(dialog.Area.Child).Viewport);

        Click(dialog.ButtonOk);
        await result;
    }

    [AvaloniaFact]
    public async Task Fill_InAWindow_IsResizableAndMostOfTheOwner()
    {
        Window owner = new() { Width = 1000, Height = 800, Content = new TextBlock() };
        owner.Show();
        Border view = new();

        Task<bool> result = Dialog.View.Open((Control)owner.Content!, view, "Big", fill: true, displayMode: DialogDisplayMode.Window);
        Pump();
        Window window = Assert.Single(owner.OwnedWindows);
        Assert.True(window.CanResize);
        Assert.Equal(800, window.Width);
        Assert.Equal(640, window.Height);
        Assert.Equal(SizeToContent.Manual, window.SizeToContent);
        Assert.IsNotType<ScrollViewer>(window.Content);

        Assert.True(Dialog.View.Close(view));
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Fill_InAWindow_UsesTheGivenSize()
    {
        Window owner = new() { Width = 1000, Height = 800, Content = new TextBlock() };
        owner.Show();
        Border view = new();

        Task<bool> result = Dialog.View.Open((Control)owner.Content!, view, maxWidth: 600, windowHeight: 500, fill: true,
            displayMode: DialogDisplayMode.Window);
        Pump();
        Window window = Assert.Single(owner.OwnedWindows);
        Assert.Equal(600, window.Width);
        Assert.Equal(500, window.Height);

        Assert.True(Dialog.View.Close(view));
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Fill_IgnoresAWindowsSize()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        Window window = new() { Width = 300, Height = 200, Content = new TextBlock() };

        Task<bool> result = Dialog.View.Open(inner, window, fill: true, displayMode: DialogDisplayMode.Overlay);
        Pump();
        ViewDialog dialog = Shown(host);
        Assert.Equal(double.PositiveInfinity, Card(host).MaxWidth);
        Assert.True(double.IsNaN(dialog.Area.Height));

        Click(dialog.ButtonOk);
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task Fill_WithAContentHeight_Throws_AndLeavesTheWindowAsItWas()
    {
        (_, TextBlock inner) = ShowOverlayHost();
        TextBlock content = new();
        Window window = new() { Content = content };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            Dialog.View.Open(inner, window, contentHeight: 300, fill: true, displayMode: DialogDisplayMode.Overlay));
        Assert.Same(content, window.Content);
    }

    [AvaloniaFact]
    public async Task UnknownFit_Throws()
    {
        (_, TextBlock inner) = ShowOverlayHost();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            Dialog.View.Open(inner, new TextBox(), fit: (ViewFit)99, displayMode: DialogDisplayMode.Overlay));
    }
}
