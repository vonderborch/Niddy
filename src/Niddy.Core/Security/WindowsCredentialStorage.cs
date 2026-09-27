using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Niddy.Security;

/// <summary>
/// <see cref="ISecureStorage" /> implementation for Windows using the Credential Manager (DPAPI vault).
/// </summary>
internal sealed class WindowsCredentialStorage : ISecureStorage
{
    private enum CredentialType : uint
    {
        Generic = 1u
    }

    private enum CredentialPersistence : uint
    {
        LocalMachine = 2u
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;

        public CredentialType Type;

        public string TargetName;

        public string Comment;

        public FILETIME LastWritten;

        public uint CredentialBlobSize;

        public nint CredentialBlob;

        public CredentialPersistence Persist;

        public uint AttributeCount;

        public nint Attributes;

        public string TargetAlias;

        public string UserName;
    }

    private readonly string _targetPrefix;

    internal WindowsCredentialStorage(string serviceName)
    {
        _targetPrefix = serviceName + ":";
    }

    public string? GetToken(string key)
    {
        if (!CredRead(_targetPrefix + key, CredentialType.Generic, 0u, out var credential))
        {
            return null;
        }
        try
        {
            CREDENTIAL cREDENTIAL = Marshal.PtrToStructure<CREDENTIAL>(credential);
            if (cREDENTIAL.CredentialBlobSize == 0 || cREDENTIAL.CredentialBlob == IntPtr.Zero)
            {
                return null;
            }
            byte[] array = new byte[cREDENTIAL.CredentialBlobSize];
            Marshal.Copy(cREDENTIAL.CredentialBlob, array, 0, (int)cREDENTIAL.CredentialBlobSize);
            return Encoding.UTF8.GetString(array);
        }
        finally
        {
            CredFree(credential);
        }
    }

    public void SetToken(string key, string? value)
    {
        string targetName = _targetPrefix + key;
        if (value == null)
        {
            CredDelete(targetName, CredentialType.Generic, 0u);
            return;
        }
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        CREDENTIAL credential = new CREDENTIAL
        {
            Type = CredentialType.Generic,
            TargetName = targetName,
            CredentialBlobSize = (uint)bytes.Length,
            CredentialBlob = Marshal.AllocHGlobal(bytes.Length),
            Persist = CredentialPersistence.LocalMachine,
            UserName = Environment.UserName
        };
        try
        {
            Marshal.Copy(bytes, 0, credential.CredentialBlob, bytes.Length);
            if (!CredWrite(ref credential, 0u))
            {
                throw new InvalidOperationException($"CredWrite failed. Error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            if (credential.CredentialBlob != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(credential.CredentialBlob);
            }
        }
    }

    public bool TokenExists(string key)
    {
        if (!CredRead(_targetPrefix + key, CredentialType.Generic, 0u, out var credential))
        {
            return false;
        }
        CredFree(credential);
        return true;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string targetName, CredentialType type, uint flags, out nint credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string targetName, CredentialType type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(nint credential);
}
