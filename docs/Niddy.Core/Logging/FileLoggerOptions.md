# FileLoggerOptions

`Niddy.Logging` · Niddy.Core · [source](../../../src/Niddy.Core/Logging/FileLoggerOptions.cs)

Settings for [`FileLoggerProvider`](FileLoggerProvider.md).

| Property | Default | Description |
|---|---|---|
| `required string Directory` | | The folder to write to; created if needed. |
| `string FileNamePrefix` | `"log"` | Files are named `{prefix}-{yyyyMMdd}.log`. |
| `LogLevel MinimumLevel` | `Information` | The least severe level written. |
| `long MaxFileSizeBytes` | 10 MB | Size before a new file starts for the same day (`{prefix}-{yyyyMMdd}_1.log`, …); ≤ 0 means no limit. |
| `int RetainedFileCount` | 7 | Files to keep; older ones are deleted when a new file starts. ≤ 0 keeps all. |
| `bool IncludeScopes` | false | Include active logging scopes in each line. |
| `bool UseUtc` | false | UTC timestamps and file dates instead of local time. |
| `TimeProvider TimeProvider` | `TimeProvider.System` | The clock; replace it in tests. |

## Example

```csharp
builder.Logging.AddFile(paths.Logs, o =>
{
    o.FileNamePrefix = "myapp";
    o.MaxFileSizeBytes = 2 * 1024 * 1024;
    o.RetainedFileCount = 30;
    o.UseUtc = true;
});
```
