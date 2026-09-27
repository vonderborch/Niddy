using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace Niddy.Avalonia.Utilities;

/// <summary>
/// Requests user attention through the dock (macOS bounce) or taskbar (Windows flash).
/// </summary>
public static class WindowNotification
{
    /// <summary>
    /// Bounces the dock icon (macOS) or flashes the taskbar button (Windows).
    /// Falls back to <see cref="Window.Activate"/> on other platforms.
    /// </summary>
    public static void RequestAttention()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
                return;

            var mainWindow = desktop.MainWindow;
            if (mainWindow is null)
                return;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                RequestAttentionMacOS();
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                RequestAttentionWindows(mainWindow);
            else
                mainWindow.Activate();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WindowNotification.RequestAttention failed: {ex.Message}");
        }
    }

    private static void RequestAttentionMacOS()
    {
        try
        {
            var nsApp = objc_getClass("NSApplication");
            var sharedApp = objc_msgSend(nsApp, sel_registerName("sharedApplication"));
            // NSInformationalRequest = 10 (single bounce); use 0 for NSCriticalRequest (repeated bounce)
            objc_msgSend(sharedApp, sel_registerName("requestUserAttention:"), 10);
        }
        catch { /* ignore — non-macOS or sandboxed environment */ }
    }

    private static void RequestAttentionWindows(Window window)
    {
        try
        {
            var handle = window.TryGetPlatformHandle();
            if (handle is null || handle.Handle == IntPtr.Zero)
            {
                window.Activate();
                return;
            }

            var flashInfo = new FLASHWINFO
            {
                cbSize = (uint)Marshal.SizeOf<FLASHWINFO>(),
                hwnd = handle.Handle,
                dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG,
                uCount = 3,
                dwTimeout = 0
            };

            FlashWindowEx(ref flashInfo);
        }
        catch
        {
            window.Activate();
        }
    }

    // macOS ObjC runtime
    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector, int arg);

    // Windows user32
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

    [StructLayout(LayoutKind.Sequential)]
    private struct FLASHWINFO
    {
        public uint cbSize;
        public IntPtr hwnd;
        public uint dwFlags;
        public uint uCount;
        public uint dwTimeout;
    }

    private const uint FLASHW_ALL = 3;
    private const uint FLASHW_TIMERNOFG = 12;
}
