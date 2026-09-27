using System.Diagnostics;
using System.Text;

namespace Niddy.Security;

/// <summary>
/// <see cref="ISecureStorage" /> implementation for Linux using the Secret Service API
/// via <c>secret-tool</c> (interfaces with GNOME Keyring, KWallet, etc.).
/// </summary>
internal sealed class LinuxSecretServiceStorage : ISecureStorage
{
    private const string ServiceAttribute = "service";

    private const string AccountAttribute = "account";

    private readonly string _serviceValue;

    internal LinuxSecretServiceStorage(string serviceName)
    {
        _serviceValue = serviceName.ToLowerInvariant();
    }

    public string? GetToken(string key)
    {
        try
        {
            string text = RunSecretTool("lookup", "service", _serviceValue, "account", key);
            return string.IsNullOrEmpty(text) ? null : text;
        }
        catch
        {
            return null;
        }
    }

    public void SetToken(string key, string? value)
    {
        if (value == null)
        {
            try
            {
                RunSecretTool("clear", "service", _serviceValue, "account", key);
                return;
            }
            catch
            {
                return;
            }
        }
        RunSecretToolWithInput(value, "store", "--label", _serviceValue + ": " + key, "service", _serviceValue, "account", key);
    }

    public bool TokenExists(string key)
    {
        return GetToken(key) != null;
    }

    /// <summary>
    /// Returns true if <c>secret-tool</c> is available on this system.
    /// </summary>
    public static bool IsAvailable()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo("which", "secret-tool")
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using Process process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }
            process.WaitForExit(1000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string RunSecretTool(params string[] args)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo("secret-tool")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8
        };
        foreach (string item in args)
        {
            processStartInfo.ArgumentList.Add(item);
        }
        using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start secret-tool.");
        string text = process.StandardOutput.ReadToEnd();
        process.WaitForExit(5000);
        return text.TrimEnd(new char[2] { '\n', '\r' });
    }

    private static void RunSecretToolWithInput(string input, params string[] args)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo("secret-tool")
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string item in args)
        {
            processStartInfo.ArgumentList.Add(item);
        }
        using Process process = Process.Start(processStartInfo) ?? throw new InvalidOperationException("Failed to start secret-tool.");
        process.StandardInput.Write(input);
        process.StandardInput.Close();
        process.WaitForExit(5000);
        if (process.ExitCode != 0)
        {
            string value = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"secret-tool exited with code {process.ExitCode}: {value}");
        }
    }
}
