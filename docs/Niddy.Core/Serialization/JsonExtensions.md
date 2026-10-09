# JsonExtensions

`Niddy.Core.Serialization` · Niddy.Core · [source](../../../src/Niddy.Core/Serialization/JsonExtensions.cs)

C# 14 extensions on `JsonSerializerOptions`, `JsonSerializer` and `JsonObject`: Niddy's default options, a deserialize that returns `default` instead of throwing, and `RenameProperty` for upgrading JSON.

## API

| Member | Description |
|---|---|
| `static JsonSerializerOptions JsonSerializerOptions.Niddy { get; }` | Niddy's default options: camelCase names, indented, enums as strings, fields included. It's one shared, read-only instance. To change something, make a copy: `new JsonSerializerOptions(JsonSerializerOptions.Niddy)`. [JsonFile](JsonFile.md) and [SettingsStore](../Settings/SettingsStore.md) use it when you don't pass options. |
| `static TValue? JsonSerializer.DeserializeOrDefault<TValue>(string? json, JsonSerializerOptions? options = null)` | Deserializes `json`, or returns `default` when it's null, empty or whitespace, or isn't valid for `TValue` (that is, when deserializing throws `JsonException`, `NotSupportedException` or `ArgumentException`). Null `options` means `JsonSerializerOptions.Niddy`. |
| `bool JsonObject.RenameProperty(string oldName, string newName)` | Renames a property, keeping its value and its place in the object. An existing `newName` property is replaced. Returns false if there's no `oldName`. Made for [SettingsStore](../Settings/SettingsStore.md#versioning) upgrade steps. |

There's no serialize shortcut. Use `JsonSerializer.Serialize(value, JsonSerializerOptions.Niddy)`.

## Example

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.Serialization;

string json = JsonSerializer.Serialize(project, JsonSerializerOptions.Niddy);
Project? copy = JsonSerializer.DeserializeOrDefault<Project>(clipboardText);   // null for junk

JsonSerializerOptions compact = new(JsonSerializerOptions.Niddy) { WriteIndented = false };

JsonObject json = JsonNode.Parse(oldText)!.AsObject();
json.RenameProperty("colour", "color");   // same value, same position
```

## See also

- [JsonFile](JsonFile.md) to read and write JSON files.
