using System.Reflection;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     The registered pages and the page tree they form.
///     <para>
///         Pages are registered in one of two ways:
///     </para>
///     <list type="bullet">
///         <item>
///             Automatically, by adding the <c>Niddy.Avalonia.Generators</c> package and tagging pages with
///             <see cref="PageRegistrationAttribute"/>. Pages in referenced projects are included, and so are
///             pages in assemblies loaded at runtime (e.g. plugins), as soon as they load.
///         </item>
///         <item>
///             Explicitly, with <see cref="Register{TPage}(string?, bool, IEnumerable{Type}?, IEnumerable{Type}?, bool?, PageIcon?, string?)"/>,
///             e.g. in <c>NiddyApp.Configure</c>.
///         </item>
///     </list>
///     Both can be used together. A page type is registered once; registering it again with different settings throws,
///     and so does a registration that would make a page its own ancestor.
/// </summary>
public static class PageRegistry
{
    private const string NiddyAvaloniaAssemblyName = "Niddy.Avalonia";

    private static readonly Lock Gate = new();
    private static readonly List<PageRegistration> Pages = new();
    private static readonly Dictionary<PageId, PageRegistration> PagesById = new();
    private static readonly HashSet<Type> IncludedProviders = new();
    private static volatile bool _discovered;
    private static bool _discovering;
    private static bool _watchingAssemblyLoads;

    /// <summary>Raised when pages are registered, possibly on a background thread (e.g. when a plugin loads).</summary>
    public static event EventHandler? PagesChanged;

    /// <summary>Registers <typeparamref name="TPage"/>.</summary>
    /// <param name="displayName">A human-readable name, e.g. for a navigation menu. Defaults to the class name without a "Page" suffix.</param>
    /// <param name="keepAlive">Whether one instance is kept and reused every time the page is navigated to.</param>
    /// <param name="parents">The pages this page is a child of. They don't have to be registered yet.</param>
    /// <param name="children">This page's child pages, in the order a <see cref="PageView"/> lists them.</param>
    /// <param name="topLevel">
    ///     Whether the page is listed with the top-level pages, or null (the default) for when it has no parents.
    /// </param>
    /// <param name="icon">The page's icon, e.g. for a <see cref="PageMenu"/>.</param>
    /// <param name="shortcut">
    ///     A keyboard shortcut that navigates to the page, e.g. <c>"Primary+1"</c>, where <c>Primary</c> is Cmd on macOS
    ///     and Ctrl elsewhere. See <see cref="PageRegistration.Shortcut"/>.
    /// </param>
    /// <exception cref="ArgumentException">A parent or child isn't a page type, or the shortcut isn't a valid key gesture.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The page is already registered with different settings, or the registration would make a page its own ancestor.
    /// </exception>
    public static PageRegistration Register<TPage>(
        string? displayName = null,
        bool keepAlive = false,
        IEnumerable<Type>? parents = null,
        IEnumerable<Type>? children = null,
        bool? topLevel = null,
        PageIcon? icon = null,
        string? shortcut = null
    )
        where TPage : Page, new() => Register(static () => new TPage(), displayName, keepAlive, parents, children, topLevel, icon, shortcut);

