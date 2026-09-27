using System.Diagnostics;

namespace Niddy.Helpers;

/// <summary>
/// Helper methods for working with URLs.
/// </summary>
public static class UrlHelpers
{
    /// <summary>
    /// Opens a URL in the default browser. Writes <paramref name="errorMessage" /> to
    /// <see cref="Error" /> if the process cannot be started.
    /// </summary>
    public static void OpenUrl(string url, string errorMessage = "Failed to open URL.")
    {
        try
        {
            Process.Start(new ProcessStartInfo(url)
            {
                UseShellExecute = true,
                Verb = "open"
            });
        }
        catch
        {
            Console.Error.WriteLine(errorMessage);
        }
    }
}
