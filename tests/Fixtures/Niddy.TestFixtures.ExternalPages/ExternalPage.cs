using Niddy.Avalonia.PageSystem;

namespace Niddy.TestFixtures.ExternalPages;

/// <summary>A page in a referenced project, registered by that project's generated provider.</summary>
[PageRegistration(DisplayName = "External page")]
public sealed class ExternalPage : Page;
