using Microsoft.Extensions.Logging;
using Niddy.IO;

namespace Niddy.Logging;

/// <summary>Sets up logging to files with <see cref="FileLoggerProvider"/>.</summary>
public static class NiddyLogging
{
    /// <summary>Adds logging to daily, size-capped files in <paramref name="directory"/>.</summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="directory">The folder to write to, e.g. <see cref="AppPaths.Logs"/>.</param>
    /// <param name="configure">Changes the other settings, e.g. the minimum level or how many files to keep.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ILoggingBuilder AddFile(this ILoggingBuilder builder, string directory, Action<FileLoggerOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var options = new FileLoggerOptions { Directory = directory };
        configure?.Invoke(options);
        return builder.AddProvider(new FileLoggerProvider(options));
    }

    /// <summary>
    /// Creates a logger factory that writes to files in <paramref name="directory"/>. Dispose it on exit so queued lines
    /// are written.
    /// </summary>
    /// <param name="directory">The folder to write to.</param>
    /// <param name="minimumLevel">The least severe level logged.</param>
    /// <param name="configure">Adds other providers or filters, e.g. <c>b =&gt; b.AddConsole()</c>.</param>
    public static ILoggerFactory CreateFactory(string directory, LogLevel minimumLevel = LogLevel.Information, Action<ILoggingBuilder>? configure = null) =>
        LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            builder.AddFile(directory, o => o.MinimumLevel = minimumLevel);
            configure?.Invoke(builder);
        });

    /// <summary>Creates a logger factory that writes to files in the app's <see cref="AppPaths.Logs"/> folder.</summary>
    /// <param name="paths">The app's folders.</param>
    /// <param name="minimumLevel">The least severe level logged.</param>
    /// <param name="configure">Adds other providers or filters.</param>
    public static ILoggerFactory CreateFactory(AppPaths paths, LogLevel minimumLevel = LogLevel.Information, Action<ILoggingBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return CreateFactory(paths.Logs, minimumLevel, configure);
    }
}
