using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Niddy.Core.IO;
using Niddy.Core.Logging.Batching;
using Niddy.Core.Logging.Files;

namespace Niddy.Core.Logging;

/// <summary>
/// Sets up <c>Microsoft.Extensions.Logging</c>: file logging with <see cref="FileLoggerProvider"/>, any other providers,
/// and ready-made logger factories.
/// </summary>
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
        FileLoggerOptions options = new() { Directory = directory };
        configure?.Invoke(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory, nameof(directory));
        return builder.AddOwnedProvider(_ => new FileLoggerProvider(options));
    }

    /// <summary>
    /// Adds a provider that the logger factory (or DI container) creates and disposes. Use it instead of
    /// <c>AddProvider(new MyProvider())</c>: a provider instance passed to <c>AddProvider</c> is never disposed, so a
    /// queueing provider such as a <see cref="BatchingLoggerProvider{TOptions}"/> loses what's still queued at exit.
    /// </summary>
    /// <param name="builder">The logging builder.</param>
    /// <param name="factory">Creates the provider, once, from the service provider.</param>
    /// <returns>The builder, for chaining.</returns>
    public static ILoggingBuilder AddOwnedProvider(this ILoggingBuilder builder, Func<IServiceProvider, ILoggerProvider> factory)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(factory);
        builder.Services.AddSingleton(factory);
        return builder;
    }

    /// <summary>
    /// Creates a logger factory with only the providers <paramref name="configure"/> adds (no files). Dispose it on
    /// exit so queued entries are written.
    /// </summary>
    /// <param name="configure">Adds providers or filters, e.g. <c>b =&gt; b.AddConsole().AddOwnedProvider(_ =&gt; new DbLoggerProvider(...))</c>.</param>
    /// <param name="minimumLevel">The least severe level logged.</param>
    public static ILoggerFactory CreateFactory(Action<ILoggingBuilder> configure, LogLevel minimumLevel = LogLevel.Information)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(minimumLevel);
            configure(builder);
        });
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
