# Retry

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/Retry.cs)

Runs an action, retrying with exponential backoff when it throws. The delay before attempt *n* (0-based) is `min(baseDelayMs × 2ⁿ, maxDelayMs)`. The last attempt's exception is rethrown.

## API

| Member | Description |
|---|---|
| `void Execute(Action action, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000)` | Sync, no result. |
| `T Execute<T>(Func<T> func, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000)` | Sync, with a result. |
| `Task ExecuteAsync(Func<Task> action, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000, CancellationToken cancellationToken = default)` | Async; the token cancels the waits. |
| `Task<T> ExecuteAsync<T>(Func<Task<T>> func, ...same...)` | Async, with a result. |

`maxRetries` is the total number of attempts. Every exception is retried; filter inside the delegate if some shouldn't be.

## Examples

```csharp
using Niddy.Helpers;

Retry.Execute(() => File.Move(temp, target, overwrite: true), maxRetries: 5);

string body = await Retry.ExecuteAsync(
    () => http.GetStringAsync(url, ct),
    maxRetries: 4, baseDelayMs: 250, maxDelayMs: 5000, cancellationToken: ct);
```

Combine with [`Result.TryAsync`](../Results/Result.md) to turn the final failure into a value:

```csharp
Result<string> result = await Result.TryAsync(() => Retry.ExecuteAsync(() => http.GetStringAsync(url)));
```
