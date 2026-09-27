# StreamHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/StreamHelpers.cs)

Reads a whole stream into memory.

## API

| Member | Description |
|---|---|
| `byte[] ReadToBytes(Stream stream)` | Reads the rest of the stream as bytes. |
| `Task<byte[]> ReadToBytesAsync(Stream stream, CancellationToken cancellationToken = default)` | Async version. |
| `string ReadToString(Stream stream, Encoding? encoding = null)` | Reads the rest of the stream as text (UTF-8 by default). |
| `Task<string> ReadToStringAsync(Stream stream, Encoding? encoding = null, CancellationToken cancellationToken = default)` | Async version. |

## Example

```csharp
using Niddy.Helpers;

// e.g. a file picked with Niddy.Avalonia's FilePicker, which works on every platform
await using var stream = await file.OpenReadAsync();
string text = await StreamHelpers.ReadToStringAsync(stream);
```
