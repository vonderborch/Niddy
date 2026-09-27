namespace Niddy.Avalonia.Hosting;

/// <summary>Whether a <see cref="NiddyApp" /> behaves as a desktop or a mobile app.</summary>
public enum AppMode
{
    /// <summary>
    ///     Decide from the platform: <see cref="Mobile" /> on Android, iOS and the browser (single-view lifetimes),
    ///     otherwise <see cref="Desktop" />.
    /// </summary>
    Auto,
    /// <summary>
    ///     A resizable main window, dialogs in their own windows and toasts in the bottom right (bottom center when
    ///     narrow).
    /// </summary>
    Desktop,
    /// <summary>
    ///     A full-screen view, overlay dialogs and toasts at the bottom center. On desktop, the main window is
    ///     phone-sized (see <see cref="MobilePreviewWidth" />), to preview the mobile layout.
    /// </summary>
    Mobile
}
