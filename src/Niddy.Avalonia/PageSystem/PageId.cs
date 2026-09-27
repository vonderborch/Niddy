namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     Identifies a page by its type. Every page type has one, available as <c>MyPage.PageId</c> (see
///     <see cref="PageIdExtensions"/>), <see cref="Of{TPage}"/> or <see cref="Of(Type)"/>. Two IDs are equal when
///     their page types are the same.
/// </summary>
public class PageId : IEquatable<PageId>
{
    private protected PageId(Type pageType) => PageType = pageType;

    /// <summary>The page's type.</summary>
    public Type PageType { get; }

    /// <summary>The page's registration, or null if it isn't registered (yet).</summary>
    public PageRegistration? Registration => PageRegistry.Find(this);

    /// <summary>Returns the ID of <typeparamref name="TPage"/>.</summary>
    public static PageId<TPage> Of<TPage>() where TPage : Page => PageId<TPage>.Instance;

    /// <summary>Returns the ID of <paramref name="pageType"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="pageType"/> doesn't derive from <see cref="Page"/>.</exception>
    public static PageId Of(Type pageType)
    {
        ArgumentNullException.ThrowIfNull(pageType);
        if (!typeof(Page).IsAssignableFrom(pageType))
            throw new ArgumentException($"{pageType.FullName} doesn't derive from {typeof(Page).FullName}, so it isn't a page.", nameof(pageType));
        return new PageId(pageType);
    }

    /// <summary>Returns the page's type, e.g. to set <see cref="PageView.StartingPage"/> to <c>HomePage.PageId</c>.</summary>
    public static implicit operator Type(PageId pageId) => pageId.PageType;

    /// <summary>Whether two IDs identify the same page type.</summary>
    public static bool operator ==(PageId? left, PageId? right) => Equals(left, right);

    /// <summary>Whether two IDs identify different page types.</summary>
    public static bool operator !=(PageId? left, PageId? right) => !Equals(left, right);

    /// <inheritdoc />
    public bool Equals(PageId? other) => other is not null && other.PageType == PageType;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PageId other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => PageType.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => PageType.Name;
}

/// <summary>The ID of <typeparamref name="TPage"/>.</summary>
/// <typeparam name="TPage">The page's type.</typeparam>
public sealed class PageId<TPage> : PageId where TPage : Page
{
    private PageId() : base(typeof(TPage)) { }

    /// <summary>The ID of <typeparamref name="TPage"/>, also available as <c>TPage.PageId</c>.</summary>
    public static PageId<TPage> Instance { get; } = new();
}

/// <summary>Adds <c>PageId</c> to every page type.</summary>
public static class PageIdExtensions
{
    extension<TPage>(TPage) where TPage : Page
    {
        /// <summary>
        ///     This page type's ID, e.g. <c>NavigateTo(SettingsPage.PageId)</c>. Inside the page itself, qualify it with
        ///     the class name, since a bare <c>PageId</c> means the <see cref="PageSystem.PageId"/> type.
        /// </summary>
        public static PageId<TPage> PageId => PageId<TPage>.Instance;
    }
}
