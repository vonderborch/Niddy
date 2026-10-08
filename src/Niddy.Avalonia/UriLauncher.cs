using Avalonia.Controls;
using Niddy.Avalonia.Hosting;

namespace Niddy.Avalonia;

/// <summary>
///     Opens URIs with the platform's default handler through Avalonia's <see cref="TopLevel.Launcher"/>,
///     which works on desktop, mobile, and browser (unlike <c>Process.Start</c>, which is desktop-only).
/// </summary>
public static class UriLauncher
{
    /// <summary>
    ///     Opens <paramref name="uri"/> with the platform's default handler (usually the browser).
    ///     Returns false if the URI is invalid, <paramref name="source"/> isn't attached to a visual tree,
    ///     or the platform couldn't open it.
    /// </summary>
    public static async Task<bool> TryLaunchAsync(Control source, string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return false;
        }

        TopLevel? topLevel = TopLevel.GetTopLevel(source);
        if (topLevel is null)
        {
            return false;
        }

        try
        {
            return await topLevel.Launcher.LaunchUriAsync(parsed);
        }
        catch (Exception ex)
        {
            NiddyLog.Warning(typeof(UriLauncher), ex, $"Couldn't open '{uri}'");
            return false;
        }
    }
}
