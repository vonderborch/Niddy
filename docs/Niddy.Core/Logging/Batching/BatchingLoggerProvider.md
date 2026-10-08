# BatchingLoggerProvider&lt;TOptions&gt;

`Niddy.Core.Logging.Batching` · Niddy.Core · [source](../../../../src/Niddy.Core/Logging/Batching/BatchingLoggerProvider.cs)

A base class for `ILoggerProvider`s that write somewhere slow: files, a database, a log server. Logging calls never block. Each entry is captured as a [`LogEntry`](#logentry) and queued. A background task then hands the queue to your `WriteBatchAsync` in batches. Subclass it, then ship an `AddXxx(this ILoggingBuilder)` extension that registers the provider with [`NiddyLogging.AddOwnedProvider`](../NiddyLogging.md).

[`FileLoggerProvider`](../Files/FileLoggerProvider.md) is built on it.

How it behaves:

- **One writer at a time.** `WriteBatchAsync`, `FlushAsync`, `CloseAsync` and `OnWriteFailed` all run on the background writer, one after another and never at the same time. Your writer state (a stream, a connection) needs no locking. Where the writer runs is up to you; see [Writer thread](#writer-thread).
- **Little work on the caller's thread.** A log call checks the level, formats the message, captures the scopes and queues the entry. Everything else happens on the writer.
- **Batches.** Each batch holds up to `MaxBatchSize` entries, oldest first. By default, entries are written as soon as the writer picks them up. With `FlushInterval` set, a part-full batch waits for more entries: it's written when it's full or when the interval has passed, whichever comes first. `FlushAsync` runs whenever the queue empties.
- **Failures never reach the caller.** If `WriteBatchAsync` or `FlushAsync` throws, that batch is dropped and `OnWriteFailed` is called. Exceptions from `OnWriteFailed` are swallowed too. Never log from these hooks; it would feed back into the queue.
- **Dispose drains the queue.** `Dispose()` doesn't wait for `FlushInterval`. It stops accepting entries and waits up to `ShutdownTimeout` for the queue to be written and for `CloseAsync` to run. If time runs out, the writer's cancellation token is cancelled and anything left is lost. Disposing the logger factory disposes the provider only when the factory owns it, so register with `AddOwnedProvider`, not `AddProvider(instance)`.
- **Lost entries.** After dispose, `IsEnabled` returns false and log calls are ignored. With `MaxQueueLength` set, entries logged while the queue is full are dropped.
- **Safe constructor.** The hooks only run once an entry arrives or the provider is disposed, so your constructor has always finished by then.

## API

| Member | Description |
|---|---|
| `protected BatchingLoggerProvider(TOptions options)` | Validates the options (`MaxBatchSize` ≥ 1) and starts the background writer. |
| `TOptions Options` | The settings the provider was created with. |
| `ILogger CreateLogger(string categoryName)` | |
| `void SetScopeProvider(IExternalScopeProvider scopeProvider)` | Called by the logger factory; scopes are captured when `IncludeScopes` is on. |
| `void Dispose()` | Writes what's queued (up to `ShutdownTimeout`), then runs `CloseAsync`. |
| `protected abstract Task WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)` | Write the batch. Don't keep the list: it's reused for the next batch. The entries themselves are safe to keep. |
| `protected virtual Task FlushAsync(CancellationToken cancellationToken)` | Called after the queue empties, e.g. to flush a buffered stream. Does nothing by default. |
| `protected virtual Task CloseAsync()` | Called once, after the last batch, to release files or connections. Does nothing by default. |
| `protected virtual void OnWriteFailed(Exception exception, IReadOnlyList<LogEntry> batch)` | Called when writing or flushing throws. `batch` is empty for a flush failure. Does nothing by default; override it to reset a connection or count failures. |

The cancellation token is only cancelled when `Dispose` runs out of time.

## Writer thread

`WriterThread` chooses where the writer runs. None of the choices use CPU while idle. An idle writer is either a task waiting for entries or a thread blocked until one arrives.

| `LogWriterThread` | Runs on | Use it when |
|---|---|---|
| `ThreadPool` (default) | A thread-pool thread, borrowed only while writing | Almost always. It holds no thread while idle. |
| `Dedicated` | Its own background thread, named `Niddy log writer (<provider type>)` | Logs must keep flowing even if the app starves the thread pool, e.g. crash logs. It costs one thread per provider. |
| `Shared` | One background thread, `Niddy log writer (shared)`, used by every provider that chooses `Shared` | You want that same independence from the pool for several providers on a single thread. |

How the threads behave:

- **Staying on the thread.** With `Dedicated` or `Shared`, the hooks run on that thread, and an `await` inside a hook comes back to it unless you write `ConfigureAwait(false)`.
- **Never block on async code in a hook.** That means `.Result`, `.Wait()` or `GetAwaiter().GetResult()`. As on a UI thread, it can deadlock the writer for good.
- **Blocking work on `Shared`.** Providers on the shared thread take turns. One that blocks, such as a synchronous database call waiting on a timeout, holds up the others. Use `Shared` only for writers that await their I/O, and give a blocking writer `Dedicated` instead.
- **When threads stop.** A dedicated thread stops when its provider is disposed. The shared thread stops when the last provider using it is disposed, and a new one starts if another comes along. Both are background threads, so they never keep the app alive.
- **Browser and WASI.** Threads can't be started there, so `Dedicated` and `Shared` quietly fall back to `ThreadPool`.
- **Async I/O still uses the pool.** Truly async I/O, such as a network call inside your writer, completes on the runtime's I/O threads either way. That's why [`FileLoggerProvider`](../Files/FileLoggerProvider.md) writes synchronously, so a file writer on `Dedicated` doesn't depend on the thread pool at all.

## Example: a database provider

This example writes to SQLite with `Microsoft.Data.Sqlite` and assumes a `Logs` table already exists. Any other database follows the same shape.

```csharp
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Niddy.Core.Logging;
using Niddy.Core.Logging.Batching;

public sealed class SqliteLoggerOptions : BatchingLoggerOptions
{
    public required string ConnectionString { get; set; }
}

[ProviderAlias("Sqlite")]   // lets "Logging:Sqlite:LogLevel" configuration target this provider
public sealed class SqliteLoggerProvider(SqliteLoggerOptions options)
    : BatchingLoggerProvider<SqliteLoggerOptions>(options)
{
    private SqliteConnection? _connection;

    protected override async Task WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        if (_connection == null)
        {
            _connection = new SqliteConnection(Options.ConnectionString);
            await _connection.OpenAsync(cancellationToken);
        }

        // One transaction per batch: far faster than one insert at a time.
        using SqliteTransaction transaction = _connection.BeginTransaction();
        foreach (LogEntry entry in batch)
        {
            using SqliteCommand command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO Logs (Timestamp, Level, Category, EventId, Message, Exception) " +
                "VALUES ($timestamp, $level, $category, $eventId, $message, $exception)";
            command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("$level", entry.Level.ToString());
            command.Parameters.AddWithValue("$category", entry.Category);
            command.Parameters.AddWithValue("$eventId", entry.EventId.Id);
            command.Parameters.AddWithValue("$message", entry.Message);
            command.Parameters.AddWithValue("$exception", (object?)entry.Exception?.ToString() ?? DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        transaction.Commit();
    }

    // Runs on the writer task, so touching _connection here is safe. Reconnect on the next batch.
    protected override void OnWriteFailed(Exception exception, IReadOnlyList<LogEntry> batch)
    {
        _connection?.Dispose();
        _connection = null;
    }

    protected override async Task CloseAsync()
    {
        if (_connection != null)
        {
            await _connection.DisposeAsync();
        }
    }
}

public static class SqliteLoggingExtensions
{
    public static ILoggingBuilder AddSqlite(this ILoggingBuilder builder, string connectionString,
        Action<SqliteLoggerOptions>? configure = null)
    {
        SqliteLoggerOptions options = new()
        {
            ConnectionString = connectionString,
            MaxQueueLength = 10_000,
            FlushInterval = TimeSpan.FromSeconds(2),   // up to 100 rows per transaction, at most 2 s late
        };
        configure?.Invoke(options);
        return builder.AddOwnedProvider(_ => new SqliteLoggerProvider(options));
    }
}

// In the app: files plus the database.
using ILoggerFactory loggerFactory = NiddyLogging.CreateFactory(paths, LogLevel.Information,
    b => b.AddSqlite("Data Source=logs.db", o => o.MinimumLevel = LogLevel.Warning));
```

For network or database writers, set two options:

- `FlushInterval` turns a burst of single entries into one round trip.
- `MaxQueueLength` makes an outage drop new entries instead of filling memory.

## LogEntry

[source](../../../../src/Niddy.Core/Logging/Batching/LogEntry.cs)

```csharp
public sealed record LogEntry(
    DateTimeOffset Timestamp, LogLevel Level, string Category, EventId EventId, string Message,
    Exception? Exception, IReadOnlyList<object?> Scopes, IReadOnlyList<KeyValuePair<string, object?>> Properties);
```

One entry, captured on the thread that logged it. Each field:

| Field | Meaning |
|---|---|
| `Timestamp` | Taken from `TimeProvider`, in UTC when `UseUtc` is on and local time otherwise. |
| `Message` | The formatted text. Entries with no message and no exception are skipped. |
| `Scopes` | The active scopes, outermost first. Empty unless `IncludeScopes` is on. |
| `Properties` | The structured values of a message template. `"Saved {OrderId}"` gives `OrderId` plus the template itself under `{OriginalFormat}`. Empty when the state wasn't a key/value list. |

Write `Properties` into separate columns or a JSON column if you want structured logs.

## BatchingLoggerOptions

[source](../../../../src/Niddy.Core/Logging/Batching/BatchingLoggerOptions.cs)

The settings every batching provider shares. Derive from it to add your own, as [`FileLoggerOptions`](../Files/FileLoggerOptions.md) does.

| Property | Default | Description |
|---|---|---|
| `LogLevel MinimumLevel` | `Information` | The least severe level written. The factory's own filters apply as well. |
| `bool IncludeScopes` | false | Capture the active scopes in `LogEntry.Scopes`. |
| `bool UseUtc` | false | UTC timestamps instead of local time. |
| `TimeProvider TimeProvider` | `TimeProvider.System` | The clock; replace it in tests. |
| `int MaxBatchSize` | 100 | The most entries passed to `WriteBatchAsync` at once; must be at least 1. |
| `LogWriterThread WriterThread` | `ThreadPool` | Where the writer runs; see [Writer thread](#writer-thread). |
| `TimeSpan FlushInterval` | zero | How long a part-full batch waits for more entries. Zero writes them as soon as they're picked up. The wait uses `TimeProvider`. |
| `int MaxQueueLength` | 0 (no limit) | The most entries waiting to be written. New entries are dropped while the queue is full. |
| `TimeSpan ShutdownTimeout` | 5 seconds | How long `Dispose` waits for the queue to drain. |
