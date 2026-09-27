using Niddy.Avalonia.PageSystem;

namespace Niddy.TestFixtures.PluginPages;

/// <summary>A page in an assembly the tests load at runtime, like a plugin.</summary>
[PageRegistration]
public sealed class PluginPage : Page;
