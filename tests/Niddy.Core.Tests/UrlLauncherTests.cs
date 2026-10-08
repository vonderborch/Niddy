using Niddy.Core.SystemInfo;

namespace Niddy.Core.Tests;

// Nothing here opens a URL: that would launch a browser on the test machine.
public class UrlLauncherTests
{
    [Theory]
    [InlineData(OS.Windows, "Shell")]
    [InlineData(OS.MacOS, "Shell")]
    [InlineData(OS.Linux, "Shell")]
    [InlineData(OS.FreeBSD, "Shell")]
    [InlineData(OS.iOS, "UIKit")]
    [InlineData(OS.iPadOS, "UIKit")]
    [InlineData(OS.TvOS, "UIKit")]
    [InlineData(OS.MacCatalyst, "UIKit")]
    [InlineData(OS.Browser, "Browser")]
    [InlineData(OS.Android, "None")]
    [InlineData(OS.WatchOS, "None")]
    [InlineData(OS.Wasi, "None")]
    [InlineData(OS.Unknown, "None")]
    public void OpenerFor_PicksThePlatformsMechanism(OS os, string expected) =>
        Assert.Equal(expected, UrlLauncher.OpenerFor(os).ToString());

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("example.com")]
    [InlineData("/usr/bin/true")]
    [InlineData(@"C:\Windows\notepad.exe")]
    [InlineData(@"\\server\share\app.exe")]
    public void Open_RejectsAnythingButAnAbsoluteUrl(string url) =>
        Assert.ThrowsAny<ArgumentException>(() => UrlLauncher.Open(url));
}
