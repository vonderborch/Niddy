# Retry

`Niddy.Core.Resilience` · Niddy.Core · [source](../../../src/Niddy.Core/Resilience/Retry.cs)

Runs an action, retrying with exponential backoff, and optional random jitter, when it throws. How it retries is set by a `RetryOptions` record, which you can define once and share. When it stops retrying, it throws a `MaxRetriesException` or a `RetryBlockedException` that carries every attempt's exception.

## API

| Member | Description |
|---|---|
| `void Execute(Action action, RetryOptions? options = null)` | Sync, no result. |
| `T Execute<T>(Func<T> func, RetryOptions? options = null)` | Sync, with a result. |
| `Task ExecuteAsync(Func<Task> action, RetryOptions? options = null, CancellationToken cancellationToken = default)` | Async, no result. |
| `Task<T> ExecuteAsync<T>(Func<Task<T>> func, RetryOptions? options = null, CancellationToken cancellationToken = default)` | Async, with a result. |

Null `options` means `RetryOptions.Default`.

The `cancellationToken` is checked before each attempt and cancels the waits. Once it's cancelled, a failed attempt isn't retried: an `OperationCanceledException` from a cancelled action comes straight out, unwrapped. Pass the token into the action too, so an attempt in progress stops.

### RetryOptions

[source](../../../src/Niddy.Core/Resilience/RetryOptions.cs)

An immutable record: share one instance, and derive variants with `with`. Every value is checked when it's set, so a bad value throws `ArgumentOutOfRangeException` where the options are built, not later.

| Property | Default | Meaning |
|---|---|---|
| `int MaxAttempts` | `3` | The total number of attempts, including the first. At least 1. |
| `TimeSpan BaseDelay` | 100 ms | The wait after the first failed attempt. It doubles after each one after that. |
| `TimeSpan MaxDelay` | 3 s | The cap on the doubling wait. Set it equal to `BaseDelay` for a fixed wait. |
| `TimeSpan MaxJitter` | zero | A random extra wait of between zero and this, added to every wait (after the cap). It spreads out callers that failed together, so their retries don't all arrive at once. |
| `Func<Exception, bool>? ShouldRetry` | null (retry everything) | Which exceptions are worth another attempt. An exception it rejects is thrown straight away, unchanged. |
| `Func<RetryAttempt, bool>? OnRetry` | null | Called after a failed attempt, before the wait, when another attempt will follow. Return `true` to keep retrying, or `false` to stop with a `RetryBlockedException`. Use it to log, or to fix what made the attempt fail. It isn't called after the last attempt. |
| `static RetryOptions Default` | | The defaults above. |

The wait after failed attempt *n* (1-based) is `min(BaseDelay × 2ⁿ⁻¹, MaxDelay)` plus the jitter. Large attempt counts can't overflow. The waits (and the three `TimeSpan` values) can't be negative or longer than `int.MaxValue` milliseconds (about 24.8 days).

`RetryAttempt` is a `readonly record struct` passed to `OnRetry`:

| Property | Meaning |
|---|---|
| `int Number` | The number of the attempt that failed, starting at 1. |
| `Exception Exception` | What it threw. |
| `TimeSpan Delay` | How long `Retry` will wait before the next attempt, jitter included. |

### Exceptions

[source](../../../src/Niddy.Core/Resilience/RetryException.cs)

| Exception | Thrown when |
|---|---|
| `MaxRetriesException` | Every attempt failed with an exception `ShouldRetry` accepted. With `MaxAttempts = 1`, that's the first failure. |
| `RetryBlockedException` | `OnRetry` returned `false`. |
| `RetryException` | The abstract base of both. Catch it to handle either. |

Both have:

- `InnerException`: the last attempt's exception.
- `IReadOnlyList<Exception> Exceptions`: every attempt's exception, in order.
- `int Attempts`: the number of attempts made.

Their messages read "Gave up after 3 attempts: {last message}" and "Retrying was stopped after attempt 2: {last message}". Exceptions `ShouldRetry` rejects, and cancellation, are never wrapped.

## Examples

```csharp
using System.Net;
using Niddy.Core.Resilience;

// A field, so every call shares the same settings
private static readonly RetryOptions FileRetry = new()
{
    MaxAttempts = 5,
    ShouldRetry = ex => ex is IOException or UnauthorizedAccessException,   // a bug shouldn't be retried
};

Retry.Execute(() => File.Move(temp, target, overwrite: true), FileRetry);
Retry.Execute(() => File.Copy(a, b, overwrite: true), FileRetry with { MaxAttempts = 2 });

// Many clients calling one server: back off with jitter, log each retry, and stop early on a 4xx
string body = await Retry.ExecuteAsync(
    () => http.GetStringAsync(url, ct),
    new RetryOptions
    {
        MaxAttempts = 4,
        BaseDelay = TimeSpan.FromMilliseconds(250),
        MaxDelay = TimeSpan.FromSeconds(5),
        MaxJitter = TimeSpan.FromMilliseconds(250),
        ShouldRetry = ex => ex is HttpRequestException,
        OnRetry = attempt =>
        {
            logger.LogWarning(attempt.Exception, "Attempt {Number} failed, retrying in {Delay}", attempt.Number, attempt.Delay);
            return attempt.Exception is not HttpRequestException { StatusCode: >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError };
        },
    },
    ct);
```

Handling the outcome:

```csharp
try
{
    await Retry.ExecuteAsync(() => SyncAsync(ct), cancellationToken: ct);
}
catch (MaxRetriesException ex)
{
    logger.LogError(ex.InnerException, "Sync failed {Attempts} times", ex.Attempts);
}
catch (RetryBlockedException ex)
{
    logger.LogWarning(ex.InnerException, "Sync stopped retrying");
}
```

Combine with [`Result.TryAsync`](../Results/Result.md) to turn the final failure into a value:

```csharp
Result<string> result = await Result.TryAsync(() => Retry.ExecuteAsync(() => http.GetStringAsync(url)));
```

## See also

- [FileSystemExtensions](../IO/FileSystemExtensions.md#safe-deletes): `File.DeleteWithRetry` and `Directory.DeleteWithRetry` are built on `Retry`, with the file-specific clean-up in `OnRetry`.
