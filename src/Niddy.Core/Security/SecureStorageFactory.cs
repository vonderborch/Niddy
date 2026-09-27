using System.Runtime.InteropServices;

namespace Niddy.Security;

/// <summary>
/// Creates the appropriate <see cref="ISecureStorage"/> implementation for the current platform.
/// </summary>
/// <remarks>
/// Platform dispatch:
/// <list type="bullet">
///   <item>macOS  — Keychain Services (Security.framework)</item>
///   <item>Windows — Credential Manager (DPAPI-protected vault)</item>
///   <item>Linux   — Secret Service via <c>secret-tool</c>; falls back to encrypted file storage</item>
///   <item>Other   — Encrypted file storage (AES-256-CBC, PBKDF2-derived key)</item>
/// </list>
/// </remarks>
public static class SecureStorageFactory
{
    /// <summary>
    /// Creates a secure storage instance for the current platform.
    /// </summary>
    /// <param name="baseDirectory">
    /// Base directory used for the encrypted-file fallback storage and Linux availability check.
    /// </param>
    /// <param name="serviceName">
    /// Application identifier embedded in keychain/credential entries. Defaults to <c>"app"</c>.
    /// </param>
    public static ISecureStorage Create(string baseDirectory, string serviceName = "app")
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return new MacOSKeychainStorage(serviceName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new WindowsCredentialStorage(serviceName);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            if (LinuxSecretServiceStorage.IsAvailable())
                return new LinuxSecretServiceStorage(serviceName);

            return new EncryptedFileSecureStorage(baseDirectory, serviceName);
        }

        return new EncryptedFileSecureStorage(baseDirectory, serviceName);
    }
}
