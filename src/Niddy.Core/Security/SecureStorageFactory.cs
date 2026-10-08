using Niddy.Core.Security.Platforms;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.Security;

/// <summary>
/// Creates the appropriate <see cref="ISecureStorage"/> implementation for the current platform.
/// </summary>
/// <remarks>
/// Registered providers (see <see cref="Register"/>) are tried first, newest first; the first non-null result wins.
/// If none applies, the built-in choice for <see cref="Platform.Info"/>'s <see cref="Platform.OS"/> (<see cref="CreateDefault"/>) is used:
/// <list type="bullet">
///   <item>macOS, iOS, iPadOS, tvOS, watchOS, Mac Catalyst — Keychain (SecItem API)</item>
///   <item>Windows — Credential Manager (DPAPI-protected vault)</item>
///   <item>Linux   — Secret Service via <c>secret-tool</c>; falls back to encrypted file storage</item>
///   <item>Browser — throws <see cref="PlatformNotSupportedException"/></item>
///   <item>Other (Android, FreeBSD, WASI, ...) — Encrypted file storage (AES-256-CBC, PBKDF2-derived key)</item>
/// </list>
/// </remarks>
public static class SecureStorageFactory
{
    private static readonly Lock Gate = new();

    // Newest first. Replaced, never mutated, so Create can read it without locking.
    private static SecureStorageProvider[] _providers = [];

    /// <summary>
    /// Creates a secure storage instance for the current platform.
    /// </summary>
    /// <param name="baseDirectory">
    /// Base directory used for the encrypted-file fallback storage and Linux availability check.
    /// </param>
    /// <param name="serviceName">
    /// Application identifier embedded in keychain/credential entries. Defaults to <c>"app"</c>.
    /// </param>
    /// <returns>The first registered provider's non-null result, or the <see cref="CreateDefault"/> storage.</returns>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// The app declared its supported operating systems (<see cref="Platform.DeclareSupportedOperatingSystems"/>)
    /// and this isn't one of them.
    /// </exception>
    public static ISecureStorage Create(string baseDirectory, string serviceName = "app")
    {
        SecureStorageContext context = new(baseDirectory, serviceName, Platform.Info.OS);
        foreach (SecureStorageProvider provider in _providers)
        {
            ISecureStorage? storage = provider(context);
            if (storage is not null)
            {
                return storage;
            }
        }

        return CreateDefault(context);
    }

    /// <summary>
    /// Creates the built-in storage for <see cref="SecureStorageContext.OS"/>, ignoring registered providers.
    /// Use it from a provider to wrap or fall back to the default.
    /// </summary>
    /// <param name="context">The base directory, service name and operating system.</param>
    /// <exception cref="PlatformNotSupportedException">
    /// <see cref="SecureStorageContext.OS"/> is <see cref="OS.Browser"/>: a browser has nowhere to keep secrets safely.
    /// </exception>
    public static ISecureStorage CreateDefault(SecureStorageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.OS switch
        {
            OS.MacOS or OS.iOS or OS.iPadOS or OS.TvOS or OS.WatchOS or OS.MacCatalyst => new AppleKeychainStorage(context.ServiceName),
            OS.Browser => throw new PlatformNotSupportedException(
                "There's no secure storage in the browser: anything the app stores there can be read by script on the page "
                + "and by anyone with access to the browser profile. Keep secrets on a server, or register your own provider "
                + "with SecureStorageFactory.Register."),
            OS.Windows => new WindowsCredentialStorage(context.ServiceName),
            OS.Linux when LinuxSecretServiceStorage.IsAvailable() => new LinuxSecretServiceStorage(context.ServiceName),
            _ => new EncryptedFileSecureStorage(context.BaseDirectory, context.ServiceName),
        };
    }

    /// <summary>
    /// Registers a provider that <see cref="Create"/> tries before the built-in storage. Providers are tried newest
    /// first; one that returns null passes to the next. Exceptions from a provider aren't caught.
    /// </summary>
    /// <param name="provider">The provider to add.</param>
    /// <returns>A handle that removes the provider when disposed.</returns>
    public static IDisposable Register(SecureStorageProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        lock (Gate)
        {
            _providers = [provider, .. _providers];
        }

        return new Registration(provider);
    }

    private sealed class Registration(SecureStorageProvider provider) : IDisposable
    {
        private SecureStorageProvider? _provider = provider;

        public void Dispose()
        {
            SecureStorageProvider? provider = Interlocked.Exchange(ref this._provider, null);
            if (provider is null)
            {
                return;
            }

            lock (Gate)
            {
                int index = Array.IndexOf(_providers, provider);
                if (index >= 0)
                {
                    _providers = [.. _providers[..index], .. _providers[(index + 1)..]];
                }
            }
        }
    }
}
