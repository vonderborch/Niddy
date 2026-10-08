# FileLoggerProvider

`Niddy.Core.Logging.Files` · Niddy.Core · [source](../../../../src/Niddy.Core/Logging/Files/FileLoggerProvider.cs)

An `ILoggerProvider` that writes to daily, size-capped files and deletes old ones. Lines look like:

```
2026-09-26 14:03:12.345 +01:00 [INF] MyApp.Sync: Synced 12 items
```

followed by the exception, if any. It's a [`BatchingLoggerProvider<FileLoggerOptions>`](../Batching/BatchingLoggerProvider.md), so logging never blocks on disk: lines are queued and written by a background task. Disposing the provider writes everything still queued, and disposing the `ILoggerFactory` does that for you. Disk writes are synchronous on the writer, so with `WriterThread = Dedicated` the file logger doesn't depend on the thread pool at all. If writing fails (e.g. a full disk), that line is dropped and the file is reopened for the next one. The app doesn't crash.

It's usually created through [`NiddyLogging`](../NiddyLogging.md) and configured with [`FileLoggerOptions`](FileLoggerOptions.md). Its configuration alias is `File`, so `Logging:File:LogLevel` settings target it.

## API

| Member | Description |
|---|---|
| `FileLoggerProvider(FileLoggerOptions options)` | Creates the provider; the directory is created if needed. Throws `ArgumentException` for a blank `Directory`. |
| `string Directory` | The full path of the log folder. |
| `string? CurrentFile` | The file being written to, or null before the first line. |
| `FileLoggerOptions Options` | The settings (inherited). |
| `ILogger CreateLogger(string categoryName)` | Inherited. |
| `void SetScopeProvider(IExternalScopeProvider scopeProvider)` | Inherited; scopes are written when `IncludeScopes` is on. |
| `void Dispose()` | Writes the queue (up to `ShutdownTimeout`) and closes the file. |

## Example

```csharp
using Microsoft.Extensions.Logging;
using Niddy.Core.Logging;

using ILoggerFactory loggerFactory = LoggerFactory.Create(b => b
    .AddConsole()
    .AddFile(logDir, o => o.IncludeScopes = true));
```

Register it with `AddFile` rather than `AddProvider(new FileLoggerProvider(...))`. The factory never disposes a provider instance passed to `AddProvider`, so lines still queued at exit are lost.