    /// <summary>
    ///     Registers <typeparamref name="TPage"/>, created by <paramref name="factory"/>, e.g. to resolve it from a
    ///     dependency injection container.
    /// </summary>
    /// <inheritdoc cref="Register{TPage}(string?, bool, IEnumerable{Type}?, IEnumerable{Type}?, bool?, PageIcon?, string?)"/>
    public static PageRegistration Register<TPage>(
        Func<TPage> factory,
        string? displayName = null,
        bool keepAlive = false,
        IEnumerable<Type>? parents = null,
        IEnumerable<Type>? children = null,
        bool? topLevel = null,
        PageIcon? icon = null,
        string? shortcut = null
    )
        where TPage : Page
    {
        ArgumentNullException.ThrowIfNull(factory);
        var registration = new PageRegistration(
            PageId<TPage>.Instance,
            displayName,
            keepAlive,
            ToPageIds(parents, nameof(parents)),
            ToPageIds(children, nameof(children)),
            topLevel,
            icon,
            shortcut is null ? null : PageShortcut.Parse(shortcut, nameof(shortcut)),
            factory
        );

        lock (Gate)
        {
            if (PagesById.TryGetValue(registration.PageId, out var existing))
            {
                // The same page registered twice with the same settings (e.g. explicitly and by the generator) is harmless.
                if (existing.HasSameSettings(registration))
                    return existing;
                throw new InvalidOperationException(
                    $"{typeof(TPage).FullName} is already registered with different settings. Register each page once."
                );
            }

            PagesById.Add(registration.PageId, registration);
            Pages.Add(registration);

            if (FindCycle(registration.PageId) is { } cycle)
            {
                PagesById.Remove(registration.PageId);
                Pages.Remove(registration);
                throw new InvalidOperationException(
                    $"Registering {typeof(TPage).FullName} would make a page its own ancestor: {string.Join(" > ", cycle)}."
                );
            }
        }

        PagesChanged?.Invoke(null, EventArgs.Empty);
        return registration;
    }

    /// <summary>
    ///     Registers the pages of <typeparamref name="TProvider"/>, once. Generated providers use this to
    ///     include the pages of the projects they reference.
    /// </summary>
    public static void Include<TProvider>() where TProvider : IPageProvider, new()
    {
        if (!Claim(typeof(TProvider)))
            return;

        try
        {
            new TProvider().RegisterPages();
        }
        catch
        {
            Release(typeof(TProvider));
            throw;
        }
    }

    /// <summary>Finds the registration of <paramref name="pageId"/>.</summary>
    /// <returns>The registration, or null if the page isn't registered.</returns>
    public static PageRegistration? Find(PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(pageId);
        EnsureDiscovered();
        lock (Gate)
            return PagesById.GetValueOrDefault(pageId);
    }

    /// <summary>Finds the registration of <typeparamref name="TPage"/>.</summary>
    /// <returns>The registration, or null if the page isn't registered.</returns>
    public static PageRegistration? Find<TPage>() where TPage : Page => Find(PageId<TPage>.Instance);

    /// <summary>
    ///     Returns the child pages of <paramref name="parent"/>: those it names as children, in that order, then those
    ///     that name it as a parent, in registration order. For null, returns the top-level pages in registration order.
    /// </summary>
    /// <param name="parent">The parent page, or null for top-level pages.</param>
    public static IReadOnlyList<PageRegistration> GetPages(PageId? parent = null)
    {
        EnsureDiscovered();
        lock (Gate)
        {
            if (parent is null)
                return Pages.Where(IsTopLevel).ToArray();

            var pages = new List<PageRegistration>();
            if (PagesById.TryGetValue(parent, out var registration))
            {
                foreach (var child in registration.DeclaredChildren)
                {
                    if (PagesById.TryGetValue(child, out var page) && !pages.Contains(page))
                        pages.Add(page);
                }
            }
            pages.AddRange(Pages.Where(p => p.DeclaredParents.Contains(parent) && !pages.Contains(p)));
            return pages;
        }
    }

    /// <summary>Returns every registered page, in registration order.</summary>
    public static IReadOnlyList<PageRegistration> GetAllPages()
    {
        EnsureDiscovered();
        lock (Gate)
            return Pages.ToArray();
    }

    internal static IReadOnlyList<PageId> GetParents(PageRegistration registration)
    {
        EnsureDiscovered();
        lock (Gate)
            return ParentsOf(registration);
    }

