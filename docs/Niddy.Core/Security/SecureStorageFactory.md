# SecureStorageFactory

`Niddy.Security` · Niddy.Core · [source](../../../src/Niddy.Core/Security/SecureStorageFactory.cs)

Creates the best [`ISecureStorage`](ISecureStorage.md) for the platform the app is running on.

| Platform | Storage |
|---|---|
| macOS | Keychain (Security.framework), as generic passwords under the service name. |
| Windows | Credential Manager (DPAPI-protected). |
| Linux | Secret Service through `secret-tool` (GNOME Keyring, KWallet) if it's installed; otherwise [`EncryptedFileSecureStorage`](EncryptedFileSecureStorage.md). |
| Anything else (iOS, Android, browser, …) | [`EncryptedFileSecureStorage`](EncryptedFileSecureStorage.md). |

## API

| Member | Description |
|---|---|
| `static ISecureStorage Create(string baseDirectory, string serviceName = "app")` | `baseDirectory` is where the encrypted-file fallback keeps its files (in a `.secure` subfolder). `serviceName` identifies the app in the keychain or credential store, so give each app its own. |

## Example

```csharp
using Niddy.IO;
using Niddy.Security;

var paths = AppPaths.For("MyApp");
ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, serviceName: "MyApp");
```

In a [`NiddyApp`](../../Niddy.Avalonia/Hosting/NiddyApp.md), use its `Paths`:

```csharp
var secrets = SecureStorageFactory.Create(NiddyApp.Current!.Paths.Data, "MyApp");
```

## See also

- [ISecureStorage](ISecureStorage.md), [AppPaths](../IO/AppPaths.md)
