using Avalonia.Animation;
using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.PageSystem;
using Niddy.Avalonia.Theme;

namespace Niddy.Avalonia.Hosting;

/// <summary>
///     Everything <see cref="NiddyApp" /> sets up, configured in one place by
///     <see cref="Configure(NiddyAppOptions)" />. Only <see cref="StartingPage" /> is required.
/// </summary>
public sealed class NiddyAppOptions
{
    /// <summary>
    ///     The app's name, used for its folders (see <see cref="Paths" />). Defaults to
    ///     <see cref="Title" />, or the entry assembly's name if that's empty too.
    /// </summary>
    public string? AppName { get; set; }

    /// <summary>The organization the app's folders go under on Windows, e.g. "Contoso". Optional.</summary>
    public string? Organization { get; set; }

    /// <summary>
    ///     Whether the app behaves as a desktop or mobile app: the main window, how dialogs are shown and where
    ///     toasts go. Defaults to <see cref="Auto" />, which decides from the platform. Set
    ///     <see cref="Mobile" /> on desktop to preview the mobile layout in a phone-sized window.
    /// </summary>
    public AppMode Mode { get; set; } = AppMode.Auto;

    /// <summary>The first page to show, e.g. <c>HomePage.PageId</c>. Required.</summary>
    public PageId? StartingPage { get; set; }

    /// <summary>
    ///     The app-wide data context given to every page, e.g. your main data context. Pages deriving from
    ///     <see cref="Page{T1,T2}" /> require it to be a <c>TAppDataContext</c>.
    /// </summary>
    public object? AppDataContext { get; set; }

    /// <summary>
    ///     The animation between pages, e.g. <c>new PageSlide(TimeSpan.FromMilliseconds(200))</c>. Null (the
    ///     default) switches instantly. See <see cref="PageTransition" />.
    /// </summary>
    public IPageTransition? PageTransition { get; set; }

    /// <summary>
    ///     Whether the platform back request (Android back button, iOS back gesture, browser back) goes back
    ///     a page. Defaults to true. An open overlay dialog is always dismissed first.
    /// </summary>
    public bool HandleBackRequests { get; set; } = true;

    /// <summary>
    ///     Wraps the page view in your own chrome, such as a navigation bar or side menu. Receives the
    ///     <see cref="PageView" /> and returns the control to show. Dialogs and toasts still cover the result.
    /// </summary>
    public Func<PageView, Control>? Layout { get; set; }

    /// <summary>
    ///     Adds the Fluent theme, with the DataGrid and ColorPicker styles, so the app needs no theme of its
    ///     own. Defaults to true. Set to false if your App.axaml adds a theme.
    /// </summary>
    public bool UseFluentTheme { get; set; } = true;

    /// <summary>Forces a light or dark theme. Null (the default) follows the operating system.</summary>
    public AppTheme? Theme { get; set; }

    /// <summary>
    ///     How dialogs are shown. Sets <see cref="DefaultMode" />. Defaults to <see cref="Auto" />,
    ///     which means windows in desktop mode and overlays in mobile mode.
    /// </summary>
    public DialogDisplayMode DialogMode { get; set; } = DialogDisplayMode.Auto;

    /// <summary>Where toasts are shown on wide screens. Defaults to <see cref="BottomRight" />.</summary>
    public ScreenPlacement ToastPlacement { get; set; } = ScreenPlacement.BottomRight;

    /// <summary>
    ///     Where toasts are shown in mobile mode and narrow windows. Defaults to <see cref="BottomCenter" />;
    ///     null always uses <see cref="ToastPlacement" />. See <see cref="NarrowPlacement" />.
    /// </summary>
    public ScreenPlacement? ToastNarrowPlacement { get; set; } = ScreenPlacement.BottomCenter;

    /// <summary>How long toasts stay visible. Sets <see cref="DefaultDuration" />. Defaults to 3 seconds.</summary>
    public TimeSpan ToastDuration { get; set; } = TimeSpan.FromSeconds(3L);

    /// <summary>The main window's title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The main window's initial width in desktop mode. Defaults to 1024.</summary>
    public double Width { get; set; } = 1024.0;

    /// <summary>The main window's initial height in desktop mode. Defaults to 768.</summary>
    public double Height { get; set; } = 768.0;

    /// <summary>The main window's minimum width. Defaults to 0.</summary>
    public double MinWidth { get; set; }

    /// <summary>The main window's minimum height. Defaults to 0.</summary>
    public double MinHeight { get; set; }

    /// <summary>The main window's width in <see cref="Mobile" /> mode on desktop. Defaults to 390.</summary>
    public double MobilePreviewWidth { get; set; } = 390.0;

    /// <summary>The main window's height in <see cref="Mobile" /> mode on desktop. Defaults to 844.</summary>
    public double MobilePreviewHeight { get; set; } = 844.0;

    /// <summary>
    ///     Whether the main window's size, position and maximized state are saved on close and restored next time,
    ///     in <see cref="Default" />. Defaults to true. Other windows can opt in with
    ///     <see cref="KeyProperty" />.
    /// </summary>
    public bool RememberWindowState { get; set; } = true;

    /// <summary>Customizes the main window after the options above are applied, e.g. to set its icon.</summary>
    public Action<Window>? ConfigureWindow { get; set; }

    /// <summary>
    ///     Where the app logs, including unhandled exceptions. Null (the default) logs nothing; for a rolling log file
    ///     in the app's log folder use <c>NiddyLogging.CreateFactory(AppPaths.For("My App"))</c>.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; set; }

    /// <summary>
    ///     Whether <see cref="GlobalExceptionHandler" /> is installed, so exceptions on the UI thread are logged and shown
    ///     instead of crashing the app. Defaults to true.
    /// </summary>
    public bool HandleUnhandledExceptions { get; set; } = true;

    /// <summary>Whether unhandled UI-thread exceptions show <see cref="Exception" />. Defaults to true.</summary>
    public bool ShowUnhandledExceptionDialog { get; set; } = true;

    /// <summary>The URL the exception dialog's Report Issue button opens. The button is hidden when empty.</summary>
    public string ReportIssueLink { get; set; } = string.Empty;

    /// <summary>
    ///     Called for every unhandled exception before the default handling. Set <see cref="Handled" />
    ///     or <see cref="ShowDialog" /> to change it. See <see cref="ExceptionOccurred" />.
    /// </summary>
    public Action<UnhandledExceptionOccurredEventArgs>? OnUnhandledException { get; set; }
}
