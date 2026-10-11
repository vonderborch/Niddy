# Niddy documentation

One page per public class, grouped by package and folder, following the source layout (`docs/<Project>/<Folder>/<Class>.md`). Each page has the API and examples. Small supporting types (enums, event args, options records) are covered on the page of the class that uses them.

- New to Niddy? Start with [App setup, end to end](examples/app-setup.md).
- Using an AI assistant? Point it at [AI-GUIDE.md](AI-GUIDE.md), a single-file guide to the whole library.

## Examples

Walkthroughs that combine several classes.

| Page | Covers |
|---|---|
| [App setup, end to end](examples/app-setup.md) | `NiddyApp`, pages in a tree, a menu, an app data context, settings |
| [Settings, logging, paths and secrets](examples/settings-logging-paths.md) | `AppPaths`, `SettingsStore`, `NiddyLogging`, `SecureStorageFactory`, `AtomicFile` |
| [Long-running work](examples/long-running-work.md) | `Dialog.Progress`, `Toast.ShowProgress`, `DataContextBase.RunAsync`, `Retry`, `WindowNotification` |
| [A custom dialog](examples/custom-dialog.md) | `DialogBase<T>`, `DialogLayout`, `Dialog.Show` |
| [Mobile and browser](examples/mobile-and-browser.md) | `AppMode`, overlays, `FilePicker` handles, `Responsive`, back requests |
| [Plugin pages](examples/plugin-pages.md) | Pages from other assemblies, runtime loading, `PageRegistry` |
| [OAuth sign-in](examples/oauth-sign-in.md) | `Dialog.Web` with `closeWhen`, `ISecureStorage` |

## Niddy.Core

UI-independent building blocks. Namespaces follow the folders, e.g. `Niddy.Core.Settings` or `Niddy.Core.IO.Archives`; root types are in `Niddy.Core`.

