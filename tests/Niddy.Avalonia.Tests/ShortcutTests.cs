using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Niddy.Avalonia.PageSystem;

namespace Niddy.Avalonia.Tests;

public class ShortcutTests
{
    [Theory]
    [InlineData("Ctrl+S", Key.S, KeyModifiers.Control)]
    [InlineData("Ctrl+Shift+S", Key.S, KeyModifiers.Control | KeyModifiers.Shift)]
    [InlineData("F5", Key.F5, KeyModifiers.None)]
    [InlineData("Alt+2", Key.D2, KeyModifiers.Alt)]
    [InlineData("Ctrl++", Key.OemPlus, KeyModifiers.Control)]
    public void ParsesGestures(string text, Key key, KeyModifiers modifiers)
    {
        var gesture = PageShortcut.Parse(text);
        Assert.Equal(key, gesture.Key);
        Assert.Equal(modifiers, gesture.KeyModifiers);
    }

    [Fact]
    public void PrimaryIsThePlatformModifier()
    {
        var gesture = PageShortcut.Parse("Primary+1");
        Assert.Equal(Key.D1, gesture.Key);
        Assert.Equal(PageShortcut.PrimaryModifier, gesture.KeyModifiers);
        Assert.Equal(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control, PageShortcut.PrimaryModifier);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Ctrl+")]
    [InlineData("Ctrl+NotAKey")]
    [InlineData("Hyper+S")]
    public void RejectsInvalidGestures(string text)
    {
        Assert.Throws<ArgumentException>(() => PageShortcut.Parse(text));
        Assert.False(PageShortcut.TryParse(text, out _));
    }

    [AvaloniaFact]
    public void GeneratedRegistrationsHaveTheirShortcut()
    {
        var registration = PageRegistry.Find(DetailsPage.PageId)!;
        Assert.Equal(PageShortcut.Parse("Primary+2"), registration.Shortcut);
    }

    [AvaloniaFact]
    public void ShortcutsNavigateToAvailablePages()
    {
        var view = new PageView { StartingPage = typeof(HomePage), AppDataContext = new TestAppDataContext() };
        new Window { Content = view }.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(view.HandleShortcut(Key.D2, PageShortcut.PrimaryModifier));
        Assert.Equal(DetailsPage.PageId, view.PageControlDataContext.CurrentPageId);

        Assert.False(view.HandleShortcut(Key.D9, PageShortcut.PrimaryModifier));

        view.HandleShortcuts = false;
        view.NavigateTo<HomePage>();
        Assert.False(view.HandleShortcut(Key.D2, PageShortcut.PrimaryModifier));
        Assert.Equal(HomePage.PageId, view.PageControlDataContext.CurrentPageId);
    }

    [AvaloniaFact]
    public void KeyPressesInTheWindowTriggerShortcuts()
    {
        var view = new PageView { StartingPage = typeof(HomePage), AppDataContext = new TestAppDataContext() };
        var window = new Window { Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        window.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.D2, KeyModifiers = PageShortcut.PrimaryModifier });

        Assert.Equal(DetailsPage.PageId, view.PageControlDataContext.CurrentPageId);
    }

    [Fact]
    public void RegisteringWithAnInvalidShortcutThrows() =>
        Assert.Throws<ArgumentException>(() => PageRegistry.Register<PushedPage>(() => new PushedPage("x"), shortcut: "Ctrl+Nope"));
}
