# FileLoggerOptions

`Niddy.Core.Logging.Files` · Niddy.Core · [source](../../../../src/Niddy.Core/Logging/Files/FileLoggerOptions.cs)

Settings for [`FileLoggerProvider`](FileLoggerProvider.md). Derives from [`BatchingLoggerOptions`](../Batching/BatchingLoggerProvider.md#batchingloggeroptions).

| Property | Default | Description |
|---|---|---|
| `required string Directory` | | The folder to write to; created if needed. |
| `string FileNamePrefix` | `"log"` | Files are named `{prefix}-{yyyyMMdd}.log`. |
| `long MaxFileSizeBytes` | 10 MB | Size before a new file starts for the same day (`{prefix}-{yyyyMMdd}_1.log`, …); ≤ 0 means no limit. |
| `int RetainedFileCount` | 7 | Files to keep; older ones are deleted when a new file starts. ≤ 0 keeps all. |

Inherited from `BatchingLoggerOptions`:

| Property | Default | Description |
|---|---|---|
| `LogLevel MinimumLevel` | `Information` | The least severe level written. |
| `bool IncludeScopes` | false | Include active logging scopes in each line. |
| `bool UseUtc` | false | UTC timestamps and file dates instead of local time. |
| `TimeProvider TimeProvider` | `TimeProvider.System` | The clock; replace it in tests. |
| `int MaxBatchSize` | 100 | Lines written per batch before the file is flushed. |
| `LogWriterThread WriterThread` | `ThreadPool` | Where lines are written: borrowed pool threads, a thread of its own (`Dedicated`), or one thread shared with other providers. See [Writer thread](../Batching/BatchingLoggerProvider.md#writer-thread). |
| `TimeSpan FlushInterval` | zero | How long to hold a part-full batch; zero writes lines as soon as possible. |
| `int MaxQueueLength` | 0 (no limit) | The most lines waiting to be written; extra lines are dropped. |
| `TimeSpan ShutdownTimeout` | 5 seconds | How long disposing waits for queued lines to be written. |

## Example

```csharp
builder.Logging.AddFile(paths.Logs, o =>
{
    o.FileNamePrefix = "myapp";
    o.MaxFileSizeBytes = 2 * 1024 * 1024;
    o.RetainedFileCount = 30;
    o.UseUtc = true;
    o.WriterThread = LogWriterThread.Dedicated;   // keep logging even if the thread pool is starved
});
```