| Folder | Pages |
|---|---|
| (root) | [DisposableExtensions](Niddy.Core/DisposableExtensions.md): `value.DisposeIfPossible()` |
| Collections | [ListExtensions](Niddy.Core/Collections/ListExtensions.md) |
| GitHub | [GitHubIssues](Niddy.Core/GitHub/GitHubIssues.md): open an issue (bug, feature request, anything) through the API, the browser, or the API with a browser fallback, with `GitHubIssueReference` and `GitHubIssueSubmission` · [GitHubIssue](Niddy.Core/GitHub/GitHubIssue.md): the issue's contents, crash reports from exceptions, with `GitHubIssueReporter` · [GitHubIssueSection](Niddy.Core/GitHub/GitHubIssueSection.md): collapsible logs and other blocks · [GitHubReleases](Niddy.Core/GitHub/GitHubReleases.md): latest, tagged and listed releases, update checks, picking the asset for this platform, checksum-verified downloads and a cache for the rate limit, with `GitHubRelease`, `GitHubReleaseAsset`, `GitHubReleaseOptions` and `GitHubUpdateCheck` |
| IO | [AppPaths](Niddy.Core/IO/AppPaths.md): per-platform app folders · [AtomicFile](Niddy.Core/IO/AtomicFile.md): crash-safe writes · [DebouncedFileWatcher](Niddy.Core/IO/DebouncedFileWatcher.md) · [FileSystemExtensions](Niddy.Core/IO/FileSystemExtensions.md): `File`/`Directory`/`Path` extensions such as deletes that retry · [StreamExtensions](Niddy.Core/IO/StreamExtensions.md) |
| IO.Archives | [Archive](Niddy.Core/IO/Archives/Archive.md): zip, tar and tar.gz, with `ArchiveFormat`, the options records, `ArchiveEntryInfo` and `ArchiveProgress` |
| IO.Backups | [BackupExtensions](Niddy.Core/IO/Backups/BackupExtensions.md): `File.Backup` / `Directory.Backup`, with `BackupNaming` |
| Logging | [NiddyLogging](Niddy.Core/Logging/NiddyLogging.md): one-line file logging, other providers |
| Logging.Files | [FileLoggerProvider](Niddy.Core/Logging/Files/FileLoggerProvider.md) · [FileLoggerOptions](Niddy.Core/Logging/Files/FileLoggerOptions.md) |
| Logging.Batching | [BatchingLoggerProvider](Niddy.Core/Logging/Batching/BatchingLoggerProvider.md): base for your own providers (database, network), with `LogEntry`, `BatchingLoggerOptions` and `LogWriterThread` |
| Resilience | [Retry](Niddy.Core/Resilience/Retry.md): retry with exponential backoff, `MaxRetriesException` |
| Results | [Result](Niddy.Core/Results/Result.md) · [Error](Niddy.Core/Results/Error.md) · [Option](Niddy.Core/Results/Option.md) |
| Security | [ISecureStorage](Niddy.Core/Security/ISecureStorage.md) · [SecureStorageFactory](Niddy.Core/Security/SecureStorageFactory.md): keychain / credential manager / encrypted files, plus pluggable providers (`SecureStorageProvider`, `SecureStorageContext`) · [EncryptedFileSecureStorage](Niddy.Core/Security/EncryptedFileSecureStorage.md) |
| Serialization | [JsonFile](Niddy.Core/Serialization/JsonFile.md): atomic JSON files · [JsonExtensions](Niddy.Core/Serialization/JsonExtensions.md): `JsonSerializerOptions.Niddy`, `JsonSerializer.DeserializeOrDefault`, `JsonObject.RenameProperty` |
| Settings | [SettingsStore\<T>](Niddy.Core/Settings/SettingsStore.md): typed JSON settings with change events, file watching, and versioned step-by-step upgrades (`Version`, `ConfigureMigrations`, [`SettingsMigrations`](Niddy.Core/Settings/SettingsStore.md#settingsmigrations), `SettingsMigrationFailureAction`, `SettingsMigrationException`) |
| SystemInfo | [Platform](Niddy.Core/SystemInfo/Platform.md): OS and version, architecture, target triple, memory and GPUs; declaring supported OSes |
| Text | [Humanize](Niddy.Core/Text/Humanize.md): sizes, counts, durations, relative times · [StringExtensions](Niddy.Core/Text/StringExtensions.md) |
| Web | [UrlLauncher](Niddy.Core/Web/UrlLauncher.md): open a URL in the browser without a UI framework · [WebContent](Niddy.Core/Web/WebContent.md): download a file or into a folder (atomic, with `DownloadOptions` for overwriting, progress via `DownloadProgress`, a size limit, SHA-256 checking and resuming), read a URL as text, bytes or JSON, or stream it line by line; every method also has a synchronous version |
| Threading | [Debouncer](Niddy.Core/Threading/Debouncer.md) · [Throttler](Niddy.Core/Threading/Throttler.md) · [Guard](Niddy.Core/Threading/Guard.md) · [AtomicOperations](Niddy.Core/Threading/AtomicOperations.md) |

## Niddy.Avalonia

Avalonia app building blocks. Namespaces follow the folders, e.g. `Niddy.Avalonia.PageSystem`; root types are in `Niddy.Avalonia`. In XAML, `xmlns:niddy="https://github.com/vonderborch/Niddy"` covers every public namespace.

| Folder | Pages |
|---|---|
| Hosting | [NiddyApp](Niddy.Avalonia/Hosting/NiddyApp.md): the whole app from one `Configure` method · [NiddyAppOptions](Niddy.Avalonia/Hosting/NiddyAppOptions.md) · [AppShell](Niddy.Avalonia/Hosting/AppShell.md) · [GlobalExceptionHandler](Niddy.Avalonia/Hosting/GlobalExceptionHandler.md) |
| PageSystem | [Page](Niddy.Avalonia/PageSystem/Page.md) · [PageView](Niddy.Avalonia/PageSystem/PageView.md) · [PageControlDataContext](Niddy.Avalonia/PageSystem/PageControlDataContext.md) · [PageId](Niddy.Avalonia/PageSystem/PageId.md) · [PageShortcut](Niddy.Avalonia/PageSystem/PageShortcut.md) · [PageRegistrationAttribute](Niddy.Avalonia/PageSystem/PageRegistrationAttribute.md) · [PageRegistry](Niddy.Avalonia/PageSystem/PageRegistry.md): also `IPageProvider` · [PageRegistration](Niddy.Avalonia/PageSystem/PageRegistration.md) · [PageMenu](Niddy.Avalonia/PageSystem/PageMenu.md) · [PageMenuItem](Niddy.Avalonia/PageSystem/PageMenuItem.md) · [PageIcon](Niddy.Avalonia/PageSystem/PageIcon.md) · [PageIconAttribute](Niddy.Avalonia/PageSystem/PageIconAttribute.md) · [PageIconView](Niddy.Avalonia/PageSystem/PageIconView.md) |
| Dialogs | [Dialog](Niddy.Avalonia/Dialogs/Dialog.md): eleven built-in dialogs, plus `TableColumn<T>` · [DialogBase\<T>](Niddy.Avalonia/Dialogs/DialogBase.md) · [DialogLayout](Niddy.Avalonia/Dialogs/DialogLayout.md) · [DialogOverlayHost](Niddy.Avalonia/Dialogs/DialogOverlayHost.md) |
| Toasts | [Toast](Niddy.Avalonia/Toasts/Toast.md) · [ToastHandle](Niddy.Avalonia/Toasts/ToastHandle.md) · [ToastHost](Niddy.Avalonia/Toasts/ToastHost.md) |
| State | [UiStateStore](Niddy.Avalonia/State/UiStateStore.md) · [WindowMemory](Niddy.Avalonia/State/WindowMemory.md) · [LayoutMemory](Niddy.Avalonia/State/LayoutMemory.md) |
| Converters | [BoolToValueConverter](Niddy.Avalonia/Converters/BoolToValueConverter.md) · [CollectionConverters](Niddy.Avalonia/Converters/CollectionConverters.md) · [EnumToBoolConverter](Niddy.Avalonia/Converters/EnumToBoolConverter.md) · [EnumValuesExtension](Niddy.Avalonia/Converters/EnumValuesExtension.md) · [HumanizeConverters](Niddy.Avalonia/Converters/HumanizeConverters.md) |
| DataContexts | [DataContextBase](Niddy.Avalonia/DataContexts/DataContextBase.md) |
| Layout | [Responsive](Niddy.Avalonia/Layout/Responsive.md) |
| Theme | [ThemeManager](Niddy.Avalonia/Theme/ThemeManager.md) |
| (root) | [FilePicker](Niddy.Avalonia/FilePicker.md) · [ScreenPlacement](Niddy.Avalonia/ScreenPlacement.md) · [UriLauncher](Niddy.Avalonia/UriLauncher.md) · [WindowNotification](Niddy.Avalonia/WindowNotification.md) |

## Niddy.Avalonia.Generators

A source generator, in its own package (which depends on Niddy.Avalonia), that registers `[PageRegistration]` pages at compile time.

- [PageRegistrationGenerator](Niddy.Avalonia.Generators/PageRegistrationGenerator.md)
- [Diagnostics](Niddy.Avalonia.Generators/Diagnostics.md) (NIDDY001–NIDDY008)
