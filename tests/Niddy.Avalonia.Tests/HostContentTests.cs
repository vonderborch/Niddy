using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Niddy.Avalonia.Dialogs;
using Niddy.Avalonia.Toasts;

namespace Niddy.Avalonia.Tests;

public class HostContentTests
{
    [AvaloniaFact]
    public void XamlChildContentGoesUnderTheOverlays()
    {
        HostsWindow window = new();
        window.Show();

        TextBlock inner = window.Inner;
        Assert.Same(window.Toasts, ToastHost.FindInVisualTree(inner));
        Assert.Same(window.Dialogs, DialogOverlayHost.FindInVisualTree(inner));
        Assert.True(inner.IsEffectivelyVisible);
    }
}