    // Callers hold the gate.
    private static List<PageId> ParentsOf(PageRegistration registration)
    {
        var parents = registration.DeclaredParents.ToList();
        parents.AddRange(Pages.Where(p => p.DeclaredChildren.Contains(registration.PageId) && !parents.Contains(p.PageId)).Select(p => p.PageId));
        return parents;
    }

    private static bool IsTopLevel(PageRegistration registration) => registration.TopLevel ?? ParentsOf(registration).Count == 0;

    /// <summary>Finds a path from <paramref name="start"/> through child pages back to itself. Callers hold the gate.</summary>
    private static List<PageId>? FindCycle(PageId start)
    {
        var path = new List<PageId> { start };
        var visited = new HashSet<PageId>();
        return Visit(start) ? path : null;

        bool Visit(PageId page)
        {
            foreach (var child in ChildrenOf(page))
            {
                path.Add(child);
                if (child == start || (visited.Add(child) && Visit(child)))
                    return true;
                path.RemoveAt(path.Count - 1);
            }
            return false;
        }
    }

    /// <summary>Every declared child of <paramref name="page"/>, registered or not. Callers hold the gate.</summary>
    private static IEnumerable<PageId> ChildrenOf(PageId page)
    {
        var declared = PagesById.TryGetValue(page, out var registration) ? registration.DeclaredChildren : [];
        return declared.Concat(Pages.Where(p => p.DeclaredParents.Contains(page)).Select(p => p.PageId)).Distinct();
    }

    private static PageId[] ToPageIds(IEnumerable<Type>? types, string parameterName)
    {
        if (types is null)
            return [];

        var ids = new List<PageId>();
        foreach (var type in types)
        {
            if (type is null || !typeof(Page).IsAssignableFrom(type))
                throw new ArgumentException($"{type?.FullName ?? "null"} isn't a page type.", parameterName);
            var id = PageId.Of(type);
            if (!ids.Contains(id))
                ids.Add(id);
        }
        return ids.ToArray();
    }

    /// <summary>
    ///     Includes the generated pages of every loaded assembly, and of assemblies loaded from now on. Runs once,
    ///     on the first lookup, so pages registered explicitly beforehand are checked for conflicts too.
    /// </summary>
    private static void EnsureDiscovered()
    {
        if (_discovered)
            return;

        lock (Gate)
        {
            // A provider's pages raise PagesChanged, whose handlers may look pages up again on this thread.
            if (_discovered || _discovering)
                return;
            _discovering = true;
        }

        try
        {
            if (!_watchingAssemblyLoads)
            {
                // Subscribe before scanning so an assembly loading meanwhile isn't missed; providers are included once.
                AppDomain.CurrentDomain.AssemblyLoad += (_, e) => IncludeAssembly(e.LoadedAssembly);
                _watchingAssemblyLoads = true;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                IncludeAssembly(assembly);
            _discovered = true;
        }
        finally
        {
            lock (Gate)
                _discovering = false;
        }
    }

    private static void IncludeAssembly(Assembly assembly)
    {
        // Only assemblies built against Niddy.Avalonia can have pages; this skips the framework cheaply.
        if (assembly.IsDynamic || !assembly.GetReferencedAssemblies().Any(a => a.Name == NiddyAvaloniaAssemblyName))
            return;

        foreach (var attribute in assembly.GetCustomAttributes<PageProviderAttribute>())
        {
            if (!Claim(attribute.ProviderType))
                continue;

            try
            {
                var provider = Activator.CreateInstance(attribute.ProviderType) as IPageProvider
                    ?? throw new InvalidOperationException($"{attribute.ProviderType.FullName} isn't an {nameof(IPageProvider)}.");
                provider.RegisterPages();
            }
            catch
            {
                Release(attribute.ProviderType);
                throw;
            }
        }
    }

    private static bool Claim(Type providerType)
    {
        lock (Gate)
            return IncludedProviders.Add(providerType);
    }

    private static void Release(Type providerType)
    {
        lock (Gate)
            IncludedProviders.Remove(providerType);
    }
}
