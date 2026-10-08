using Niddy.Core.SystemInfo;

namespace Niddy.Core.IO;

/// <summary>
/// Where an app keeps its files, following each platform's conventions:
/// <list type="table">
/// <listheader><term>Platform</term><description>Locations</description></listheader>
/// <item><term>Windows</term><description>
/// Data and Config: <c>%APPDATA%\[Organization\]App</c>. Cache and Logs: <c>%LOCALAPPDATA%\[Organization\]App\Cache</c> and <c>\Logs</c>.
/// </description></item>
/// <item><term>macOS, Mac Catalyst</term><description>
/// Data and Config: <c>~/Library/Application Support/App</c>. Cache: <c>~/Library/Caches/App</c>. Logs: <c>~/Library/Logs/App</c>.
/// </description></item>
/// <item><term>Linux, FreeBSD</term><description>
/// Data: <c>$XDG_DATA_HOME/app</c> (<c>~/.local/share</c>). Config: <c>$XDG_CONFIG_HOME/app</c> (<c>~/.config</c>).
/// Cache: <c>$XDG_CACHE_HOME/app</c> (<c>~/.cache</c>). Logs: <c>$XDG_STATE_HOME/app/logs</c> (<c>~/.local/state</c>).
/// </description></item>
/// <item><term>Android, iOS, browser</term><description>Subfolders of the app's local data folder.</description></item>
/// </list>
/// The directories aren't created until you call <see cref="EnsureCreated" />.
/// </summary>
/// <param name="Data">Files the user would miss if they were deleted, such as databases and documents.</param>
/// <param name="Config">Settings files. The same as <paramref name="Data" /> except on Linux and FreeBSD.</param>
/// <param name="Cache">Files that can be recreated and may be deleted by the system or the user at any time.</param>
/// <param name="Logs">Log files.</param>
/// <example>
/// <code>
/// AppPaths paths = AppPaths.For("MyApp").EnsureCreated();
/// string settingsFile = Path.Combine(paths.Config, "settings.json");
/// </code>
/// </example>
public sealed record AppPaths(string Data, string Config, string Cache, string Logs)
{
    /// <summary>Gets the standard locations for an app on the current platform (<see cref="Platform.Info"/>).</summary>
    /// <param name="appName">The app's name, used as the folder name (lowercased on Linux and FreeBSD, as is customary there).</param>
    /// <param name="organization">
    /// An optional organization or publisher name to group apps under, on Windows only (e.g. <c>%APPDATA%\Contoso\MyApp</c>).
    /// </param>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
    /// </exception>
    public static AppPaths For(string appName, string? organization = null)
    {
        return For(appName, organization, Platform.Info.OS, Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    /// <summary>
    /// Gets locations under a single folder, for portable installs that keep everything next to the app:
    /// <c>Data</c> and <c>Config</c> are <paramref name="baseDirectory" /> itself, with <c>Cache</c> and <c>Logs</c> subfolders.
    /// </summary>
    /// <param name="baseDirectory">The folder, e.g. <see cref="AppContext.BaseDirectory" />.</param>
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
        foreach (string path in (string[])[this.Data, this.Config, this.Cache, this.Logs])
        {
            Directory.CreateDirectory(path);
        }
        return this;
    }

    internal static AppPaths For(string appName, string? organization, OS os, Func<string, string?> getEnvironmentVariable, string home)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appName);
        if (appName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || appName.IndexOfAny(['/', '\\']) >= 0)
        {
            throw new ArgumentException("'" + appName + "' can't be used as a folder name.", nameof(appName));
        }
        switch (os)
        {
            case OS.Windows:
            {
                string folder = string.IsNullOrWhiteSpace(organization) ? appName : Path.Combine(organization, appName);
                string roaming = Path.Combine(Env("APPDATA", Path.Combine(home, "AppData", "Roaming")), folder);
                string local = Path.Combine(Env("LOCALAPPDATA", Path.Combine(home, "AppData", "Local")), folder);
                return new AppPaths(roaming, roaming, Path.Combine(local, "Cache"), Path.Combine(local, "Logs"));
            }
            case OS.MacOS or OS.MacCatalyst:
            {
                string library = Path.Combine(home, "Library");
                string support = Path.Combine(library, "Application Support", appName);
                return new AppPaths(support, support, Path.Combine(library, "Caches", appName), Path.Combine(library, "Logs", appName));
            }
            case OS.Linux or OS.FreeBSD:
            {
                string folder = appName.ToLowerInvariant().Replace(' ', '-');
                return new AppPaths(
                    Path.Combine(Env("XDG_DATA_HOME", Path.Combine(home, ".local", "share")), folder),
                    Path.Combine(Env("XDG_CONFIG_HOME", Path.Combine(home, ".config")), folder),
                    Path.Combine(Env("XDG_CACHE_HOME", Path.Combine(home, ".cache")), folder),
                    Path.Combine(Env("XDG_STATE_HOME", Path.Combine(home, ".local", "state")), folder, "logs"));
            }
            default:
            {
                // Android, iOS and friends, the browser, WASI: everything under the app's own data folder.
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appName);
                return new AppPaths(root, root, Path.Combine(root, "Cache"), Path.Combine(root, "Logs"));
            }
        }

        string Env(string name, string fallback)
        {
            string? value = getEnvironmentVariable(name);
            return !string.IsNullOrEmpty(value) && Path.IsPathRooted(value) ? value : fallback;
        }
    }
}
