# SettingsStore\<T>

`Niddy.Core.Settings` · Niddy.Core · [source](../../../src/Niddy.Core/Settings/SettingsStore.cs)

Typed settings kept in a JSON file. Derive a class that names the file, read with `Current`, and change with `Update`, which saves and raises `Changed`.

- The file is read the first time `Current` is used. A missing file gives `CreateDefault()`.
- A file that can't be read as `T` raises `CorruptFileDetected`. If `OverrideCorruptFile` is true (the default), it is renamed to `*.bad` (so it can be recovered) and the defaults are used. If `OverrideCorruptFile` is false, the corrupt file is left untouched on disk.
- Saves are atomic ([`AtomicFile`](../IO/AtomicFile.md)), so a crash never leaves a half-written file.
- The store is thread-safe, but `Current` is a live object. Change it through `Update` so the change is saved and announced.
- Override `Version` to version the file and upgrade old ones step by step with a lambda per version (`ConfigureMigrations`), choosing what happens if a step fails. See [Versioning](#versioning).

## API

| Member | Description |
|---|---|
| `protected SettingsStore(string filePath, JsonSerializerOptions? serializationOptions = null, bool overrideCorruptFile = true)` | `serializationOptions` defaults to [`JsonSerializerOptions.Niddy`](../Serialization/JsonExtensions.md). |
| `string FilePath` | |
| `bool OverrideCorruptFile` | Whether to set aside (rename to `*.bad`) a corrupt file when reading it. Defaults to `true`. |
| `T Current` | Loaded lazily. |
| `virtual int Version` | The version of the settings this store reads and writes. 0 (the default) means unversioned. |
| `int? FileVersion` | The version the file had when last read, before upgrading. 0 for a file without one; null when there was no file, it couldn't be read, or the store is unversioned. |
| `bool BackupBeforeUpgrade` | Copy the file to `settings.json.v1-20261008T140312Z.bak` before saving an upgraded version over it. Defaults to `true`. Best effort. |
| `SettingsMigrationFailureAction MigrationFailureAction` | What to do when an upgrade step throws: `UseDefaults` (the default), `SkipStep` or `Throw`. See [When an upgrade fails](#when-an-upgrade-fails). |
| `bool TreatMissingMigrationAsError` | Whether a version with no step in `ConfigureMigrations` is a failure, handled like a step that threw. Defaults to `false`: the version is skipped. See [Missing steps](#missing-steps). |
| `T Load()` | Re-reads the file and raises `Changed` (source `Load`). |
| `void Save()` / `Task SaveAsync(CancellationToken)` | Writes `Current`. |
| `T Update(Action<T> change)` | Changes, saves, raises `Changed` (source `Update`); returns the settings. |
| `T Reset()` | Replaces with `CreateDefault()`, saves, raises `Changed` (source `Reset`). |
| `void Watch(TimeSpan? delay = null)` | Reloads when something else edits the file (another instance, a text editor). The store's own saves are ignored. |
| `bool IsWatching`, `void StopWatching()` | |
| `event EventHandler<SettingsChangedEventArgs<T>> Changed` | `e.Settings`, `e.Source`. **While watching, it can be raised on a background thread**; marshal to the UI thread yourself. |
| `event EventHandler<SettingsCorruptFileEventArgs> CorruptFileDetected` | `e.FilePath`, `e.Exception`, `e.IsSetAside`. Raised when a file can't be parsed, or an upgrade step throws. |
| `Dispose()` | Stops watching. |
| `protected virtual void Dispose(bool disposing)` | Override to release a subclass's own resources; call the base method. `disposing` is false from a finalizer. |
| `protected virtual T CreateDefault()` | Defaults to `new T()`. |
| `protected virtual string VersionPropertyName` | The property that holds the version in the file. Defaults to `"$version"`. |
| `protected virtual void ConfigureMigrations(SettingsMigrations migrations)` | Adds the upgrade steps: one lambda per version, with `migrations.Add(fromVersion, json => ...)`. Called once, the first time a file is read. See [SettingsMigrations](#settingsmigrations). |
| `protected virtual void OnUpgraded(T settings, int fromVersion)` | Called once after an older file was migrated and deserialized, before `OnLoaded`. For upgrades that are easier on the typed settings. |
| `protected virtual SettingsMigrationFailureAction OnMigrationFailed(SettingsMigrationException exception)` | Called when an upgrade step or `OnUpgraded` throws, or a step is missing and `TreatMissingMigrationAsError` is set; returns what to do. Defaults to `MigrationFailureAction`. Override to decide per step, or to log. |
| `protected virtual void OnLoaded(T settings)` | After loading or creating, before it becomes `Current`: fill in missing values. |
| `protected virtual void OnChanged(T settings, SettingsChangeSource source)` | Raises `Changed`. |
| `protected virtual void OnCorruptFileDetected(string filePath, Exception exception, bool isSetAside)` | Raises `CorruptFileDetected`. |

`SettingsChangeSource`: `Update`, `Reset`, `Load` (including reloads while watching).

`SettingsCorruptFileEventArgs`: `FilePath`, `Exception`, `IsSetAside`.

## Versioning

Override `Version` once the shape of your settings changes in a way the serializer can't absorb: a renamed, moved or retyped property, or a value that needs converting. Adding a property with a default doesn't need a new version, because a missing property just keeps its default.

- The store writes the version as the first property of the file: `{ "$version": 2, "theme": "Dark", ... }`. It's removed from the JSON before deserializing, so `T` doesn't need (and shouldn't have) a version property.
- A file without a version is version 0. So the first `Version` you declare is 1, and the step from version 0 upgrades files written before versioning was turned on.
- When the file's version is lower than `Version`, the store upgrades it one version at a time, in order, on the raw [`JsonObject`](https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonobject). A file saved at version 1 read by a store at version 5 goes through the steps from 1, 2, 3 and 4: 1→2→3→4→5. It works on the JSON rather than on `T` because an old file often doesn't fit the current class.
- Each step is its own lambda, added in `ConfigureMigrations` with `migrations.Add(fromVersion, json => ...)`, and changes the JSON in place. By default a version without a step is skipped, so you only write the steps that do something; set `TreatMissingMigrationAsError` to make it a failure instead (see [Missing steps](#missing-steps)). Property names are as they appear in the file (camelCase with the default options). `RenameProperty` (from [JsonExtensions](../Serialization/JsonExtensions.md)) renames one in place.
- Then the JSON is deserialized, `OnUpgraded(settings, fromVersion)` runs once, then `OnLoaded`. The old file is backed up (unless `BackupBeforeUpgrade` is false), and the upgraded settings are saved straight away. Backing up and saving are best effort.
- A file that's already at `Version` isn't migrated or rewritten.
- If a step or `OnUpgraded` throws, `MigrationFailureAction` (or your `OnMigrationFailed`) decides what happens. See below.
- A file with a newer version than `Version` (written by a newer copy of the app) is read as it is, without migrating. Properties `T` doesn't know are dropped unless `T` has a `[JsonExtensionData]` property. Saves keep the newer version, so the newer app won't upgrade the file a second time. `FileVersion > Version` tells you this happened.
- Upgrades also apply to files picked up by `Watch`.
- To take over a version property your settings already have, override `VersionPropertyName` (e.g. `"version"`) and remove that property from `T`.

`FileVersion` is the file's version before the upgrade, so after reading `Current`, `FileVersion < Version` means the settings were just upgraded (useful for a "what's new" note).

### When an upgrade fails

When a step or `OnUpgraded` throws, the store wraps the exception in a `SettingsMigrationException` (which says which step) and calls `OnMigrationFailed(exception)`. That returns `MigrationFailureAction` unless you override it. The choices are:

| `SettingsMigrationFailureAction` | What happens |
|---|---|
| `UseDefaults` (default) | The file is treated as unreadable. `CorruptFileDetected` is raised with the `SettingsMigrationException`, the file is set aside as `*.bad` if `OverrideCorruptFile` is set, and the defaults are used. |
| `SkipStep` | Whatever the failed step changed in the JSON is undone, and the upgrade carries on with the next step as if that one had nothing to do. The backup and save happen as usual. If `OnUpgraded` failed, the settings are deserialized again without it. |
| `Throw` | The `SettingsMigrationException` is thrown to whoever read the settings: `Current`, `Load`, `Update`, `Save`. Nothing is set aside, backed up or written, and nothing is cached, so the next read tries again. Use it when you'd rather stop (show an error, exit) than lose settings. A file picked up by `Watch` is ignored instead, and the current settings are kept. `Reset` still overwrites the file, since it doesn't read it. |

Override `OnMigrationFailed` to decide per step, or to log:

```csharp
protected override SettingsMigrationFailureAction OnMigrationFailed(SettingsMigrationException e)
{
    log.LogWarning(e.InnerException, "Settings upgrade {From} -> {To} failed", e.FromVersion, e.ToVersion);
    return e.FromVersion == 3
        ? SettingsMigrationFailureAction.SkipStep      // step 3 only tidies up; fine to skip
        : SettingsMigrationFailureAction.Throw;
}
```

### Missing steps

A version with no step added in `ConfigureMigrations` is a missing step. What happens then depends on `TreatMissingMigrationAsError`:

- `false` (the default): the version has nothing to change and is skipped. A store at version 5 with steps only for 1→2 and 4→5 upgrades a version 1 file through all four.
- `true`: the missing step is a failure. `OnMigrationFailed` gets a `SettingsMigrationException` with `IsMissingStep` set (its `InnerException` is an `InvalidOperationException`), and `MigrationFailureAction` applies as for a step that threw: `UseDefaults` gives up and uses the defaults, `SkipStep` skips it after all (and also gets the call, so you can log it), and `Throw` throws it. Turn it on to make sure every version bump comes with a step, so a forgotten one doesn't silently leave old JSON in place.

```csharp
AppSettingsStore store = new(paths)
{
    TreatMissingMigrationAsError = true,
    MigrationFailureAction = SettingsMigrationFailureAction.Throw,
};
```

### SettingsMigrations

The upgrade steps, passed to `ConfigureMigrations` ([source](../../../src/Niddy.Core/Settings/SettingsMigrations.cs)). The store creates it; you add to it.

| Member | Description |
|---|---|
| `SettingsMigrations Add(int fromVersion, Action<JsonObject> step)` | Adds the step that upgrades the JSON from `fromVersion` to `fromVersion + 1`. Returns this, to chain. Throws `ArgumentException` if there's already a step from `fromVersion`, and `ArgumentOutOfRangeException` if it's negative. |
| `bool Contains(int fromVersion)` | Whether there's a step from `fromVersion`. |
| `int Count` | The number of steps. |
| `IEnumerable<int> FromVersions` | The versions with a step, lowest first. |

- Steps can only be added inside `ConfigureMigrations`; `Add` throws `InvalidOperationException` after it returns.
- The order you add them in doesn't matter; they always run by version.
- A step from `Version` or above could never run, so reading the file throws `InvalidOperationException` (which also catches steps added while `Version` is still 0). Bump `Version` when you add a step.
- A step that's longer than one line can be a method: `.Add(fromVersion: 1, ConvertFontSize)` with `static void ConvertFontSize(JsonObject json)`.

### SettingsMigrationException

Describes an upgrade step that threw, or was missing while `TreatMissingMigrationAsError` is set. It's passed to `OnMigrationFailed`. Then, depending on the action, it's handed to `CorruptFileDetected` as `e.Exception` (`UseDefaults`), thrown out of `Current`, `Load` and so on (`Throw`), or dropped (`SkipStep`).

| Member | Description |
|---|---|
| `int FromVersion` | The version the failed step was upgrading from. |
| `int ToVersion` | `FromVersion + 1` for a step; `Version` for `OnUpgraded`. |
| `bool IsMissingStep` | True when there was no step from `FromVersion` and `TreatMissingMigrationAsError` is set, rather than a step that threw. |
| `InnerException` | What the step threw; an `InvalidOperationException` for a missing step. |

## Examples

```csharp
using System.Text.Json.Nodes;
using Niddy.Core.IO;
using Niddy.Core.Serialization;
using Niddy.Core.Settings;

public sealed class AppSettings
{
    public string Theme { get; set; } = "System";
    public double FontScale { get; set; } = 1.0;
    public List<string> RecentFiles { get; set; } = [];
}

public sealed class AppSettingsStore(AppPaths paths)
    : SettingsStore<AppSettings>(Path.Combine(paths.Config, "settings.json"))
{
    // 1: "colorScheme" renamed to "theme". 2: font size in points became a scale factor.
    public override int Version => 2;

    protected override void ConfigureMigrations(SettingsMigrations migrations) => migrations
        .Add(fromVersion: 0, json => json.RenameProperty("colorScheme", "theme"))
        .Add(fromVersion: 1, json =>
        {
            double points = json["fontSize"]?.GetValue<double>() ?? 14;
            json.Remove("fontSize");
            json["fontScale"] = points / 14;
        });
}

AppSettingsStore store = new(AppPaths.For("MyApp"));

string theme = store.Current.Theme;
store.Update(s => s.RecentFiles.Insert(0, path));

store.Changed += (_, e) => Dispatcher.UIThread.Post(() => ApplyTheme(e.Settings.Theme));
store.Watch();
```

A file from before versioning, `{ "colorScheme": "Dark", "fontSize": 21 }`, is read as `Theme = "Dark"`, `FontScale = 1.5`. It's copied to `settings.json.v0-….bak` and saved as `{ "$version": 2, "theme": "Dark", "fontScale": 1.5, "recentFiles": [] }`.

## See also

- [AppPaths](../IO/AppPaths.md), [JsonFile](../Serialization/JsonFile.md), [JsonExtensions](../Serialization/JsonExtensions.md), [DebouncedFileWatcher](../IO/DebouncedFileWatcher.md)
- [UiStateStore](../../Niddy.Avalonia/State/UiStateStore.md) is a `SettingsStore` for window and layout state.
- [examples/settings-logging-paths](../../examples/settings-logging-paths.md)
