using Avalonia.Controls;

namespace Niddy.Avalonia.Tests;

public enum XamlHelpersMode { Light, Dark }

public sealed class XamlHelpersModel
{
    public XamlHelpersMode Mode { get; set; } = XamlHelpersMode.Dark;

    public long Size { get; set; } = 1_500_000;

    public List<string> Items { get; set; } = [];
}

public partial class XamlHelpersWindow : Window
{
    public XamlHelpersWindow() => InitializeComponent();
}
