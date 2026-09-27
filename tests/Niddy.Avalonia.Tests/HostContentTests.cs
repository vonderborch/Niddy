using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Toast;

namespace Niddy.Avalonia.Tests;

public class HostContentTests
{
    [AvaloniaFact]
    public void XamlChildContentGoesUnderTheOverlays()
    {
        var window = new HostsWindow();
        window.Show();

        var inner = window.Inner;
        Assert.Same(window.Toasts, ToastHost.FindInVisualTree(inner));
        Assert.Same(window.Dialogs, DialogOverlayHost.FindInVisualTree(inner));
        Assert.True(inner.IsEffectivelyVisible);
    }
}
