# NiddyLogging

`Niddy.Logging` · Niddy.Core · [source](../../../src/Niddy.Core/Logging/NiddyLogging.cs)

Sets up `Microsoft.Extensions.Logging` to write daily, size-capped log files with [`FileLoggerProvider`](FileLoggerProvider.md).

## API

| Member | Description |
|---|---|
| `ILoggingBuilder AddFile(this ILoggingBuilder builder, string directory, Action<FileLoggerOptions>? configure = null)` | Adds the file provider to a logging builder (DI or `LoggerFactory.Create`). |
| `ILoggerFactory CreateFactory(string directory, LogLevel minimumLevel = LogLevel.Information, Action<ILoggingBuilder>? configure = null)` | A ready-made factory; `configure` adds other providers or filters. |
| `ILoggerFactory CreateFactory(AppPaths paths, LogLevel minimumLevel = LogLevel.Information, Action<ILoggingBuilder>? configure = null)` | The same, writing to `paths.Logs`. |

Dispose the factory on exit so queued lines reach the disk.

## Examples

```csharp
using Microsoft.Extensions.Logging;
using Niddy.IO;
using Niddy.Logging;

using var loggerFactory = NiddyLogging.CreateFactory(AppPaths.For("MyApp"), LogLevel.Debug);
var logger = loggerFactory.CreateLogger<SyncService>();
logger.LogInformation("Synced {Count} items", 12);

// With dependency injection
services.AddLogging(b => b.AddFile(paths.Logs, o =>
{
    o.RetainedFileCount = 14;
    o.MinimumLevel = LogLevel.Warning;
}));
```

In a [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md), pass the factory as `options.LoggerFactory` so the [global exception handler](../../Niddy.Avalonia/Hosting/GlobalExceptionHandler.md) logs to it.
