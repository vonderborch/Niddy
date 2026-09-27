# Option

`Niddy.Results` · Niddy.Core · [source](../../../src/Niddy.Core/Results/Option.cs)

`Option<T>` is a value that may be missing. Use it where "not there" is normal and needs no explanation (a lookup, a first match). When the caller should know *why* something failed, use [`Result<T>`](Result.md). It's a readonly struct; `default` is None.

## API: `Option<T>`

| Member | Description |
|---|---|
| `static None` / `static Some(T value)` | `Some` throws on null. |
| `HasValue`, `IsNone` | |
| `T Value` | Throws `InvalidOperationException` when None. |
| `TryGetValue(out T value)` | |
| `GetValueOrDefault(T fallback)` / `GetValueOrDefault()` | |
| `Match(some, none)` | |
| `Map(Func<T, TOut?>)` | A null result becomes None. |
| `Bind(Func<T, Option<TOut>>)` | |
| `Where(Func<T,bool>)` | None if the predicate is false. |
| `Or(Func<Option<T>>)` | The alternative when None. |
| `IfSome(Action<T>)` | Side effect; returns the same option. |
| `ToResult(Error error)` | Fails with `error` when None. |
| implicit `T? → Option<T>` | null becomes None. |
| implicit `NoneOption → Option<T>` | So `return Option.None;` works for any `T`. |

## API: `Option` (static)

| Member | Description |
|---|---|
| `NoneOption None` | Converts to any `Option<T>`. |
| `Some<T>(T value)` | |
| `From<T>(T? value)` | For reference types and nullable value types; null becomes None. |
| `GetOption(this IReadOnlyDictionary<TKey,TValue>, TKey key)` | A dictionary lookup. |
| `FirstOrNone(this IEnumerable<T>)` / `FirstOrNone(this IEnumerable<T>, Func<T,bool>)` | Unlike `FirstOrDefault`, this tells "no match" apart from a default value. |

## Examples

```csharp
using Niddy.Results;

Option<User> FindByEmail(string email) => _users.FirstOrNone(u => u.Email == email);

string greeting = FindByEmail(email)
    .Map(u => u.DisplayName)
    .Match(name => $"Welcome back, {name}", () => "Welcome!");

Option<int> ParsePort(string? text) =>
    int.TryParse(text, out var port) && port is > 0 and < 65536 ? port : Option.None;

int port = ParsePort(args.ElementAtOrDefault(0))
    .Or(() => ParsePort(Environment.GetEnvironmentVariable("PORT")))
    .GetValueOrDefault(8080);

Result<string> token = _cache.GetOption("token").ToResult(new Error("Not signed in.", "NoToken"));
```

## See also

- [Result](Result.md)
