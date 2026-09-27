# FileLoggerProvider

`Niddy.Logging` · Niddy.Core · [source](../../../src/Niddy.Core/Logging/FileLoggerProvider.cs)

An `ILoggerProvider` that writes to daily, size-capped files and deletes old ones. Lines look like:

```
2026-09-26 14:03:12.345 +01:00 [INF] MyApp.Sync: Synced 12 items
```

followed by the exception, if any. Logging never blocks on disk: lines are queued and written by a background task. Disposing the provider (which disposing the `ILoggerFactory` does) writes everything still queued. If writing fails (e.g. a full disk), lines are dropped instead of crashing the app.

Usually created through [`NiddyLogging`](NiddyLogging.md); configured with [`FileLoggerOptions`](FileLoggerOptions.md).

## API

| Member | Description |
|---|---|
| `FileLoggerProvider(FileLoggerOptions options)` | Creates the provider; the directory is created if needed. |
| `ILogger CreateLogger(string categoryName)` | |
| `void SetScopeProvider(IExternalScopeProvider scopeProvider)` | Used when `IncludeScopes` is on. |
| `void Dispose()` | Flushes the queue and closes the file. |

## Example

```csharp
using Microsoft.Extensions.Logging;
using Niddy.Logging;

using var loggerFactory = LoggerFactory.Create(b => b
    .AddConsole()
    .AddProvider(new FileLoggerProvider(new FileLoggerOptions { Directory = logDir, IncludeScopes = true })));
```
