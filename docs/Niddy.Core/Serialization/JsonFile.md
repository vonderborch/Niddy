# JsonFile

`Niddy.Core.Serialization` · Niddy.Core · [source](../../../src/Niddy.Core/Serialization/JsonFile.cs)

Reads and writes JSON files with `System.Text.Json`. Writes are atomic, going through [AtomicFile](../IO/AtomicFile.md), and reads don't throw for a file that's missing, empty or invalid.

## API

| Member | Description |
|---|---|
| `TValue? Read<TValue>(string path, JsonSerializerOptions? options = null)` | Reads and deserializes a file. Returns `default` if the file is missing, empty or isn't valid JSON for `TValue`. |
| `Task<TValue?> ReadAsync<TValue>(string path, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` | The async version. |
| `void Write<TValue>(string path, TValue value, JsonSerializerOptions? options = null)` | Serializes `value` and writes it atomically. |
| `Task WriteAsync<TValue>(string path, TValue value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default)` | The async version. |

When `options` is null, [`JsonSerializerOptions.Niddy`](JsonExtensions.md) is used.

## Example

```csharp
using Niddy.Core.Serialization;

Project project = JsonFile.Read<Project>(path) ?? new();
project.LastOpened = DateTimeOffset.Now;
await JsonFile.WriteAsync(path, project);   // temp file + replace
```

## See also

- [JsonExtensions](JsonExtensions.md) for `JsonSerializerOptions.Niddy` and `JsonSerializer.DeserializeOrDefault`.
- [SettingsStore](../Settings/SettingsStore.md) for a settings file with defaults, change events and reload.
