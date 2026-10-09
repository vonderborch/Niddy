# Settings, logging, paths and secrets

How the Niddy.Core pieces fit together in any app, with or without Avalonia. It uses [AppPaths](../Niddy.Core/IO/AppPaths.md), [SettingsStore](../Niddy.Core/Settings/SettingsStore.md), [NiddyLogging](../Niddy.Core/Logging/NiddyLogging.md), [SecureStorageFactory](../Niddy.Core/Security/SecureStorageFactory.md), [AtomicFile](../Niddy.Core/IO/AtomicFile.md) and [Result](../Niddy.Core/Results/Result.md).

## One place for folders

`AppPaths` gives per-platform `Data`, `Config`, `Cache` and `Logs` folders. Everything else hangs off it.

```csharp
using Microsoft.Extensions.Logging;
using Niddy.Core.IO;
using Niddy.Core.Logging;
using Niddy.Core.Results;
using Niddy.Core.Security;
using Niddy.Core.Settings;

AppPaths paths = AppPaths.For("MyApp", "MyCompany").EnsureCreated();
```

In a [`NiddyApp`](../Niddy.Avalonia/Hosting/NiddyApp.md), `NiddyApp.Current!.Paths` is the same thing, named after `AppName`.

## Settings

```csharp
public sealed class AppSettings
{
    public string Workspace { get; set; } = "";
    public int FontSize { get; set; } = 14;
    public List<string> RecentFiles { get; set; } = [];
}

public sealed class AppSettingsStore(AppPaths paths)
    : SettingsStore<AppSettings>(Path.Combine(paths.Config, "settings.json"));

AppSettingsStore settings = new(paths);
settings.Load();                                  // defaults if the file is missing or broken
settings.Update(s => s.RecentFiles.Insert(0, file));   // saves atomically, raises Changed
settings.Watch();                                 // pick up edits made outside the app
settings.Changed += (_, e) => Dispatcher.UIThread.Post(() => ApplyFontSize(e.Settings.FontSize));
```

`Changed` can be raised on a background thread (by `Watch`), so marshal to the UI thread as above.

When a later release renames or reshapes a setting, version the store so existing files are upgraded instead of losing the value. Here, `Workspace` used to be saved as `"folder"`:

```csharp
public sealed class AppSettingsStore(AppPaths paths)
    : SettingsStore<AppSettings>(Path.Combine(paths.Config, "settings.json"))
{
    public override int Version => 1;

    // fromVersion 0: files from before versioning. RenameProperty is in Niddy.Core.Serialization.
    protected override void ConfigureMigrations(SettingsMigrations migrations) =>
        migrations.Add(fromVersion: 0, json => json.RenameProperty("folder", "workspace"));
}
```

The old file is backed up and saved at the new version the first time it's read. See [Versioning](../Niddy.Core/Settings/SettingsStore.md#versioning).

## Logging

```csharp
using ILoggerFactory loggerFactory = NiddyLogging.CreateFactory(paths, LogLevel.Debug);   // rolling files in paths.Logs
ILogger log = loggerFactory.CreateLogger("Startup");
log.LogInformation("Started, data in {Folder}", paths.Data);
```

With `Microsoft.Extensions.Hosting` or DI, use `services.AddLogging(b => b.AddFile(paths.Logs, o => o.RetainedFileCount = 14))`. In a `NiddyApp`, pass the factory as `options.LoggerFactory` so unhandled exceptions are logged too.

Other providers go in the `configure` callback, e.g. `NiddyLogging.CreateFactory(paths, LogLevel.Debug, b => b.AddConsole())`. To skip files entirely, use `NiddyLogging.CreateFactory(b => ...)`. To write logs somewhere new, such as a database, derive from [BatchingLoggerProvider](../Niddy.Core/Logging/Batching/BatchingLoggerProvider.md) and register it with `AddOwnedProvider`.

## Secrets

Never put tokens in the settings file. Use the platform's secure store:

```csharp
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, serviceName: "MyApp");
secrets.SetToken("github", token);
string? saved = secrets.GetToken("github");
secrets.SetToken("github", null);   // delete
```

## Files that must never be half-written

```csharp
Result saved = Result.Try(() => AtomicFile.WriteAllText(Path.Combine(paths.Data, "notes.md"), text));
if (!saved.IsSuccess)
    log.LogError(saved.Error!.Exception, "Couldn't save notes: {Message}", saved.Error.Message);
```

## See also

- [app-setup](app-setup.md), [oauth-sign-in](oauth-sign-in.md)
