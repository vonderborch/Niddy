using System.Security.Cryptography;
using System.Text;

namespace Niddy.Core.Security;

/// <summary>
/// Cross-platform <see cref="ISecureStorage" /> fallback that stores tokens in AES-256-CBC
/// encrypted files under a <c>.secure</c> subdirectory of a base directory given to the constructor.
/// The encryption key is derived from user- and machine-specific data via PBKDF2.
/// </summary>
public sealed class EncryptedFileSecureStorage : ISecureStorage
{
    private readonly string _storageDirectory;

    private readonly byte[] _entropy;

    /// <summary>Creates a storage whose files go in a <c>.secure</c> subdirectory of <paramref name="baseDirectory" />, creating it if needed.</summary>
    /// <param name="baseDirectory">Base directory for the application data.</param>
    /// <param name="serviceName">Application identifier used as part of the entropy.</param>
    public EncryptedFileSecureStorage(string baseDirectory, string serviceName = "app")
    {
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            throw new ArgumentException("Base directory cannot be null or empty.", nameof(baseDirectory));
        }
        this._entropy = Encoding.UTF8.GetBytes(serviceName + "_SecureStorage_Entropy");
        this._storageDirectory = Path.Combine(baseDirectory, ".secure");
        Directory.CreateDirectory(this._storageDirectory);
    }

    /// <inheritdoc />
    public string? GetToken(string key)
    {
        string filePath = GetFilePath(key);
        if (!File.Exists(filePath))
        {
            return null;
        }
        try
        {
            return Encoding.UTF8.GetString(Decrypt(File.ReadAllBytes(filePath)));
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetToken(string key, string? value)
    {
        string filePath = GetFilePath(key);
        if (value == null)
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        else
        {
            File.WriteAllBytes(filePath, Encrypt(Encoding.UTF8.GetBytes(value)));
        }
    }

    /// <inheritdoc />
    public bool TokenExists(string key)
    {
        return File.Exists(GetFilePath(key));
    }

    private string GetFilePath(string key)
    {
        string text = key.Replace(" ", "_").Replace(".", "_");
        return Path.Combine(this._storageDirectory, text + ".token");
    }

    private byte[] Encrypt(byte[] data)
    {
        using Aes aes = CreateAes();
        aes.GenerateIV();
        aes.Key = DeriveKey(aes.KeySize / 8);
        using ICryptoTransform transform = aes.CreateEncryptor();
        using MemoryStream memoryStream = new();
        memoryStream.Write(aes.IV, 0, aes.IV.Length);
        using (CryptoStream cryptoStream = new(memoryStream, transform, CryptoStreamMode.Write))
        {
            cryptoStream.Write(data, 0, data.Length);
        }
        return memoryStream.ToArray();
    }

    private byte[] Decrypt(byte[] encryptedData)
    {
        if (encryptedData.Length < 17)
        {
            throw new CryptographicException("Encrypted data is too short.");
        }
        using Aes aes = CreateAes();
        byte[] array = new byte[16];
        Array.Copy(encryptedData, 0, array, 0, 16);
        aes.IV = array;
        aes.Key = DeriveKey(aes.KeySize / 8);
        byte[] array2 = new byte[encryptedData.Length - 16];
        Array.Copy(encryptedData, 16, array2, 0, array2.Length);
        using ICryptoTransform transform = aes.CreateDecryptor();
        using MemoryStream stream = new(array2);
        using CryptoStream cryptoStream = new(stream, transform, CryptoStreamMode.Read);
        using MemoryStream memoryStream = new();
        cryptoStream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private byte[] DeriveKey(int keyBytes)
    {
        string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        byte[] bytes = Encoding.UTF8.GetBytes(folderPath + "_" + Environment.MachineName);
        return Rfc2898DeriveBytes.Pbkdf2(bytes, this._entropy, 10000, HashAlgorithmName.SHA256, keyBytes);
    }

    private static Aes CreateAes()
    {
        Aes aes = Aes.Create();
        aes.KeySize = 256;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }
}
