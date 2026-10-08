using Avalonia.Controls;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.PageSystem;
using Niddy.Avalonia.Toasts;

namespace Niddy.Avalonia.Hosting;

/// <summary>
///     The root view of a <see cref="NiddyApp" />: a <see cref="PageView" /> (optionally wrapped by
///     <see cref="Layout" />) under a <see cref="ToastHost" /> and a <see cref="DialogOverlayHost" />.
///     It is the main window's content on desktop and the main view on mobile and browser.
///     <para>
///         <see cref="NiddyApp" /> creates it. You can also create one yourself, e.g. to render pages in a test or to
///         host them in an app that isn't a <see cref="NiddyApp" />. The shell only builds the views: the main window,
///         <see cref="Dialog.DefaultMode" /> and the other app-wide settings are still up to <see cref="NiddyApp" />.
///     </para>
/// </summary>
/// <example>
///     <code>
///         AppShell shell = new(new NiddyAppOptions { StartingPage = HomePage.PageId, AppDataContext = new MainDataContext() });
///         new Window { Content = shell }.Show();
///         shell.Pages.NavigateTo&lt;SettingsPage&gt;();
///     </code>
/// </example>
public sealed class AppShell : UserControl
{
    /// <summary>
    ///     Whether the shell is laid out for desktop or mobile, resolved from <see cref="NiddyAppOptions.Mode" />.
    /// </summary>
    public AppMode Mode { get; }

    /// <summary>The app's page view. Use it to navigate from outside a page.</summary>
    public PageView Pages { get; }

    /// <summary>The toast host covering the app.</summary>
    public ToastHost Toasts { get; }

    /// <summary>The overlay dialog host covering the app, toasts included.</summary>
    public DialogOverlayHost Dialogs { get; }

    /// <summary>Creates the shell's page view, toast host and dialog host from <paramref name="options" />.</summary>
    /// <param name="options">
    ///     The options to build from: the starting page, app data context, layout, page transition and toast placement.
    ///     Defaults to new options, which show no page until you navigate.
    /// </param>
    /// <param name="mode">
    ///     Desktop or mobile layout. <see cref="AppMode.Auto" /> (the default) uses <see cref="NiddyAppOptions.Mode" />,
    ///     and if that is <see cref="AppMode.Auto" /> too, decides from the application lifetime, then the platform.
    /// </param>
    public AppShell(NiddyAppOptions? options = null, AppMode mode = AppMode.Auto)
    {
        options ??= new NiddyAppOptions();
        if (mode == AppMode.Auto)
        {
            mode = NiddyApp.ResolveMode(options.Mode, global::Avalonia.Application.Current?.ApplicationLifetime);
        }

        Mode = mode;
        Pages = new PageView
        {
            StartingPage = options.StartingPage?.PageType,
            AppDataContext = options.AppDataContext,
            HandleBackRequests = options.HandleBackRequests,
            PageTransition = options.PageTransition
        };
        Toasts = new ToastHost
        {
            Placement = options.ToastPlacement,
            NarrowPlacement = options.ToastNarrowPlacement,
            IsMobile = mode == AppMode.Mobile,
            Content = options.Layout?.Invoke(Pages) ?? Pages
        };
        Dialogs = new DialogOverlayHost
        {
            Content = Toasts
        };
        Content = Dialogs;
    }
}
