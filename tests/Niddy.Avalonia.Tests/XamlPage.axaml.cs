using Niddy.Avalonia.PageSystem;

namespace Niddy.Avalonia.Tests;

public sealed class XamlPageDataContext
{
    public string Name { get; set; } = "from xaml";
}

/// <summary>A page with an AXAML file, whose root is the non-generic Page.</summary>
[PageRegistration(TopLevel = false)]
public sealed partial class XamlPage : Page<TestAppDataContext, XamlPageDataContext>
{
    public XamlPage() => InitializeComponent();
}
