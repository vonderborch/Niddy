# JsonExtensions

`Niddy.Core.Serialization` · Niddy.Core · [source](../../../src/Niddy.Core/Serialization/JsonExtensions.cs)

C# 14 static extensions on `JsonSerializerOptions` and `JsonSerializer`: Niddy's default options, and a deserialize that returns `default` instead of throwing.

## API

| Member | Description |
|---|---|
| `static JsonSerializerOptions JsonSerializerOptions.Niddy { get; }` | Niddy's default options: camelCase names, indented, enums as strings, fields included. It's one shared, read-only instance. To change something, make a copy: `new JsonSerializerOptions(JsonSerializerOptions.Niddy)`. [JsonFile](JsonFile.md) and [SettingsStore](../Settings/SettingsStore.md) use it when you don't pass options. |
| `static TValue? JsonSerializer.DeserializeOrDefault<TValue>(string? json, JsonSerializerOptions? options = null)` | Deserializes `json`, or returns `default` when it's null, empty or whitespace, or isn't valid for `TValue` (that is, when deserializing throws `JsonException`, `NotSupportedException` or `ArgumentException`). Null `options` means `JsonSerializerOptions.Niddy`. |

There's no serialize shortcut. Use `JsonSerializer.Serialize(value, JsonSerializerOptions.Niddy)`.

## Example

```csharp
using System.Text.Json;
using Niddy.Core.Serialization;

string json = JsonSerializer.Serialize(project, JsonSerializerOptions.Niddy);
Project? copy = JsonSerializer.DeserializeOrDefault<Project>(clipboardText);   // null for junk

JsonSerializerOptions compact = new(JsonSerializerOptions.Niddy) { WriteIndented = false };
```

## See also

- [JsonFile](JsonFile.md) to read and write JSON files.
