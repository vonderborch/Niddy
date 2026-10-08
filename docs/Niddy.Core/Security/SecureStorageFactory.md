# SecureStorageFactory

`Niddy.Core.Security` · Niddy.Core · [source](../../../src/Niddy.Core/Security/SecureStorageFactory.cs)

Creates the best [`ISecureStorage`](ISecureStorage.md) for the platform the app is running on. Apps and other packages can [register providers](#custom-providers) that are tried before the built-in choice, to replace it, wrap it, or cover platforms it doesn't (such as the Android Keystore).

The built-in choice (`CreateDefault`) goes by [`Platform.Info.OS`](../SystemInfo/Platform.md):

| `OS` | Storage |
|---|---|
| `MacOS`, `iOS`, `iPadOS`, `TvOS`, `WatchOS`, `MacCatalyst` | Keychain, through the `SecItem` API: a generic password item per key, with the service name as its service and the key as its account. Items are readable after the device's first unlock, so background work can use them. |
| `Windows` | Credential Manager (DPAPI-protected). |
| `Linux` | Secret Service through `secret-tool` (GNOME Keyring, KWallet) if it's installed; otherwise [`EncryptedFileSecureStorage`](EncryptedFileSecureStorage.md). |
| `Browser` | **Throws `PlatformNotSupportedException`.** Anything a web page stores can be read by script on the page and by anyone with the browser profile, so there's nowhere to keep secrets. Keep them on a server, or register a provider if you've decided what's acceptable for your app. |
| `Android`, `FreeBSD`, `Wasi`, `Unknown` | [`EncryptedFileSecureStorage`](EncryptedFileSecureStorage.md). |

**The encrypted-file fallback is weak.** Its key is derived from values anyone holding the files can also work out (the home folder path, the machine name and the service name), so it hides secrets from a casual look but isn't a keychain. On Android the app sandbox is what actually protects the files. For real protection there, [register a provider](#custom-providers) that uses the Android Keystore.

On iOS, iPadOS, tvOS, watchOS and Mac Catalyst the Keychain needs a signed app (a provisioning profile with the keychain entitlement); unsigned builds fail with OSStatus `-34018`. On macOS no entitlement is needed: items go in the user's login keychain.

Keychain errors aren't swallowed: a failed read, write or delete throws `InvalidOperationException` with the OSStatus code. A missing key isn't an error (`GetToken` returns null). Credential Manager throws only when a write fails.

## API

| Member | Description |
|---|---|
| `static ISecureStorage Create(string baseDirectory, string serviceName = "app")` | Tries each registered provider, newest first, and returns the first non-null result. If none applies, returns `CreateDefault`. The context's `OS` is `Platform.Info.OS`, so this throws [`UnsupportedOperatingSystemException`](../SystemInfo/Platform.md#unsupportedoperatingsystemexception) if the app [declared its supported OSes](../SystemInfo/Platform.md) and this isn't one of them. `baseDirectory` is where file-backed storage keeps its files (the encrypted-file fallback uses a `.secure` subfolder). `serviceName` identifies the app in the keychain or credential store, so give each app its own. |
| `static ISecureStorage CreateDefault(SecureStorageContext context)` | The built-in choice from the table above for `context.OS`, ignoring registered providers. It doesn't check the OS you pass is the one you're running on. Throws `PlatformNotSupportedException` for `OS.Browser`. Call it from a provider to wrap or fall back to the default; calling `Create` there would run your provider again. |
| `static IDisposable Register(SecureStorageProvider provider)` | Adds a provider that `Create` tries before the built-in choice. The newest registration is tried first. Dispose the returned handle to remove it (disposing twice is fine). Exceptions thrown by a provider aren't caught; return null instead to pass. Register before the first `Create`, since storage already created isn't replaced. |

### SecureStorageProvider

`delegate ISecureStorage? SecureStorageProvider(SecureStorageContext context)`, in the same namespace ([source](../../../src/Niddy.Core/Security/SecureStorageProvider.cs)). It returns the storage to use, or null if it doesn't apply here (wrong OS, missing native service, …), letting the next provider, and finally the built-in choice, try.

### SecureStorageContext

`sealed record SecureStorageContext(string BaseDirectory, string ServiceName, OS OS)`, in the same namespace ([source](../../../src/Niddy.Core/Security/SecureStorageContext.cs)). Holds the arguments passed to `Create`, plus the [`OS`](../SystemInfo/Platform.md#os) it's running on, so providers can switch on `context.OS` instead of detecting it themselves.

## Examples

```csharp
using Niddy.Core.IO;
using Niddy.Core.Security;

AppPaths paths = AppPaths.For("MyApp");
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, serviceName: "MyApp");
```

In a [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md), use its `Paths`:

```csharp
ISecureStorage secrets = SecureStorageFactory.Create(NiddyApp.Current!.Paths.Data, "MyApp");
```

### Custom providers

Use your own storage on one platform and keep the default elsewhere. `IndexedDbTokenStorage` stands in for your own `ISecureStorage` implementation; here it lets the app run in the browser after deciding that's acceptable for its tokens:

```csharp
using Niddy.Core.Security;
using Niddy.Core.SystemInfo;

SecureStorageFactory.Register(context =>
    context.OS == OS.Browser ? new IndexedDbTokenStorage(context.ServiceName) : null);
```

Replace the default everywhere, e.g. with a vault client:

```csharp
SecureStorageFactory.Register(context => new VaultSecureStorage(vaultUrl, context.ServiceName));
```

Wrap the default, e.g. with a decorator that logs access:

```csharp
SecureStorageFactory.Register(context =>
    new LoggingSecureStorage(SecureStorageFactory.CreateDefault(context), logger));
```

A package that adds storage for a platform can register itself when the app starts. Let the app call a registration method, and return null when the platform doesn't match:

```csharp
public static class AndroidSecureStorage
{
    public static IDisposable Register()
    {
        return SecureStorageFactory.Register(context =>
            context.OS == OS.Android ? new AndroidKeystoreStorage(context.ServiceName) : null);
    }
}
```

In tests, register an in-memory fake (see [ISecureStorage](ISecureStorage.md)) and dispose the handle afterwards:

```csharp
using IDisposable registration = SecureStorageFactory.Register(_ => new MemorySecureStorage());
```

## See also

- [ISecureStorage](ISecureStorage.md), [EncryptedFileSecureStorage](EncryptedFileSecureStorage.md), [AppPaths](../IO/AppPaths.md)
- [Platform](../SystemInfo/Platform.md): the OS detection this uses
