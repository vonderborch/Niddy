# Result

`Niddy.Results` · Niddy.Core · [source](../../../src/Niddy.Core/Results/Result.cs)

`Result` and `Result<T>` hold either success (with a value, for `Result<T>`) or an [`Error`](Error.md), so an expected failure can be returned rather than thrown. Both are readonly structs. `default` is a failure with `Error.Uninitialized`, so a result that was never assigned is never mistaken for success.

## API: `Result`

| Member | Description |
|---|---|
| `IsSuccess`, `IsFailure` | |
| `Error? Error` | Null on success. |
| `static Success()` / `static Success<T>(T value)` | |
| `static Failure(Error error)` / `static Failure(string message, string? code = null)` | |
| `static Try(Action)` / `Try<T>(Func<T>)` | Runs the delegate and turns any exception into a failure (`Error.FromException`). |
| `static TryAsync(Func<Task>)` / `TryAsync<T>(Func<Task<T>>)` | The async versions. **Cancellation (`OperationCanceledException`) is rethrown**, not captured. |
| `static Combine(params IEnumerable<Result>)` | The first failure, or success. |
| `Match(onSuccess, onFailure)` | |
| `Bind(Func<Result>)` / `Bind<T>(Func<Result<T>>)` | Runs the next step only on success. |
| `OnSuccess(Action)` / `OnFailure(Action<Error>)` | Side effects; return the same result. |
| `ThrowIfFailure()` | Throws `ResultException`. |
| implicit `Error → Result` | `return new Error("...");` or `return (Error)"..."`. |

## API: `Result<T>`

| Member | Description |
|---|---|
| `T Value` | Throws `ResultException` on failure. |
| `TryGetValue(out T value)` / `TryGetValue(out T value, out Error? error)` | |
| `GetValueOrDefault(T fallback)` / `GetValueOrDefault()` | |
| `Match`, `Map`, `MapError`, `Bind` (to `Result<TOut>` or `Result`), `MapAsync`, `BindAsync` | Chaining. |
| `Ensure(Func<T,bool> predicate, Error error)` | Fails with `error` if the predicate is false. |
| `OnSuccess(Action<T>)` / `OnFailure(Action<Error>)` | |
| `ToOption()` | [`Option<T>`](Option.md); the error is dropped. |
| `ToResult()` | Drops the value. |
| `Deconstruct(out bool isSuccess, out T? value, out Error? error)` | `var (ok, value, error) = result;` |
| implicit `T → Result<T>`, `Error → Result<T>`, `Result<T> → Result` | |

`ResultException` is an `InvalidOperationException` with an `Error` property; its inner exception is `Error.Exception`.

## Examples

```csharp
using Niddy.Results;

public Result<User> FindUser(string id)
{
    if (string.IsNullOrWhiteSpace(id))
        return new Error("An ID is required.", "InvalidId");   // Error → Result<User>
    var user = _db.Find(id);
    if (user is null)
        return Result<User>.Failure("Not found", "NotFound");
    return user;                                           // User → Result<User>
}
```

`Result.Failure(...)` makes a non-generic `Result`; for `Result<T>` use `Result<T>.Failure(...)` or return an `Error`.

Chaining:

```csharp
Result<Order> order = FindUser(id)
    .Ensure(u => u.IsActive, new Error("Account is disabled.", "Disabled"))
    .Bind(u => _orders.Create(u, items));        // Func<User, Result<Order>>

string message = order.Match(
    o => $"Order {o.Number} placed",
    e => $"Couldn't place order: {e.Message}");
```

Wrapping code that throws:

```csharp
Result<Config> config = Result.Try(() => JsonHelpers.DeserializeFromFile<Config>(path)!);
Result<string> page = await Result.TryAsync(() => http.GetStringAsync(url, ct));

if (!page.TryGetValue(out var html, out var error))
    logger.LogWarning(error!.Exception, "Download failed: {Error}", error);
```

Validating several things:

```csharp
Result valid = Result.Combine(
    ValidateName(name),
    ValidateEmail(email),
    ValidateAge(age));
```

## See also

- [Option](Option.md): a value that may be missing, with no error.
- [Error](Error.md)
- [examples/oauth-sign-in](../../examples/oauth-sign-in.md)
