# JsonHelpers

`Niddy.Helpers` · Niddy.Core · [source](../../../src/Niddy.Core/Helpers/JsonHelpers.cs)

`System.Text.Json` shortcuts with sensible default options. Writes to files are atomic (via [AtomicFile](../IO/AtomicFile.md)).

## API

| Member | Description |
|---|---|
| `static readonly JsonSerializerOptions DefaultOptions` | camelCase names, indented, enums as strings, fields included; used when `options` is null. |
| `TValue? DeserializeFromFile<TValue>(string path, JsonSerializerOptions? options = null)` | Reads and deserializes a file; `default` if the file is missing or empty. |
| `Task<TValue?> DeserializeFromFileAsync<TValue>(string path, JsonSerializerOptions? options = null, CancellationToken ct = default)` | Async version. |
| `TValue? DeserializeString<TValue>(string json, JsonSerializerOptions? options = null)` | Deserializes a string. |
| `void SerializeToFile<TValue>(string path, TValue value, JsonSerializerOptions? options = null)` | Serializes and writes atomically. |
| `Task SerializeToFileAsync<TValue>(string path, TValue value, JsonSerializerOptions? options = null, CancellationToken ct = default)` | Async version. |
| `string SerializeToString<TValue>(TValue value, JsonSerializerOptions? options = null)` | Serializes to a string. |

## Example

```csharp
using Niddy.Helpers;

var project = JsonHelpers.DeserializeFromFile<Project>(path) ?? new Project();
project.LastOpened = DateTimeOffset.Now;
await JsonHelpers.SerializeToFileAsync(path, project);   // temp file + replace
```

## See also

- [SettingsStore](../Settings/SettingsStore.md) for a settings file with defaults, change events and reload.
