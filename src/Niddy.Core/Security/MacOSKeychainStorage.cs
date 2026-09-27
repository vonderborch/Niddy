using System.Runtime.InteropServices;
using System.Text;

namespace Niddy.Security;

/// <summary>
/// <see cref="ISecureStorage" /> implementation for macOS using Keychain Services.
/// </summary>
internal sealed class MacOSKeychainStorage : ISecureStorage
{
    private readonly string _serviceName;

    private readonly string _accountPrefix;

    private const int SecItemNotFound = -25300;

    internal MacOSKeychainStorage(string serviceName)
    {
        _serviceName = serviceName;
        _accountPrefix = "com." + serviceName.ToLowerInvariant() + ".";
    }

    public string? GetToken(string key)
    {
        string text = _accountPrefix + key;
        nint passwordLength;
        nint passwordData;
        nint itemRef;
        int num = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_serviceName.Length, _serviceName, (uint)text.Length, text, out passwordLength, out passwordData, out itemRef);
        if (num == -25300 || num != 0)
        {
            return null;
        }
        if (passwordData == IntPtr.Zero || passwordLength == IntPtr.Zero)
        {
            return null;
        }
        try
        {
            int num2 = ((IntPtr)passwordLength).ToInt32();
            if (num2 == 0)
            {
                return null;
            }
            byte[] array = new byte[num2];
            Marshal.Copy(passwordData, array, 0, num2);
            return Encoding.UTF8.GetString(array);
        }
        finally
        {
            if (passwordData != IntPtr.Zero)
            {
                SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            }
        }
    }

    public void SetToken(string key, string? value)
    {
        string text = _accountPrefix + key;
        nint passwordLength2;
        if (value == null)
        {
            if (SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_serviceName.Length, _serviceName, (uint)text.Length, text, out var _, out passwordLength2, out var itemRef) == 0 && itemRef != IntPtr.Zero)
            {
                SecKeychainItemDelete(itemRef);
            }
            return;
        }
        nint passwordData;
        nint itemRef2;
        int num = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_serviceName.Length, _serviceName, (uint)text.Length, text, out passwordLength2, out passwordData, out itemRef2);
        if (passwordData != IntPtr.Zero)
        {
            SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
        }
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        if (num == 0 && itemRef2 != IntPtr.Zero)
        {
            SecKeychainItemModifyAttributesAndData(itemRef2, IntPtr.Zero, (uint)bytes.Length, bytes);
        }
        else
        {
            SecKeychainAddGenericPassword(IntPtr.Zero, (uint)_serviceName.Length, _serviceName, (uint)text.Length, text, (uint)bytes.Length, bytes, IntPtr.Zero);
        }
    }

    public bool TokenExists(string key)
    {
        string text = _accountPrefix + key;
        nint passwordLength;
        nint passwordData;
        nint itemRef;
        int num = SecKeychainFindGenericPassword(IntPtr.Zero, (uint)_serviceName.Length, _serviceName, (uint)text.Length, text, out passwordLength, out passwordData, out itemRef);
        if (passwordData != IntPtr.Zero)
        {
            SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
        }
        return num == 0;
    }

    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi)]
    private static extern int SecKeychainFindGenericPassword(nint keychainOrArray, uint serviceNameLength, string serviceName, uint accountNameLength, string accountName, out nint passwordLength, out nint passwordData, out nint itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security", CharSet = CharSet.Ansi)]
    private static extern int SecKeychainAddGenericPassword(nint keychain, uint serviceNameLength, string serviceName, uint accountNameLength, string accountName, uint passwordLength, byte[] passwordData, nint itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemModifyAttributesAndData(nint itemRef, nint attrList, uint passwordLength, byte[] passwordData);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemDelete(nint itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(nint attrList, nint data);
}
