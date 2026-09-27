using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;
using Niddy.Avalonia.Tests;

[assembly: AvaloniaTestApplication(typeof(TestApp))]

namespace Niddy.Avalonia.Tests;

public sealed class TestApp : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
