using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Dialogs.BuiltIn;

namespace Niddy.Avalonia.Tests;

public class MoreDialogTests
{
    private static (DialogOverlayHost Host, TextBlock Inner) ShowOverlayHost()
    {
        TextBlock inner = new();
        DialogOverlayHost host = new() { Content = inner };
        new Window { Width = 1000, Height = 800, Content = host }.Show();
        return (host, inner);
    }

    private static void Pump() => Dispatcher.UIThread.RunJobs();

    private static T Shown<T>(DialogOverlayHost host) =>
        Assert.IsType<T>(host.FindControl<ContentPresenter>("DialogPresenter")!.Content);

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private sealed record Person(string Name, int Age);

    private static readonly Person[] People = [new("Ada", 36), new("Grace", 85), new("Linus", 54)];

    [AvaloniaFact]
    public async Task ColorDialog_ReturnsThePickedColor()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<Color?> result = Dialog.Color.Open(inner, "Pick", Colors.Red, displayMode: DialogDisplayMode.Overlay);
        Pump();
        ColorDialog dialog = Shown<ColorDialog>(host);
        Assert.Equal(Colors.Red, dialog.SelectedColor);

        dialog.SelectedColor = Colors.Blue;
        Click(dialog.ButtonOk);

        Assert.Equal(Colors.Blue, await result);
    }

    [AvaloniaFact]
    public async Task ColorDialog_CancelReturnsNull()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<Color?> result = Dialog.Color.Open(inner, "Pick", displayMode: DialogDisplayMode.Overlay);
        Pump();
        Click(Shown<ColorDialog>(host).ButtonCancel);

        Assert.Null(await result);
    }

    [AvaloniaFact]
    public async Task MarkdownDialog_ShowsTheMarkdown()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<bool> result = Dialog.Markdown.Open(inner, "Notes", "# Hello\n\n*World*", displayMode: DialogDisplayMode.Overlay);
        Pump();
        MarkdownDialog dialog = Shown<MarkdownDialog>(host);
        Assert.Equal("# Hello\n\n*World*", dialog.Markdown);
        Assert.False(dialog.ButtonCancel.IsVisible);

        Click(dialog.ButtonOk);
        Assert.True(await result);
    }

    [AvaloniaFact]
    public async Task MarkdownDialog_WithCancelReturnsFalse()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<bool> result = Dialog.Markdown.Open(inner, "License", "Terms", okButtonText: "Accept", cancelButtonText: "Decline", displayMode: DialogDisplayMode.Overlay);
        Pump();
        MarkdownDialog dialog = Shown<MarkdownDialog>(host);
        Assert.True(dialog.ButtonCancel.IsVisible);

        Click(dialog.ButtonCancel);
        Assert.False(await result);
    }

    [AvaloniaFact]
    public async Task TableDialog_BuildsColumns()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task result = Dialog.Table.Open(inner, "People", "", People,
            [new TableColumn<Person>("Name", p => p.Name), new TableColumn<Person>("Age", p => p.Age) { AlignRight = true }],
            displayMode: DialogDisplayMode.Overlay);
        Pump();
        TableDialog dialog = Shown<TableDialog>(host);
        Assert.Equal(["Name", "Age"], dialog.Grid.Columns.Select(c => c.Header as string));
        Assert.False(dialog.ButtonCancel.IsVisible);

        Click(dialog.ButtonOk);
        await result;
    }

    [AvaloniaFact]
    public async Task TableDialog_PickReturnsTheSelectedItem()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<Person?> result = Dialog.Table.Pick(inner, "Pick someone", "", People, displayMode: DialogDisplayMode.Overlay);
        Pump();
        TableDialog dialog = Shown<TableDialog>(host);
        Assert.True(dialog.Grid.AutoGenerateColumns);
        Assert.False(dialog.ButtonOk.IsEnabled);

        dialog.Select(1);
        Assert.True(dialog.ButtonOk.IsEnabled);
        Click(dialog.ButtonOk);

        Assert.Equal(People[1], await result);
    }

    [AvaloniaFact]
    public async Task TableDialog_PickCancelReturnsDefault()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<Person?> result = Dialog.Table.Pick(inner, "Pick someone", "", People, displayMode: DialogDisplayMode.Overlay);
        Pump();
        Click(Shown<TableDialog>(host).ButtonCancel);

        Assert.Null(await result);
    }

    [AvaloniaFact]
    public async Task WebDialog_ClosesWhenANavigationMatches()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<Uri?> result = Dialog.Web.Open(inner, "Sign in", new Uri("https://example.com/login"),
            closeWhen: uri => uri.AbsolutePath == "/done", displayMode: DialogDisplayMode.Overlay);
        Pump();
        WebDialog dialog = Shown<WebDialog>(host);
        Assert.Equal(new Uri("https://example.com/login"), dialog.CurrentAddress);
        Assert.True(dialog.ButtonOpenInBrowser.IsVisible);

        Assert.False(dialog.OnNavigating(new Uri("https://example.com/step2")));
        Assert.True(dialog.OnNavigating(new Uri("https://example.com/done?code=1")));

        Assert.Equal(new Uri("https://example.com/done?code=1"), await result);
    }

    [AvaloniaFact]
    public async Task WebDialog_CloseReturnsTheCurrentAddress()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();

        Task<Uri?> result = Dialog.Web.OpenHtml(inner, "Help", "<p>Hi</p>", displayMode: DialogDisplayMode.Overlay);
        Pump();
        WebDialog dialog = Shown<WebDialog>(host);
        Assert.False(dialog.ButtonOpenInBrowser.IsVisible);

        Click(dialog.ButtonClose);
        Assert.Null(await result);
    }

    [AvaloniaFact]
    public void ExceptionDialog_CopiesTheFullException()
    {
        (DialogOverlayHost host, TextBlock inner) = ShowOverlayHost();
        InvalidOperationException exception = new("Outer", new ArgumentException("Inner"));

        _ = Dialog.Exception.Open(inner, "Oops", "", exception, "", displayMode: DialogDisplayMode.Overlay);
        Pump();
        ExceptionDialog dialog = Shown<ExceptionDialog>(host);

        Assert.True(dialog.ButtonCopyDetails.IsVisible);
        Assert.Contains("Inner", dialog.Details);
    }
}
