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

UI-independent helpers. Namespaces are `Niddy.<Folder>`, e.g. `Niddy.Settings`.

| Folder | Pages |
|---|---|
| Helpers | [ByteHelpers](Niddy.Core/Helpers/ByteHelpers.md) · [DisposingHelpers](Niddy.Core/Helpers/DisposingHelpers.md) · [Humanize](Niddy.Core/Helpers/Humanize.md) · [IOHelpers](Niddy.Core/Helpers/IOHelpers.md) · [JsonHelpers](Niddy.Core/Helpers/JsonHelpers.md) · [ListExtensions](Niddy.Core/Helpers/ListExtensions.md) · [PathHelpers](Niddy.Core/Helpers/PathHelpers.md) · [Retry](Niddy.Core/Helpers/Retry.md) · [StreamHelpers](Niddy.Core/Helpers/StreamHelpers.md) · [StringExtensions](Niddy.Core/Helpers/StringExtensions.md) · [UrlHelpers](Niddy.Core/Helpers/UrlHelpers.md) |
| IO | [AppPaths](Niddy.Core/IO/AppPaths.md): per-platform app folders · [AtomicFile](Niddy.Core/IO/AtomicFile.md): crash-safe writes · [DebouncedFileWatcher](Niddy.Core/IO/DebouncedFileWatcher.md) |
| Logging | [NiddyLogging](Niddy.Core/Logging/NiddyLogging.md): one-line file logging · [FileLoggerProvider](Niddy.Core/Logging/FileLoggerProvider.md) · [FileLoggerOptions](Niddy.Core/Logging/FileLoggerOptions.md) |
| Results | [Result](Niddy.Core/Results/Result.md) · [Error](Niddy.Core/Results/Error.md) · [Option](Niddy.Core/Results/Option.md) |
| Security | [ISecureStorage](Niddy.Core/Security/ISecureStorage.md) · [SecureStorageFactory](Niddy.Core/Security/SecureStorageFactory.md): keychain / credential manager / encrypted files · [EncryptedFileSecureStorage](Niddy.Core/Security/EncryptedFileSecureStorage.md) |
| Settings | [SettingsStore\<T>](Niddy.Core/Settings/SettingsStore.md): typed JSON settings with change events and file watching |
| Threading | [Debouncer](Niddy.Core/Threading/Debouncer.md) · [Throttler](Niddy.Core/Threading/Throttler.md) · [Guard](Niddy.Core/Threading/Guard.md) · [AtomicOperations](Niddy.Core/Threading/AtomicOperations.md) |

## Niddy.Avalonia

Avalonia app building blocks. Namespaces are `Niddy.Avalonia.<Folder>`. In XAML, `xmlns:niddy="https://github.com/vonderborch/Niddy"` covers every folder except Utilities.

| Folder | Pages |
|---|---|
| Hosting | [NiddyApp](Niddy.Avalonia/Hosting/NiddyApp.md): the whole app from one `Configure` method · [NiddyAppOptions](Niddy.Avalonia/Hosting/NiddyAppOptions.md) · [AppShell](Niddy.Avalonia/Hosting/AppShell.md) · [GlobalExceptionHandler](Niddy.Avalonia/Hosting/GlobalExceptionHandler.md) |
| PageSystem | [Page](Niddy.Avalonia/PageSystem/Page.md) · [PageView](Niddy.Avalonia/PageSystem/PageView.md) · [PageControlDataContext](Niddy.Avalonia/PageSystem/PageControlDataContext.md) · [PageRegistrationAttribute](Niddy.Avalonia/PageSystem/PageRegistrationAttribute.md) · [PageRegistry](Niddy.Avalonia/PageSystem/PageRegistry.md) · [PageRegistration](Niddy.Avalonia/PageSystem/PageRegistration.md) · [PageId](Niddy.Avalonia/PageSystem/PageId.md) · [PageMenu](Niddy.Avalonia/PageSystem/PageMenu.md) · [PageMenuItem](Niddy.Avalonia/PageSystem/PageMenuItem.md) · [PageIcon](Niddy.Avalonia/PageSystem/PageIcon.md) · [PageIconAttribute](Niddy.Avalonia/PageSystem/PageIconAttribute.md) · [PageIconView](Niddy.Avalonia/PageSystem/PageIconView.md) · [PageShortcut](Niddy.Avalonia/PageSystem/PageShortcut.md) |
| Dialogs | [Dialog](Niddy.Avalonia/Dialogs/Dialog.md): eleven built-in dialogs · [DialogBase\<T>](Niddy.Avalonia/Dialogs/DialogBase.md) · [DialogLayout](Niddy.Avalonia/Dialogs/DialogLayout.md) · [DialogOverlayHost](Niddy.Avalonia/Dialogs/DialogOverlayHost.md) |
| Toast | [Toast](Niddy.Avalonia/Toast/Toast.md) · [ToastHandle](Niddy.Avalonia/Toast/ToastHandle.md) · [ToastHost](Niddy.Avalonia/Toast/ToastHost.md) |
| State | [UiStateStore](Niddy.Avalonia/State/UiStateStore.md) · [WindowMemory](Niddy.Avalonia/State/WindowMemory.md) · [LayoutMemory](Niddy.Avalonia/State/LayoutMemory.md) |
| Converters | [BoolToValueConverter](Niddy.Avalonia/Converters/BoolToValueConverter.md) · [CollectionConverters](Niddy.Avalonia/Converters/CollectionConverters.md) · [EnumToBoolConverter](Niddy.Avalonia/Converters/EnumToBoolConverter.md) · [EnumValuesExtension](Niddy.Avalonia/Converters/EnumValuesExtension.md) · [HumanizeConverters](Niddy.Avalonia/Converters/HumanizeConverters.md) |
| DataContexts | [DataContextBase](Niddy.Avalonia/DataContexts/DataContextBase.md) |
| FilePicker | [FilePicker](Niddy.Avalonia/FilePicker/FilePicker.md) |
| Layout | [Responsive](Niddy.Avalonia/Layout/Responsive.md) |
| Theme | [ThemeManager](Niddy.Avalonia/Theme/ThemeManager.md) |
| Utilities | [UriLauncher](Niddy.Avalonia/Utilities/UriLauncher.md) · [WindowNotification](Niddy.Avalonia/Utilities/WindowNotification.md) |
| (root) | [ScreenPlacement](Niddy.Avalonia/ScreenPlacement.md) |

## Niddy.Avalonia.Generators

A source generator, in its own package (which depends on Niddy.Avalonia), that registers `[PageRegistration]` pages at compile time.

- [PageRegistrationGenerator](Niddy.Avalonia.Generators/PageRegistrationGenerator.md)
- [Diagnostics](Niddy.Avalonia.Generators/Diagnostics.md) (NIDDY001–NIDDY008)
