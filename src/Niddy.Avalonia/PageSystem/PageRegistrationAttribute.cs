namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     Registers a <see cref="Page"/> and places it in the page tree. The registration is read at compile time by the
///     <c>Niddy.Avalonia.Generators</c> package, which registers every attributed page automatically, including pages
///     in other projects and in assemblies loaded at runtime. Without that package the attribute does nothing; register
///     pages with <see cref="PageRegistry.Register{TPage}(string?, bool, IEnumerable{Type}?, IEnumerable{Type}?, bool?, PageIcon?, string?)"/>
///     instead.
///     <para>
///         The tree can be declared from either end: a parent lists its <see cref="Children"/>, or a child lists its
///         <see cref="Parents"/> (e.g. a plugin page adding itself under a page of the app). Both can be combined, and
///         a page can have several parents.
///     </para>
/// </summary>
/// <example>
///     <code>
///         [PageRegistration(DisplayName = "Settings", Children = [typeof(GeneralSettingsPage), typeof(AdvancedSettingsPage)])]
///         public partial class SettingsPage : Page&lt;MainDataContext, SettingsPageDataContext&gt;
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PageRegistrationAttribute : Attribute
{
    /// <summary>A human-readable name, e.g. for a navigation menu. Defaults to the class name without a "Page" suffix.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    ///     Whether one instance of the page is kept and reused every time it is navigated to, keeping its
    ///     state. Useful for tabs and sidebar sections. Defaults to false: each navigation creates a new page.
    /// </summary>
    public bool KeepAlive { get; set; }

    /// <summary>The pages this page is a child of. They don't have to be registered yet.</summary>
    public Type[]? Parents { get; set; }

    /// <summary>
    ///     This page's child pages, in the order a <see cref="PageView"/> lists them. Each must be registered itself to
    ///     appear.
    /// </summary>
    public Type[]? Children { get; set; }

    /// <summary>
    ///     Whether the page is listed with the top-level pages. If not set, it is when the page has no parents. Set it
    ///     to true to also list a child page at the top level, or false to hide a parentless page from menus.
    /// </summary>
    public bool TopLevel { get; set; }

    /// <summary>
    ///     A keyboard shortcut that navigates to the page while it's listed in a <see cref="PageView"/>, e.g.
    ///     <c>"Primary+1"</c> or <c>"Ctrl+Shift+S"</c>. <c>Primary</c> is Cmd on macOS and Ctrl elsewhere. See
    ///     <see cref="PageRegistration.Shortcut"/>.
    /// </summary>
    public string? Shortcut { get; set; }
}
