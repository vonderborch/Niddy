using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Niddy.Avalonia.Dialogs;

namespace Niddy.Avalonia.Tests;

public class DialogTests
{
    private static (DialogOverlayHost Host, TextBlock Inner) ShowOverlayHost()
    {
        var inner = new TextBlock();
        var host = new DialogOverlayHost { Content = inner };
        new Window { Content = host }.Show();
        return (host, inner);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static Control? ShownDialog(DialogOverlayHost host) =>
        host.FindControl<ContentPresenter>("DialogPresenter")!.Content as Control;

    [AvaloniaFact]
    public async Task OverlayConfirmation_ReturnsTheClickedButton()
    {
        var (host, inner) = ShowOverlayHost();

        var result = Dialog.Confirmation.Open(inner, "Title", "Description");
        Pump();
        var dialog = Assert.IsType<ConfirmationDialog>(ShownDialog(host));
        Assert.Equal("Title", dialog.Title);

        dialog.ButtonYes.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.True(await result);
        Pump();
        Assert.Null(ShownDialog(host));
    }

    [AvaloniaFact]
    public async Task Dismissing_ReturnsTheDefaultResult()
    {
        var (host, inner) = ShowOverlayHost();

        var result = Dialog.Confirmation.Open(inner, "Title", "Description", defaultResult: true);
        Pump();

        Assert.True(((IDialog)ShownDialog(host)!).TryDismiss());
        Assert.True(await result);
    }

    [AvaloniaFact]
    public void DialogsWithoutCancelCannotBeDismissed()
    {
        var (host, inner) = ShowOverlayHost();

        var result = Dialog.Input.Open(inner, "Title", "Description", showCancelButton: false);
        Pump();

        Assert.False(((IDialog)ShownDialog(host)!).TryDismiss());
        Assert.False(result.IsCompleted);
        Assert.True(((IDialog)ShownDialog(host)!).TryDismiss(force: true));
        Pump();
        Assert.True(result.IsCompleted);
    }

    [AvaloniaFact]
    public async Task OverlayDialogsStack()
    {
        var (host, inner) = ShowOverlayHost();

        var first = Dialog.Notification.Open(inner, "First", "");
        Pump();
        var firstDialog = ShownDialog(host);
        var second = Dialog.Notification.Open(inner, "Second", "");
        Pump();
        Assert.NotSame(firstDialog, ShownDialog(host));

        ((IDialog)ShownDialog(host)!).TryDismiss();
        await second;
        Pump();

        Assert.Same(firstDialog, ShownDialog(host));
        Assert.False(first.IsCompleted);
    }

    [AvaloniaFact]
    public async Task Progress_ReturnsTrueWhenTheWorkCompletes()
    {
        var (_, inner) = ShowOverlayHost();

        var result = await Dialog.Progress.Open(inner, "Working", "", async (progress, token) =>
        {
            progress.Report(50);
            await Task.Delay(10, token);
        });

        Assert.True(result);
    }

    [AvaloniaFact]
    public async Task WindowDialog_UsesTheFixedSize()
    {
        var owner = new Window { Content = new TextBlock() };
        owner.Show();

        var result = Dialog.Notification.Open((Control)owner.Content!, "Title", "", displayMode: DialogDisplayMode.Window,
            windowWidth: 320, windowHeight: 240);
        Pump();

        var window = Assert.Single(owner.OwnedWindows);
        Assert.Equal("Title", window.Title);
        Assert.Equal(320, window.Width);
        Assert.Equal(240, window.Height);
        Assert.Equal(SizeToContent.Manual, window.SizeToContent);

        window.Close();
        Assert.False(await result.ContinueWith(_ => false));
    }

    [AvaloniaFact]
    public void DialogLayout_HidesEmptyPartsAndShowsTheRest()
    {
        var layout = new DialogLayout { Title = "Title", Content = new TextBlock { Text = "Body" } };
        new Window { Content = layout }.Show();

        var texts = layout.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text);
        Assert.Equal(["Title", "Body"], texts);

        layout.Buttons.Add(new Button());
        Assert.True(layout.Buttons[0].IsEffectivelyVisible);
    }
}
