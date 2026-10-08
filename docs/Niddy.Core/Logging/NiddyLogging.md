# NiddyLogging

`Niddy.Core.Logging` · Niddy.Core · [source](../../../src/Niddy.Core/Logging/NiddyLogging.cs)

Sets up `Microsoft.Extensions.Logging`. It covers three things:

- Daily, size-capped log files with [`FileLoggerProvider`](Files/FileLoggerProvider.md).
- Registering any other provider so that it's disposed properly.
- Ready-made logger factories.

## API

| Member | Description |
|---|---|
| `ILoggingBuilder AddFile(this ILoggingBuilder builder, string directory, Action<FileLoggerOptions>? configure = null)` | Adds the file provider to a logging builder (DI or `LoggerFactory.Create`). |
| `ILoggingBuilder AddOwnedProvider(this ILoggingBuilder builder, Func<IServiceProvider, ILoggerProvider> factory)` | Adds a provider that the factory or DI container creates once and disposes with itself. |
| `ILoggerFactory CreateFactory(Action<ILoggingBuilder> configure, LogLevel minimumLevel = LogLevel.Information)` | A factory with only the providers `configure` adds, and no files. |
| `ILoggerFactory CreateFactory(string directory, LogLevel minimumLevel = LogLevel.Information, Action<ILoggingBuilder>? configure = null)` | A factory that writes files to `directory`; `configure` adds other providers or filters. |
| `ILoggerFactory CreateFactory(AppPaths paths, LogLevel minimumLevel = LogLevel.Information, Action<ILoggingBuilder>? configure = null)` | The same, writing to `paths.Logs`. |

`minimumLevel` is applied to the whole factory, so it filters every provider. For the overloads that write files, it's also the file provider's `MinimumLevel`. Dispose the factory on exit so queued entries are written.

## Other providers

Niddy never adds a provider on its own. Logs go only where your code sends them. Packages that offer a provider expose an `AddXxx` extension that you call yourself, and [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md) logs only to the factory you pass it.

`ILoggingBuilder` is the extension point, so any provider works alongside the files. That includes Microsoft's console and debug providers, Serilog, OpenTelemetry, or your own.

To write your own provider:

1. Derive from [`BatchingLoggerProvider<TOptions>`](Batching/BatchingLoggerProvider.md). It handles queueing, batching, shutdown and errors; you write `WriteBatchAsync`. The page has a full database example.
2. Ship an `AddXxx(this ILoggingBuilder)` extension that calls `AddOwnedProvider`.

Use `AddOwnedProvider` instead of `AddProvider(new MyProvider())`. The factory never disposes a provider instance passed to `AddProvider`, so a queueing provider would lose what's still queued at exit.

## Examples

```csharp
using Microsoft.Extensions.Logging;
using Niddy.Core.IO;
using Niddy.Core.Logging;

using ILoggerFactory loggerFactory = NiddyLogging.CreateFactory(AppPaths.For("MyApp"), LogLevel.Debug);
ILogger<SyncService> logger = loggerFactory.CreateLogger<SyncService>();
logger.LogInformation("Synced {Count} items", 12);

// Files plus a custom provider
using ILoggerFactory withDb = NiddyLogging.CreateFactory(paths, LogLevel.Information,
    b => b.AddOwnedProvider(_ => new SqliteLoggerProvider(new() { ConnectionString = "Data Source=logs.db" })));

// No files at all, e.g. in the browser where there's no real disk
using ILoggerFactory noFiles = NiddyLogging.CreateFactory(b => b.AddConsole(), LogLevel.Debug);

// With dependency injection
services.AddLogging(b => b.AddFile(paths.Logs, o =>
{
    o.RetainedFileCount = 14;
    o.MinimumLevel = LogLevel.Warning;
}));
```

`SqliteLoggerProvider` is the example from [BatchingLoggerProvider](Batching/BatchingLoggerProvider.md#example-a-database-provider). `AddConsole` comes from the `Microsoft.Extensions.Logging.Console` package.

In a [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md), pass the factory as `options.LoggerFactory` so the [global exception handler](../../Niddy.Avalonia/Hosting/GlobalExceptionHandler.md) logs to it.
