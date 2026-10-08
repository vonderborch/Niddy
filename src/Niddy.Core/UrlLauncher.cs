using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using Niddy.Core.SystemInfo;

namespace Niddy.Core;

/// <summary>
/// Opens URLs with the platform's default handler, without a UI framework.
/// </summary>
public static partial class UrlLauncher
{
    /// <summary>How <see cref="Open"/> opens a URL on a platform.</summary>
    internal enum UrlOpener
    {
        /// <summary>No way to open URLs without a UI framework.</summary>
        None,

        /// <summary>The desktop shell: <c>ShellExecute</c> on Windows, <c>open</c> on macOS, <c>xdg-open</c> elsewhere.</summary>
        Shell,

        /// <summary>UIKit's <c>UIApplication.openURL</c>.</summary>
        UIKit,

        /// <summary>The browser's <c>window.open</c>.</summary>
        Browser,
    }

    /// <summary>
    /// Opens <paramref name="url"/> with the platform's default handler, usually the web browser. Returns once the
    /// request is made; the handler starts on its own.
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <listheader><term>Platform</term><description>How</description></listheader>
    /// <item><term>Windows, macOS, Linux, FreeBSD</term><description>The shell (<c>ShellExecute</c>, <c>open</c>, <c>xdg-open</c>).</description></item>
    /// <item><term>iOS, iPadOS, tvOS, Mac Catalyst</term><description>
    /// <c>UIApplication.openURL</c>, on the main thread. Custom schemes must be listed under
    /// <c>LSApplicationQueriesSchemes</c> in Info.plist.
    /// </description></item>
    /// <item><term>Browser</term><description>
    /// <c>window.open</c> in a new tab. Browsers block it as a pop-up unless it runs while handling a click or key press.
    /// </description></item>
    /// <item><term>Android, watchOS, WASI</term><description>
    /// Not supported: opening a URL needs the app's UI framework. In an Avalonia app use <c>UriLauncher</c>.
    /// </description></item>
    /// </list>
    /// Whatever handler is registered for the URL's scheme runs, so check the scheme of URLs you didn't write yourself.
    /// </remarks>
    /// <param name="url">An absolute URL, e.g. <c>https://example.com</c> or <c>mailto:me@example.com</c>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> isn't an absolute URL. A file path is rejected unless it's written as a <c>file:</c> URL.
    /// </exception>
    /// <exception cref="PlatformNotSupportedException">This platform can't open URLs without a UI framework.</exception>
    /// <exception cref="UnsupportedOperatingSystemException">
    /// <see cref="Platform.DeclareSupportedOperatingSystems"/> was called and this OS isn't one of those declared.
    /// </exception>
    public static void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        // Uri also accepts plain paths ("/usr/bin/x", "C:\app.exe") as file URLs; only take files written as file: URLs.
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            || (uri.IsFile && !url.TrimStart().StartsWith("file:", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"'{url}' isn't an absolute URL.", nameof(url));
        }

        OS os = Platform.Info.OS;
        switch (OpenerFor(os))
        {
            case UrlOpener.Shell:
                using (Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true, Verb = "open" }))
                {
                }
                return;
            case UrlOpener.UIKit:
                UIKitOpener.Open(uri.AbsoluteUri);
                return;
            case UrlOpener.Browser:
                if (OperatingSystem.IsBrowser())
                {
                    BrowserOpener.Open(uri.AbsoluteUri, "_blank", "noopener,noreferrer");
                    return;
                }
                break;
        }
        throw new PlatformNotSupportedException(
            $"URLs can't be opened on {os} without a UI framework. In an Avalonia app, use UriLauncher.TryLaunchAsync.");
    }

    internal static UrlOpener OpenerFor(OS os) => os switch
    {
        OS.Windows or OS.MacOS or OS.Linux or OS.FreeBSD => UrlOpener.Shell,
        OS.iOS or OS.iPadOS or OS.TvOS or OS.MacCatalyst => UrlOpener.UIKit,
        OS.Browser => UrlOpener.Browser,
        _ => UrlOpener.None,
    };

    [SupportedOSPlatform("browser")]
    private static partial class BrowserOpener
    {
        [JSImport("globalThis.open")]
        public static partial void Open(string url, string target, string features);
    }

    /// <summary>
    /// Calls <c>[UIApplication.sharedApplication openURL:options:completionHandler:]</c> through the Objective-C runtime,
    /// so Niddy.Core needs no Apple bindings. A <c>CFURL</c> and an empty <c>CFDictionary</c> stand in for the
    /// <c>NSURL</c> and <c>NSDictionary</c> (they're toll-free bridged). UIKit must be called on the main thread.
    /// </summary>
    private static unsafe class UIKitOpener
    {
        private const string ObjC = "/usr/lib/libobjc.dylib";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private const string LibSystem = "/usr/lib/libSystem.dylib";

        public static void Open(string url)
        {
            nint text = CFStringCreateWithCharacters(0, url, url.Length);
            nint cfUrl;
            try
            {
                cfUrl = CFURLCreateWithString(0, text, 0);
            }
            finally
            {
                CFRelease(text);
            }
            if (cfUrl == 0)
            {
                throw new ArgumentException($"'{url}' isn't a URL UIKit accepts.", nameof(url));
            }

            if (pthread_main_np() != 0)
            {
                OpenAndRelease(cfUrl);
            }
            else
            {
                nint mainQueue = NativeLibrary.GetExport(NativeLibrary.Load(LibSystem), "_dispatch_main_q");
                dispatch_async_f(mainQueue, cfUrl, &OnMainThread);
            }
        }

        [UnmanagedCallersOnly]
        private static void OnMainThread(nint cfUrl) => OpenAndRelease(cfUrl);

        // Takes ownership of cfUrl. Never throws: from OnMainThread, an exception can't cross back into native code.
        private static void OpenAndRelease(nint cfUrl)
        {
            nint options = 0;
            try
            {
                options = CFDictionaryCreate(0, 0, 0, 0, 0, 0);
                nint application = objc_msgSend(objc_getClass("UIApplication"), sel_registerName("sharedApplication"));
                objc_msgSend(application, sel_registerName("openURL:options:completionHandler:"), cfUrl, options, 0);
            }
            catch
            {
                // openURL reports nothing back either; the URL just doesn't open.
            }
            finally
            {
                if (options != 0)
                {
                    CFRelease(options);
                }
                CFRelease(cfUrl);
            }
        }

        [DllImport(ObjC)]
        private static extern nint objc_getClass([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(ObjC)]
        private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

        [DllImport(ObjC)]
        private static extern nint objc_msgSend(nint receiver, nint selector);

        [DllImport(ObjC)]
        private static extern void objc_msgSend(nint receiver, nint selector, nint url, nint options, nint completionHandler);

        [DllImport(CoreFoundation)]
        private static extern nint CFStringCreateWithCharacters(nint allocator, [MarshalAs(UnmanagedType.LPWStr)] string characters, nint length);

        [DllImport(CoreFoundation)]
        private static extern nint CFURLCreateWithString(nint allocator, nint urlString, nint baseUrl);

        [DllImport(CoreFoundation)]
        private static extern nint CFDictionaryCreate(nint allocator, nint keys, nint values, nint count, nint keyCallBacks, nint valueCallBacks);

        [DllImport(CoreFoundation)]
        private static extern void CFRelease(nint value);

        [DllImport(LibSystem)]
        private static extern int pthread_main_np();

        [DllImport(LibSystem)]
        private static extern void dispatch_async_f(nint queue, nint context, delegate* unmanaged<nint, void> work);
    }
}
