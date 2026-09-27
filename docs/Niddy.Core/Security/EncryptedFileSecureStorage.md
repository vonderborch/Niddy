# EncryptedFileSecureStorage

`Niddy.Security` · Niddy.Core · [source](../../../src/Niddy.Core/Security/EncryptedFileSecureStorage.cs)

An [`ISecureStorage`](ISecureStorage.md) that works everywhere: each value is an AES-256-CBC encrypted file in `<baseDirectory>/.secure`. The key is derived with PBKDF2 from user- and machine-specific data plus the service name, so the files can't be read on another machine or by another user, but this is weaker than an OS keychain. [`SecureStorageFactory`](SecureStorageFactory.md) uses it only where no keychain is available; create it directly only if you specifically want file storage.

## API

| Member | Description |
|---|---|
| `EncryptedFileSecureStorage(string baseDirectory, string serviceName = "app")` | Throws if `baseDirectory` is empty. |
| `GetToken`, `SetToken`, `TokenExists` | See [`ISecureStorage`](ISecureStorage.md). |

## Example

```csharp
using Niddy.Security;

var secrets = new EncryptedFileSecureStorage(paths.Data, "MyApp");
secrets.SetToken("api-key", key);
```

## See also

- [SecureStorageFactory](SecureStorageFactory.md)
