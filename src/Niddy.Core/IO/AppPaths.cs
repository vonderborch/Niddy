namespace Niddy.IO;

/// <summary>
/// Where an app keeps its files, following each platform's conventions:
/// <list type="table">
/// <listheader><term>Platform</term><description>Locations</description></listheader>
/// <item><term>Windows</term><description>
/// Data and Config: <c>%APPDATA%\[Organization\]App</c>. Cache and Logs: <c>%LOCALAPPDATA%\[Organization\]App\Cache</c> and <c>\Logs</c>.
/// </description></item>
/// <item><term>macOS</term><description>
/// Data and Config: <c>~/Library/Application Support/App</c>. Cache: <c>~/Library/Caches/App</c>. Logs: <c>~/Library/Logs/App</c>.
/// </description></item>
/// <item><term>Linux</term><description>
/// Data: <c>$XDG_DATA_HOME/app</c> (<c>~/.local/share</c>). Config: <c>$XDG_CONFIG_HOME/app</c> (<c>~/.config</c>).
/// Cache: <c>$XDG_CACHE_HOME/app</c> (<c>~/.cache</c>). Logs: <c>$XDG_STATE_HOME/app/logs</c> (<c>~/.local/state</c>).
/// </description></item>
/// <item><term>Android, iOS, browser</term><description>Subfolders of the app's local data folder.</description></item>
/// </list>
/// The directories aren't created until you call <see cref="EnsureCreated" />.
/// </summary>
/// <param name="Data">Files the user would miss if they were deleted, such as databases and documents.</param>
/// <param name="Config">Settings files. The same as <paramref name="Data" /> except on Linux.</param>
/// <param name="Cache">Files that can be recreated and may be deleted by the system or the user at any time.</param>
/// <param name="Logs">Log files.</param>
/// <example>
/// <code>
/// var paths = AppPaths.For("MyApp").EnsureCreated();
/// var settingsFile = Path.Combine(paths.Config, "settings.json");
/// </code>
/// </example>
public sealed record AppPaths(string Data, string Config, string Cache, string Logs)
{
    internal enum Platform
    {
        Windows,
        MacOS,
        Linux,
        Other
    }

    /// <summary>Gets the standard locations for an app on the current platform.</summary>
    /// <param name="appName">The app's name, used as the folder name (lowercased on Linux, as is customary there).</param>
    /// <param name="organization">
    /// An optional organization or publisher name to group apps under, on Windows only (e.g. <c>%APPDATA%\Contoso\MyApp</c>).
    /// </param>
    public static AppPaths For(string appName, string? organization = null)
    {
        return For(appName, organization, CurrentPlatform(), Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    /// <summary>
    /// Gets locations under a single folder, for portable installs that keep everything next to the app:
    /// <c>Data</c> and <c>Config</c> are <paramref name="baseDirectory" /> itself, with <c>Cache</c> and <c>Logs</c> subfolders.
    /// </summary>
    /// <param name="baseDirectory">The folder, e.g. <see cref="BaseDirectory" />.</param>
    public static AppPaths Portable(string baseDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseDirectory);
        string fullPath = Path.GetFullPath(baseDirectory);
        return new AppPaths(fullPath, fullPath, Path.Combine(fullPath, "Cache"), Path.Combine(fullPath, "Logs"));
    }

    /// <summary>Creates any of the directories that don't exist yet.</summary>
    /// <returns>This instance, for chaining.</returns>
    public AppPaths EnsureCreated()
    {
        string[] array = new string[4] { Data, Config, Cache, Logs };
        foreach (string path in array)
        {
            Directory.CreateDirectory(path);
        }
        return this;
    }

    internal static AppPaths For(string appName, string? organization, Platform platform, Func<string, string?> getEnvironmentVariable, string home)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        if (appName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || appName.IndexOfAny(new char[2] { '/', '\\' }) >= 0)
        {
            throw new ArgumentException("'" + appName + "' can't be used as a folder name.", "appName");
        }
        switch (platform)
        {
        case Platform.Windows:
        {
            string path3 = (string.IsNullOrWhiteSpace(organization) ? appName : Path.Combine(organization, appName));
            string text3 = Path.Combine(Env("APPDATA", Path.Combine(home, "AppData", "Roaming")), path3);
            string path4 = Path.Combine(Env("LOCALAPPDATA", Path.Combine(home, "AppData", "Local")), path3);
            return new AppPaths(text3, text3, Path.Combine(path4, "Cache"), Path.Combine(path4, "Logs"));
        }
        case Platform.MacOS:
        {
            string path2 = Path.Combine(home, "Library");
            string text2 = Path.Combine(path2, "Application Support", appName);
            return new AppPaths(text2, text2, Path.Combine(path2, "Caches", appName), Path.Combine(path2, "Logs", appName));
        }
        case Platform.Linux:
        {
            string path = appName.ToLowerInvariant().Replace(' ', '-');
            return new AppPaths(Path.Combine(Env("XDG_DATA_HOME", Path.Combine(home, ".local", "share")), path), Path.Combine(Env("XDG_CONFIG_HOME", Path.Combine(home, ".config")), path), Path.Combine(Env("XDG_CACHE_HOME", Path.Combine(home, ".cache")), path), Path.Combine(Env("XDG_STATE_HOME", Path.Combine(home, ".local", "state")), path, "logs"));
        }
        default:
        {
            string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
            return new AppPaths(text, text, Path.Combine(text, "Cache"), Path.Combine(text, "Logs"));
        }
        }
        string Env(string name, string fallback)
        {
            string text4 = getEnvironmentVariable(name);
            return (text4 != null && text4.Length > 0 && Path.IsPathRooted(text4)) ? text4 : fallback;
        }
    }

    private static Platform CurrentPlatform()
    {
        return (!OperatingSystem.IsWindows()) ? ((OperatingSystem.IsMacOS() || OperatingSystem.IsMacCatalyst()) ? Platform.MacOS : ((OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid()) ? Platform.Linux : Platform.Other)) : Platform.Windows;
    }
}
