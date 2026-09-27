# Error

`Niddy.Results` · Niddy.Core · [source](../../../src/Niddy.Core/Results/Error.cs)

Why a [`Result`](Result.md) failed: a message for people, an optional machine-readable code, and the exception behind it, if any. It's a record, so two errors with the same values are equal.

## API

| Member | Description |
|---|---|
| `Error(string Message, string? Code = null, Exception? Exception = null)` | |
| `static Error Uninitialized` | The error of a `default` result (code `"Uninitialized"`). |
| `static FromException(Exception)` | The exception's message, its type name as the code (e.g. `"IOException"`), and the exception itself. |
| implicit `string → Error` | |
| `ToString()` | `"Code: Message"`, or just the message without a code. |

## Examples

```csharp
using Niddy.Results;

Error notFound = new("The project doesn't exist.", "NotFound");
Error simple = "Something went wrong.";             // no code

Result r = Result.Try(() => File.Delete(path));
if (r.Error is { Code: "UnauthorizedAccessException" } e)
    ShowPermissionHelp(e.Message);

// Switch on codes
string text = result.Error?.Code switch
{
    "NotFound" => "Nothing here.",
    "Disabled" => "Your account is disabled.",
    _ => result.Error?.Message ?? "",
};
```

## See also

- [Result](Result.md), [Option](Option.md)
