using System.Runtime.InteropServices;
using System.Text;

namespace Niddy.Core.Security.Platforms;

/// <summary>
/// <see cref="ISecureStorage" /> implementation for every Apple platform (macOS, iOS, iPadOS, tvOS, watchOS and
/// Mac Catalyst) using the Keychain's <c>SecItem</c> API. Each value is a generic password item, with the service
/// name as its service and the key as its account.
/// </summary>
internal sealed class AppleKeychainStorage : ISecureStorage
{
    private const int ErrSecSuccess = 0;

    private const int ErrSecItemNotFound = -25300;

    private const int ErrSecMissingEntitlement = -34018;

    private const string SecurityLibrary = "/System/Library/Frameworks/Security.framework/Security";

    private const string CoreFoundationLibrary = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

    private readonly string _serviceName;

    // Touches no native code, so it can be constructed (e.g. by tests) on any OS.
    internal AppleKeychainStorage(string serviceName)
    {
        this._serviceName = serviceName;
    }

    public string? GetToken(string key)
    {
        using CFHandle service = CFHandle.String(this._serviceName), account = CFHandle.String(key);
        using CFHandle query = CFHandle.Dictionary(
            (Native.SecClass, Native.SecClassGenericPassword),
            (Native.SecAttrService, service),
            (Native.SecAttrAccount, account),
            (Native.SecReturnData, Native.CFBooleanTrue),
            (Native.SecMatchLimit, Native.SecMatchLimitOne));

        int status = SecItemCopyMatching(query, out nint result);
        if (status == ErrSecItemNotFound)
        {
            return null;
        }
        ThrowIfFailed(status, "read");

        using CFHandle data = new(result);
        int length = checked((int)CFDataGetLength(data));
        byte[] bytes = new byte[length];
        if (length > 0)
        {
            Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, length);
        }
        return Encoding.UTF8.GetString(bytes);
    }

    public void SetToken(string key, string? value)
    {
        using CFHandle service = CFHandle.String(this._serviceName), account = CFHandle.String(key);
        using CFHandle query = CFHandle.Dictionary(
            (Native.SecClass, Native.SecClassGenericPassword),
            (Native.SecAttrService, service),
            (Native.SecAttrAccount, account));

        if (value == null)
        {
            int deleted = SecItemDelete(query);
            if (deleted != ErrSecItemNotFound)
            {
                ThrowIfFailed(deleted, "delete");
            }
            return;
        }

        using CFHandle data = CFHandle.Data(Encoding.UTF8.GetBytes(value));
        using CFHandle changes = CFHandle.Dictionary((Native.SecValueData, data));
        int status = SecItemUpdate(query, changes);
        if (status == ErrSecItemNotFound)
        {
            // AfterFirstUnlock so background work can read tokens while the device is locked.
            using CFHandle item = CFHandle.Dictionary(
                (Native.SecClass, Native.SecClassGenericPassword),
                (Native.SecAttrService, service),
                (Native.SecAttrAccount, account),
                (Native.SecValueData, data),
                (Native.SecAttrAccessible, Native.SecAttrAccessibleAfterFirstUnlock));
            status = SecItemAdd(item, IntPtr.Zero);
        }
        ThrowIfFailed(status, "write");
    }

    public bool TokenExists(string key)
    {
        using CFHandle service = CFHandle.String(this._serviceName), account = CFHandle.String(key);
        using CFHandle query = CFHandle.Dictionary(
            (Native.SecClass, Native.SecClassGenericPassword),
            (Native.SecAttrService, service),
            (Native.SecAttrAccount, account),
            (Native.SecMatchLimit, Native.SecMatchLimitOne));

        int status = SecItemCopyMatching(query, out nint result);
        using CFHandle _ = new(result);
        if (status == ErrSecItemNotFound)
        {
            return false;
        }
        ThrowIfFailed(status, "read");
        return true;
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status == ErrSecSuccess)
        {
            return;
        }

        string hint = status == ErrSecMissingEntitlement
            ? " The app is missing the keychain entitlement: sign it with a provisioning profile (iOS, iPadOS, tvOS, watchOS, Mac Catalyst)."
            : string.Empty;
        throw new InvalidOperationException($"Keychain {operation} failed with OSStatus {status}.{hint}");
    }

    /// <summary>A Core Foundation object owned by the caller, released on dispose.</summary>
    private readonly struct CFHandle(nint value) : IDisposable
    {
        public nint Value { get; } = value;

        public static implicit operator nint(CFHandle handle)
        {
            return handle.Value;
        }

        public static CFHandle String(string text)
        {
            return new(CFStringCreateWithCharacters(IntPtr.Zero, text, text.Length));
        }

        public static CFHandle Data(byte[] bytes)
        {
            return new(CFDataCreate(IntPtr.Zero, bytes, bytes.Length));
        }

        public static CFHandle Dictionary(params ReadOnlySpan<(nint Key, nint Value)> pairs)
        {
            nint[] keys = new nint[pairs.Length];
            nint[] values = new nint[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
            {
                keys[i] = pairs[i].Key;
                values[i] = pairs[i].Value;
            }
            return new(CFDictionaryCreate(IntPtr.Zero, keys, values, pairs.Length, Native.KeyCallBacks, Native.ValueCallBacks));
        }

        public void Dispose()
        {
            if (this.Value != IntPtr.Zero)
            {
                CFRelease(this.Value);
            }
        }
    }

    /// <summary>
    /// The Security and Core Foundation constants. They're exported variables, not functions, so they're read with
    /// <see cref="NativeLibrary"/>. Loaded on first use, which only happens on Apple platforms.
    /// </summary>
    private static class Native
    {
        private static readonly nint Security = NativeLibrary.Load(SecurityLibrary);

        private static readonly nint CoreFoundation = NativeLibrary.Load(CoreFoundationLibrary);

        public static readonly nint SecClass = Read(Security, "kSecClass");

        public static readonly nint SecClassGenericPassword = Read(Security, "kSecClassGenericPassword");

        public static readonly nint SecAttrService = Read(Security, "kSecAttrService");

        public static readonly nint SecAttrAccount = Read(Security, "kSecAttrAccount");

        public static readonly nint SecAttrAccessible = Read(Security, "kSecAttrAccessible");

        public static readonly nint SecAttrAccessibleAfterFirstUnlock = Read(Security, "kSecAttrAccessibleAfterFirstUnlock");

        public static readonly nint SecValueData = Read(Security, "kSecValueData");

        public static readonly nint SecReturnData = Read(Security, "kSecReturnData");

        public static readonly nint SecMatchLimit = Read(Security, "kSecMatchLimit");

        public static readonly nint SecMatchLimitOne = Read(Security, "kSecMatchLimitOne");

        public static readonly nint CFBooleanTrue = Read(CoreFoundation, "kCFBooleanTrue");

        // These two are structs, so the dictionary takes their address rather than their value.
        public static readonly nint KeyCallBacks = NativeLibrary.GetExport(CoreFoundation, "kCFTypeDictionaryKeyCallBacks");

        public static readonly nint ValueCallBacks = NativeLibrary.GetExport(CoreFoundation, "kCFTypeDictionaryValueCallBacks");

        private static nint Read(nint library, string name)
        {
            return Marshal.ReadIntPtr(NativeLibrary.GetExport(library, name));
        }
    }

    [DllImport(SecurityLibrary)]
    private static extern int SecItemCopyMatching(nint query, out nint result);

    [DllImport(SecurityLibrary)]
    private static extern int SecItemAdd(nint attributes, nint result);

    [DllImport(SecurityLibrary)]
    private static extern int SecItemUpdate(nint query, nint attributesToUpdate);

    [DllImport(SecurityLibrary)]
    private static extern int SecItemDelete(nint query);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFStringCreateWithCharacters(nint allocator, [MarshalAs(UnmanagedType.LPWStr)] string characters, nint length);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDataCreate(nint allocator, byte[] bytes, nint length);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDataGetLength(nint data);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDataGetBytePtr(nint data);

    [DllImport(CoreFoundationLibrary)]
    private static extern nint CFDictionaryCreate(nint allocator, nint[] keys, nint[] values, nint count, nint keyCallBacks, nint valueCallBacks);

    [DllImport(CoreFoundationLibrary)]
    private static extern void CFRelease(nint value);
}
