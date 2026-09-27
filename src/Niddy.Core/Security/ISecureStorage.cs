namespace Niddy.Security;

/// <summary>
/// Platform-agnostic interface for secure storage of sensitive strings such as tokens and passwords.
/// </summary>
public interface ISecureStorage
{
    /// <summary>
    /// Retrieves a value from secure storage. Returns null if the key does not exist.
    /// </summary>
    string? GetToken(string key);

    /// <summary>
    /// Stores a value in secure storage. Pass null to delete an existing entry.
    /// </summary>
    void SetToken(string key, string? value);

    /// <summary>
    /// Returns true if a value exists for the given key.
    /// </summary>
    bool TokenExists(string key);
}
