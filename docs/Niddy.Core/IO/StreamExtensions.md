# StreamExtensions

`Niddy.Core.IO` · Niddy.Core · [source](../../../src/Niddy.Core/IO/StreamExtensions.cs)

C# 14 extension members on `Stream` that read the rest of a stream into memory.

## API

| Member | Description |
|---|---|
| `byte[] stream.ReadToBytes()` | Reads the rest of the stream as bytes. |
| `Task<byte[]> stream.ReadToBytesAsync(CancellationToken cancellationToken = default)` | The async version. |
| `string stream.ReadToString(Encoding? encoding = null)` | Reads the rest of the stream as text, using UTF-8 by default. |
| `Task<string> stream.ReadToStringAsync(Encoding? encoding = null, CancellationToken cancellationToken = default)` | The async version. |

The stream isn't disposed.

## Example

```csharp
using Niddy.Core.IO;

// e.g. a file picked with Niddy.Avalonia's FilePicker, which works on every platform
await using Stream stream = await file.OpenReadAsync();
string text = await stream.ReadToStringAsync();
```
