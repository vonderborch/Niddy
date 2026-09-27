using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using System.Reflection;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.State;
using Niddy.Avalonia.Theme;
using Niddy.IO;
using AvaloniaApplication = Avalonia.Application;

namespace Niddy.Avalonia.Hosting;

/// <summary>
///     An <see cref="AvaloniaApplication"/> that sets up the page system, dialogs, toasts, theme and main
///     window or view from one <see cref="Configure"/> method, on desktop, mobile and browser alike.
///     <code>
///         public class App : NiddyApp
///         {
///             protected override void Configure(NiddyAppOptions options)
///             {
///                 options.StartingPage = HomePage.PageId;
///                 options.Title = "My App";
///             }
///         }
///     </code>
///     Start it as usual from each platform's entry point, e.g.
///     <c>AppBuilder.Configure&lt;App&gt;().UsePlatformDetect().StartWithClassicDesktopLifetime(args)</c>.
///     No App.axaml is needed; if you have one, load it in <see cref="Initialize"/> before calling the base method.
/// </summary>
public abstract class NiddyApp : AvaloniaApplication
{
    private NiddyAppOptions? _options;
    private AppShell? _shell;
    private AppPaths? _paths;

    /// <summary>The running <see cref="NiddyApp"/>, or null if the app isn't one.</summary>
    public static new NiddyApp? Current => AvaloniaApplication.Current as NiddyApp;

    /// <summary>The options from <see cref="Configure"/>.</summary>
    public NiddyAppOptions Options => _options ?? throw NotInitialized();

    /// <summary>The root view, created once the framework has initialized.</summary>
    public AppShell Shell => _shell ?? throw NotInitialized();

    internal AppShell? ShellOrNull => _shell;

    /// <summary>
    ///     The app's data, config, cache and log folders for <see cref="NiddyAppOptions.AppName"/> on this platform.
    ///     Not created until you call <see cref="AppPaths.EnsureCreated"/>.
    /// </summary>
    public AppPaths Paths => _paths ??= AppPaths.For(ResolveAppName(Options), Options.Organization);

    /// <summary>
    ///     Whether the app runs in desktop or mobile mode: <see cref="NiddyAppOptions.Mode"/>, or decided from the
    ///     platform if that is <see cref="AppMode.Auto"/>. Resolved once the framework has initialized.
    /// </summary>
    public AppMode Mode => _shell?.Mode ?? ResolveMode(Options.Mode, ApplicationLifetime);

    /// <summary>
    ///     The running app's <see cref="Mode"/>, or the mode decided from the platform if the app isn't a
    ///     <see cref="NiddyApp"/> (e.g. a page view in an app of your own).
    /// </summary>
    public static AppMode CurrentMode =>
        Current is { _options: not null } app ? app.Mode : ResolveMode(AppMode.Auto, AvaloniaApplication.Current?.ApplicationLifetime);

    /// <summary>Configures the app. Called once, before the first window or view is created.</summary>
    protected abstract void Configure(NiddyAppOptions options);

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();
        var options = new NiddyAppOptions();
        Configure(options);
        if (options.StartingPage is null)
            throw new InvalidOperationException($"{nameof(NiddyAppOptions)}.{nameof(NiddyAppOptions.StartingPage)} must be set in {nameof(Configure)}.");
        _options = options;

        if (options.UseFluentTheme)
            Styles.AddRange(CreateFluentStyles());

        if (options.Theme is { } theme)
            RequestedThemeVariant = theme == AppTheme.Dark ? ThemeVariant.Dark : ThemeVariant.Light;

        Toast.Toast.DefaultDuration = options.ToastDuration;

        if (options.RememberWindowState && UiStateStore.Default is null)
            UiStateStore.Default = UiStateStore.For(Paths);

        if (options.HandleUnhandledExceptions)
        {
            GlobalExceptionHandler.Install(new GlobalExceptionHandlerOptions
            {
                LoggerFactory = options.LoggerFactory,
                ShowDialog = options.ShowUnhandledExceptionDialog,
                ReportIssueLink = options.ReportIssueLink,
            });
            if (options.OnUnhandledException is { } callback)
                GlobalExceptionHandler.ExceptionOccurred += (_, e) => callback(e);
        }
    }

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        // Initialize is skipped if a subclass overrides it without calling the base method.
        if (_options is null)
            throw new InvalidOperationException($"Call base.{nameof(Initialize)}() when overriding {nameof(NiddyApp)}.{nameof(Initialize)}.");

        var mode = ResolveMode(_options.Mode, ApplicationLifetime);

        // The shell always has an overlay host, so Auto would always pick overlays; desktop mode prefers windows.
        Dialog.DefaultMode = _options.DialogMode == DialogDisplayMode.Auto
            ? mode == AppMode.Desktop ? DialogDisplayMode.Window : DialogDisplayMode.Overlay
            : _options.DialogMode;

        _shell = new AppShell(_options, mode);
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = CreateMainWindow(_shell);
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = _shell;
                break;
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    ///     Creates the desktop main window around <paramref name="shell"/>. Override to use your own window
    ///     class; <see cref="NiddyAppOptions.ConfigureWindow"/> covers smaller changes.
    /// </summary>
    protected virtual Window CreateMainWindow(AppShell shell)
    {
        var mobile = shell.Mode == AppMode.Mobile;
        var window = new Window
        {
            Title = Options.Title,
            Width = mobile ? Options.MobilePreviewWidth : Options.Width,
            Height = mobile ? Options.MobilePreviewHeight : Options.Height,
            MinWidth = Options.MinWidth,
            MinHeight = Options.MinHeight,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = shell,
        };
        // In the mobile preview the phone-sized window shouldn't take the desktop window's saved size.
        if (Options.RememberWindowState && !mobile)
            WindowMemory.SetKey(window, "Main");
        Options.ConfigureWindow?.Invoke(window);
        return window;
    }

    internal static string ResolveAppName(NiddyAppOptions options) =>
        !string.IsNullOrWhiteSpace(options.AppName) ? options.AppName
        : !string.IsNullOrWhiteSpace(options.Title) ? options.Title
        : Assembly.GetEntryAssembly()?.GetName().Name ?? "NiddyApp";

    /// <summary>Resolves <see cref="AppMode.Auto"/> from the application lifetime, then the operating system.</summary>
    internal static AppMode ResolveMode(AppMode requested, IApplicationLifetime? lifetime) => requested switch
    {
        not AppMode.Auto => requested,
        _ when lifetime is IClassicDesktopStyleApplicationLifetime => AppMode.Desktop,
        _ when lifetime is ISingleViewApplicationLifetime => AppMode.Mobile,
        _ when OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsBrowser() => AppMode.Mobile,
        _ => AppMode.Desktop,
    };

    internal static IStyle[] CreateFluentStyles() =>
    [
        new FluentTheme(),
        Include("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml"),
        Include("avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml"),
    ];

    private static StyleInclude Include(string uri) => new((Uri?)null) { Source = new Uri(uri) };

    private static InvalidOperationException NotInitialized() =>
        new("The application hasn't initialized yet.");
}
