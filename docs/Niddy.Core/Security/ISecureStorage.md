# ISecureStorage

`Niddy.Security` · Niddy.Core · [source](../../../src/Niddy.Core/Security/ISecureStorage.cs)

Stores small secrets, such as access tokens, refresh tokens and passwords, by key. Get an implementation for the current platform from [`SecureStorageFactory.Create`](SecureStorageFactory.md), and depend on this interface so tests can swap in an in-memory fake.

## API

| Member | Description |
|---|---|
| `string? GetToken(string key)` | Null if there's nothing stored under `key`. |
| `void SetToken(string key, string? value)` | Stores or replaces the value. **Null deletes it.** |
| `bool TokenExists(string key)` | |

## Examples

```csharp
using Niddy.Security;

ISecureStorage secrets = SecureStorageFactory.Create(paths.Data, "MyApp");

secrets.SetToken("github.refresh", refreshToken);
string? token = secrets.GetToken("github.refresh");
secrets.SetToken("github.refresh", null);           // sign out
```

A test fake:

```csharp
sealed class MemorySecureStorage : ISecureStorage
{
    private readonly Dictionary<string, string> _values = [];
    public string? GetToken(string key) => _values.GetValueOrDefault(key);
    public void SetToken(string key, string? value)
    {
        if (value is null) _values.Remove(key); else _values[key] = value;
    }
    public bool TokenExists(string key) => _values.ContainsKey(key);
}
```

## See also

- [SecureStorageFactory](SecureStorageFactory.md), [EncryptedFileSecureStorage](EncryptedFileSecureStorage.md)
- [examples/oauth-sign-in](../../examples/oauth-sign-in.md)
