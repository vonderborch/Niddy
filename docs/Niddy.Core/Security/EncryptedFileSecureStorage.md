# EncryptedFileSecureStorage

`Niddy.Core.Security` · Niddy.Core · [source](../../../src/Niddy.Core/Security/EncryptedFileSecureStorage.cs)

An [`ISecureStorage`](ISecureStorage.md) that works everywhere: each value is an AES-256-CBC encrypted file in `<baseDirectory>/.secure`. The key is derived with PBKDF2 from the user's home folder path, the machine name and the service name. That stops the files being read by just copying them somewhere else, but those values aren't secret: anyone who has the files and knows (or guesses) them can derive the key. Treat it as obfuscation backed by file permissions or the OS sandbox, much weaker than an OS keychain. [`SecureStorageFactory`](SecureStorageFactory.md) uses it only where no keychain is available; create it directly only if you specifically want file storage.

## API

| Member | Description |
|---|---|
| `EncryptedFileSecureStorage(string baseDirectory, string serviceName = "app")` | Throws if `baseDirectory` is empty. |
| `GetToken`, `SetToken`, `TokenExists` | See [`ISecureStorage`](ISecureStorage.md). |

## Example

```csharp
using Niddy.Core.Security;

EncryptedFileSecureStorage secrets = new(paths.Data, "MyApp");
secrets.SetToken("api-key", key);
```

## See also

- [SecureStorageFactory](SecureStorageFactory.md)
