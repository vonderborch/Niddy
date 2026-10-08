using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Niddy.Avalonia.Hosting;

/// <summary>
///     Where Niddy.Avalonia reports problems it recovers from: the app's logger factory
///     (<see cref="NiddyAppOptions.LoggerFactory"/>, or <see cref="GlobalExceptionHandlerOptions.LoggerFactory"/> outside
///     a <see cref="NiddyApp"/>), or <see cref="Trace"/> if the app didn't give one.
/// </summary>
internal static class NiddyLog
{
    public static void Warning(Type source, Exception? exception, string message)
    {
        try
        {
            ILoggerFactory? factory = NiddyApp.Current?.OptionsOrNull?.LoggerFactory ?? GlobalExceptionHandler.Options?.LoggerFactory;
            if (factory is null)
            {
                Trace.TraceWarning(exception is null ? message : $"{message}: {exception.Message}");
                return;
            }

            factory.CreateLogger(source.FullName ?? source.Name).LogWarning(exception, "{Message}", message);
        }
        catch
        {
            // Reporting a problem must never cause one, e.g. after the factory was disposed at exit.
        }
    }
}
