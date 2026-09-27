using Avalonia.Input;

namespace Niddy.Avalonia.PageSystem;

/// <summary>A page registered with the <see cref="PageRegistry"/>, and its place in the page tree.</summary>
public sealed class PageRegistration
{
    private readonly Func<Page> _create;

    internal PageRegistration(
        PageId pageId,
        string? displayName,
        bool keepAlive,
        IReadOnlyList<PageId> declaredParents,
        IReadOnlyList<PageId> declaredChildren,
        bool? topLevel,
        PageIcon? icon,
        KeyGesture? shortcut,
        Func<Page> create
    )
    {
        PageId = pageId;
        DisplayName = displayName ?? DefaultDisplayName(pageId.PageType);
        KeepAlive = keepAlive;
        DeclaredParents = declaredParents;
        DeclaredChildren = declaredChildren;
        TopLevel = topLevel;
        Icon = icon;
        Shortcut = shortcut;
        _create = create;
    }

    /// <summary>The page's ID.</summary>
    public PageId PageId { get; }

    /// <summary>The page's class.</summary>
    public Type PageType => PageId.PageType;

    /// <summary>A human-readable name, e.g. for a navigation menu. Defaults to the class name without a "Page" suffix.</summary>
    public string DisplayName { get; }

    /// <summary>The page's icon, e.g. for a <see cref="PageMenu"/>, or null if it has none. See <see cref="PageIconAttribute"/>.</summary>
    public PageIcon? Icon { get; }

    /// <summary>
    ///     The keyboard shortcut that navigates to the page, or null if it has none. A <see cref="PageView"/> handles it
    ///     while the page is one of its available pages (see <see cref="PageView.HandleShortcuts"/>), and a
    ///     <see cref="PageMenu"/> shows it in the page's tooltip.
    /// </summary>
    public KeyGesture? Shortcut { get; }

    /// <summary>Whether one instance of the page is kept and reused every time it is navigated to.</summary>
    public bool KeepAlive { get; }

    /// <summary>
    ///     The pages this page is a child of: those it names as parents, and those that name it as a child. Changes
    ///     as pages are registered.
    /// </summary>
    public IReadOnlyList<PageId> Parents => PageRegistry.GetParents(this);

    /// <summary>
    ///     This page's child pages: those it names as children, in that order, then those that name it as a parent,
    ///     in registration order. Only registered pages are included. Changes as pages are registered.
    /// </summary>
    public IReadOnlyList<PageRegistration> Children => PageRegistry.GetPages(PageId);

    /// <summary>
    ///     Whether the page is listed with the top-level pages. Unless set explicitly when registering, it is when the
    ///     page has no parents.
    /// </summary>
    public bool IsTopLevel => TopLevel ?? Parents.Count == 0;

    internal IReadOnlyList<PageId> DeclaredParents { get; }

    internal IReadOnlyList<PageId> DeclaredChildren { get; }

    internal bool? TopLevel { get; }

    internal Page CreatePage() =>
        _create() ?? throw new InvalidOperationException($"The factory for page '{DisplayName}' returned null.");

    /// <summary>Whether registering the page again with these settings is harmless, e.g. explicitly and by the generator.</summary>
    internal bool HasSameSettings(PageRegistration other) =>
        DisplayName == other.DisplayName
        && KeepAlive == other.KeepAlive
        && TopLevel == other.TopLevel
        && Equals(Icon, other.Icon)
        && Equals(Shortcut, other.Shortcut)
        && DeclaredParents.SequenceEqual(other.DeclaredParents)
        && DeclaredChildren.SequenceEqual(other.DeclaredChildren);

    /// <inheritdoc />
    public override string ToString() => DisplayName;

    private static string DefaultDisplayName(Type pageType)
    {
        var name = pageType.Name;
        var tick = name.IndexOf('`');
        if (tick >= 0)
            name = name[..tick];
        return name.Length > "Page".Length && name.EndsWith("Page", StringComparison.Ordinal) ? name[..^"Page".Length] : name;
    }
}
