using Avalonia.Controls;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.PageSystem;
using Niddy.Avalonia.Toast;

namespace Niddy.Avalonia.Hosting;

/// <summary>
///     The root view of a <see cref="NiddyApp" />: a <see cref="PageView" /> (optionally wrapped by
///     <see cref="Layout" />) under a <see cref="ToastHost" /> and a <see cref="DialogOverlayHost" />.
///     It is the main window's content on desktop and the main view on mobile and browser.
/// </summary>
public sealed class AppShell : UserControl
{
    /// <summary>Whether the app runs in desktop or mobile mode, resolved from <see cref="Mode" />.</summary>
    public AppMode Mode { get; }

    /// <summary>The app's page view. Use it to navigate from outside a page.</summary>
    public PageView Pages { get; }

    /// <summary>The toast host covering the app.</summary>
    public ToastHost Toasts { get; }

    /// <summary>The overlay dialog host covering the app, toasts included.</summary>
    public DialogOverlayHost Dialogs { get; }

    internal AppShell(NiddyAppOptions options, AppMode mode)
    {
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
            IsMobile = (mode == AppMode.Mobile),
            Content = (options.Layout?.Invoke(Pages) ?? Pages)
        };
        Dialogs = new DialogOverlayHost
        {
            Content = Toasts
        };
        Content = Dialogs;
    }
}
