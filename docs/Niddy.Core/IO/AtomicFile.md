# AtomicFile

`Niddy.IO` · Niddy.Core · [source](../../../src/Niddy.Core/IO/AtomicFile.cs)

Writes files so readers only ever see the old contents or the new ones, never a half-written file, even if the app crashes or the power fails mid-write. Contents go to a temporary file in the same directory, which is flushed and then renamed over the target. The directory is created if needed; on Windows the rename is retried briefly if another process (e.g. a virus scanner) holds the file.

## API

| Member | Description |
|---|---|
| `WriteAllText(string path, string contents, Encoding? encoding = null)` | UTF-8 without a BOM by default. |
| `WriteAllTextAsync(string path, string contents, Encoding? encoding = null, CancellationToken ct = default)` | |
| `WriteAllBytes(string path, ReadOnlySpan<byte> bytes)` | |
| `WriteAllBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken ct = default)` | |
| `Write(string path, Action<Stream> write)` | Write through a stream, e.g. for a serializer. |
| `WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken ct = default)` | |

If the delegate throws, the temporary file is deleted and the target is untouched.

## Examples

```csharp
using Niddy.IO;

AtomicFile.WriteAllText(notesPath, editor.Text);

await AtomicFile.WriteAsync(exportPath, async (stream, ct) =>
    await JsonSerializer.SerializeAsync(stream, document, cancellationToken: ct));
```

## See also

- [JsonHelpers](../Helpers/JsonHelpers.md) `SerializeToFile` and [SettingsStore](../Settings/SettingsStore.md) both write through `AtomicFile`.
