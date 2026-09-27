using ReactiveUI;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     The navigation state of a <see cref="PageView"/>: the current page, the pages available to it, the back stack,
///     and methods to navigate. Every page in the view shares it as <see cref="Page.PageControlDataContext"/>, and
///     <see cref="PageView.PageControlDataContext"/> exposes it for navigation bars and menus to bind to.
///     <para>
///         Every navigation respects <see cref="LockToCurrentPage"/> and lets the current page cancel it (see
///         <see cref="Page"/>'s <c>OnNavigatingFrom</c>). Properties change after the new page is shown.
///     </para>
/// </summary>
public sealed class PageControlDataContext : ReactiveObject
{
    // Parent: the page this entry was reached from as a child, or whose view it is shown in, if known.
    private sealed record Entry(Page Page, PageRegistration? Registration, PageId? Parent);

    private readonly Action<Page, NavigationMode> _present;
    private readonly Func<object?> _appDataContext;
    private readonly List<Entry> _backStack = new();
    private readonly Dictionary<PageRegistration, Page> _keptAlive = new();
    private Entry? _current;

    internal PageControlDataContext(Action<Page, NavigationMode> present, Func<object?> appDataContext)
    {
        _present = present;
        _appDataContext = appDataContext;
        AvailablePages = [];
    }

    /// <summary>Raised after every navigation, including going back.</summary>
    public event EventHandler<PageNavigationEventArgs>? Navigated;

    /// <summary>The page whose child pages this view shows, or null for top-level pages.</summary>
    public PageId? ParentPage
    {
        get;
        internal set
        {
            this.RaiseAndSetIfChanged(ref field, value);
            RefreshAvailablePages();
        }
    }

    /// <summary>The child pages of <see cref="ParentPage"/> (or the top-level pages), e.g. for a navigation menu.</summary>
    public IReadOnlyList<PageRegistration> AvailablePages
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>The registration of the page being shown, or null if none is shown or it was pushed without one.</summary>
    public PageRegistration? CurrentPage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     The parent of the page being shown: the page it was reached from as a child page, or its only parent. Null
    ///     if it has none or, with several parents, it was navigated to directly. <see cref="NavigateToParent"/> goes
    ///     here, and a <see cref="PageMenu"/> keeps it highlighted.
    /// </summary>
    public PageId? CurrentParentPage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>The ID of the page being shown, or null if none is shown or it was pushed without a registration.</summary>
    public PageId? CurrentPageId
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>The display name of the page being shown, or null if none is shown or it was pushed without a registration.</summary>
    public string? CurrentPageName
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>The page being shown, or null before the first navigation.</summary>
    public Page? ActivePage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Whether there is a previous page to go back to.</summary>
    public bool CanGoBack
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     While true, every navigation (including going back) is refused, e.g. during a critical operation.
    ///     Navigation methods return false.
    /// </summary>
    public bool LockToCurrentPage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>
    ///     The maximum number of previous pages kept for <see cref="GoBack"/>. Previous pages stay alive (keeping
    ///     their state), so this bounds memory use. Defaults to 20; 0 disables the back stack.
    /// </summary>
    public int MaxBackStackDepth
    {
        get;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            this.RaiseAndSetIfChanged(ref field, value);
            TrimBackStack();
            CanGoBack = _backStack.Count > 0;
        }
    } = 20;

    /// <summary>Navigates to a registered page. Suitable for binding a button's <c>Command</c> to a page's ID.</summary>
    /// <inheritdoc cref="NavigateTo(PageId, bool, object?)"/>
    public bool NavigateTo(PageId pageId) => NavigateTo(pageId, true, null);

    /// <summary>
    ///     Navigates this view to a registered page. Any registered page can be shown; the page tree only decides what
    ///     <see cref="AvailablePages"/> lists and where relative navigation goes.
    /// </summary>
    /// <param name="pageId">The page to navigate to, e.g. <c>SettingsPage.PageId</c>.</param>
    /// <param name="addToBackStack">
    ///     Whether the current page can be returned to with <see cref="GoBack"/>. Pass false for top-level switches
    ///     such as tabs or a sidebar, where back shouldn't retrace every switch.
    /// </param>
    /// <param name="parameter">A value passed to the page's <c>OnNavigatedTo</c>, e.g. the item to show.</param>
    /// <returns>
    ///     True if the page changed; false if navigation is locked, the current page cancelled it, or the page is
    ///     already shown and no parameter was given.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">The page isn't registered.</exception>
    public bool NavigateTo(PageId pageId, bool addToBackStack = true, object? parameter = null)
    {
        var registration = FindRegistered(pageId);
        var parent = ParentPage is { } viewParent && registration.Parents.Contains(viewParent) ? viewParent : null;
        return NavigateToRegistered(registration, parent, addToBackStack, parameter);
    }

    /// <summary>Navigates to a registered page reached from <paramref name="parent"/>, e.g. from a <see cref="PageMenu"/> tree.</summary>
    internal bool NavigateTo(PageId pageId, PageId? parent, bool addToBackStack)
    {
        var registration = FindRegistered(pageId);
        if (parent is not null && !registration.Parents.Contains(parent))
            parent = null;

        // Already showing the page: reaching it from a different parent only changes where it sits in the tree.
        if (_current?.Registration == registration)
        {
            if (parent is null || ParentOf(_current) == parent || LockToCurrentPage)
                return false;
            _current = _current with { Parent = parent };
            CurrentParentPage = parent;
            return true;
        }
        return NavigateToRegistered(registration, parent, addToBackStack, null);
    }

    /// <summary>Navigates this view to <typeparamref name="TPage"/>.</summary>
    /// <inheritdoc cref="NavigateTo(PageId, bool, object?)"/>
    public bool NavigateTo<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page =>
        NavigateTo(PageId<TPage>.Instance, addToBackStack, parameter);

    /// <summary>
    ///     Navigates to <typeparamref name="TPage"/>, a child page of the current page. If the current page contains a
    ///     <see cref="PageView"/> showing its child pages, that view navigates; otherwise this view does, and
    ///     <see cref="NavigateToParent"/> returns to the current page.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="TPage"/> isn't a child page of the current page.</exception>
    /// <inheritdoc cref="NavigateTo(PageId, bool, object?)"/>
    public bool NavigateToChild<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page
    {
        var registration = FindRegistered(PageId<TPage>.Instance);
        if (_current?.Registration is not { } current || !registration.Parents.Contains(current.PageId))
            throw new InvalidOperationException($"{typeof(TPage).Name} isn't a child page of {Describe(_current)}.");

        if (_current.Page.ChildView is { } childView)
            return childView.PageControlDataContext.NavigateTo(registration.PageId, addToBackStack, parameter);
        return NavigateToRegistered(registration, current.PageId, addToBackStack, parameter);
    }

    /// <summary>
    ///     Navigates to <typeparamref name="TPage"/>, a sibling of the current page: a child page of the same parent,
    ///     or another top-level page if the current page is top-level.
    /// </summary>
    /// <exception cref="InvalidOperationException"><typeparamref name="TPage"/> isn't a sibling of the current page.</exception>
    /// <inheritdoc cref="NavigateTo(PageId, bool, object?)"/>
    public bool NavigateToSibling<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page
    {
        var registration = FindRegistered(PageId<TPage>.Instance);
        if (_current?.Registration is not { } current)
            throw new InvalidOperationException($"{typeof(TPage).Name} isn't a sibling of {Describe(_current)}.");

        // Prefer the parent the current page was reached from; otherwise any parent the two pages share.
        var candidates = ParentOf(_current) is { } known ? [known] : current.Parents;
        var parent = candidates.FirstOrDefault(p => registration.Parents.Contains(p));
        if (parent is null && !(current.IsTopLevel && registration.IsTopLevel))
            throw new InvalidOperationException($"{typeof(TPage).Name} isn't a sibling of {current.DisplayName}.");

        return NavigateToRegistered(registration, parent, addToBackStack, parameter);
    }

    /// <summary>
    ///     Navigates this view to the current page's parent: the page it was reached from with
    ///     <see cref="NavigateToChild{TPage}"/>, or its only parent. Does nothing in a view nested in the parent page,
    ///     since the parent is already shown.
    /// </summary>
    /// <returns>
    ///     True if the page changed; false if the parent isn't known, is already shown, navigation is locked, or the
    ///     current page cancelled it.
    /// </returns>
    /// <inheritdoc cref="NavigateTo(PageId, bool, object?)"/>
    public bool NavigateToParent(bool addToBackStack = true, object? parameter = null)
    {
        if (ParentOf(_current) is not { } parent || parent == ParentPage || PageRegistry.Find(parent) is not { } registration)
            return false;

        var grandparent = registration.Parents.Count == 1 ? registration.Parents[0] : null;
        return NavigateToRegistered(registration, grandparent, addToBackStack, parameter);
    }

    /// <summary>Shows <paramref name="page"/> without registering it, e.g. a one-off page with constructor arguments.</summary>
    /// <param name="page">The page to show.</param>
    /// <param name="addToBackStack">Whether the current page can be returned to with <see cref="GoBack"/>.</param>
    /// <param name="parameter">A value passed to the page's <c>OnNavigatedTo</c>.</param>
    /// <returns>True if the page changed; false if navigation is locked or the current page cancelled it.</returns>
    public bool Push(Page page, bool addToBackStack = true, object? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(page);
        return NavigateForward(null, null, () => page, addToBackStack, parameter, askCurrentPage: true);
    }

    /// <summary>Creates a <typeparamref name="TPage"/> and shows it without registering it.</summary>
    /// <inheritdoc cref="Push(Page, bool, object?)"/>
    public bool Push<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page, new() =>
        NavigateForward(null, null, static () => new TPage(), addToBackStack, parameter, askCurrentPage: true);

    /// <summary>Returns to the previous page, restoring the same instance and its state.</summary>
    /// <returns>
    ///     True if the page changed; false if there is no previous page, navigation is locked, or the current page
    ///     cancelled it.
    /// </returns>
    public bool GoBack() => GoBackCore(askCurrentPage: true);

    /// <summary>Forgets all previous pages, so <see cref="CanGoBack"/> becomes false.</summary>
    public void ClearBackStack()
    {
        var removed = _backStack.ToArray();
        _backStack.Clear();
        foreach (var entry in removed)
            CloseIfUnused(entry);
        CanGoBack = false;
    }

    internal void RefreshAvailablePages() => AvailablePages = PageRegistry.GetPages(ParentPage);

    private static PageRegistration FindRegistered(PageId pageId)
    {
        ArgumentNullException.ThrowIfNull(pageId);
        return PageRegistry.Find(pageId) ?? throw new ArgumentOutOfRangeException(
            nameof(pageId),
            $"{pageId.PageType.FullName} isn't registered. Tag it with [PageRegistration] (with the Niddy.Avalonia.Generators " +
            $"package) or register it with {nameof(PageRegistry)}.{nameof(PageRegistry.Register)}."
        );
    }

    /// <summary>The parent of an entry: the one it was reached from, or its only parent.</summary>
    private static PageId? ParentOf(Entry? entry) =>
        entry?.Parent ?? (entry?.Registration?.Parents is [var only] ? only : null);

    private static string Describe(Entry? entry) =>
        entry is null ? "nothing (no page is shown)" : entry.Registration?.DisplayName ?? entry.Page.GetType().Name;

    private bool NavigateToRegistered(PageRegistration registration, PageId? parent, bool addToBackStack, object? parameter)
    {
        if (_current?.Registration == registration && parameter is null)
            return false;
        return NavigateForward(registration, parent, registration.CreatePage, addToBackStack, parameter, askCurrentPage: true);
    }

    private bool NavigateForward(
        PageRegistration? registration,
        PageId? parent,
        Func<Page> create,
        bool addToBackStack,
        object? parameter,
        bool askCurrentPage
    )
    {
        if (LockToCurrentPage)
            return false;

        var previous = _current;
        if (askCurrentPage && previous is not null)
        {
            var leaving = new PageNavigatingFromEventArgs(
                NavigationMode.Forward,
                previous.Registration,
                registration,
                parameter,
                () => _current == previous && NavigateForward(registration, parent, create, addToBackStack, parameter, askCurrentPage: false)
            );
            previous.Page.RaiseNavigatingFrom(leaving);
            if (leaving.Cancel)
                return false;
        }

        // Create and attach the page before changing anything, so a failure leaves the current page in place.
        var keepAlive = registration?.KeepAlive == true;
        var page = keepAlive && _keptAlive.TryGetValue(registration!, out var kept) ? kept : create();
        page.Attach(this, registration, _appDataContext());
        if (keepAlive)
            _keptAlive[registration!] = page;

        var current = new Entry(page, registration, parent);
        _current = current;
        Entry? dropped = null;
        if (previous is not null)
        {
            if (addToBackStack && MaxBackStackDepth > 0)
            {
                _backStack.Add(previous);
                TrimBackStack();
            }
            else
            {
                dropped = previous;
            }
        }

        var args = new PageNavigationEventArgs(NavigationMode.Forward, previous?.Registration, registration, parameter);
        Complete(previous, dropped, current, args);
        return true;
    }

    private bool GoBackCore(bool askCurrentPage)
    {
        if (_backStack.Count == 0 || LockToCurrentPage || _current is not { } previous)
            return false;

        var target = _backStack[^1];
        if (askCurrentPage)
        {
            var leaving = new PageNavigatingFromEventArgs(
                NavigationMode.Back,
                previous.Registration,
                target.Registration,
                null,
                () => _current == previous && GoBackCore(askCurrentPage: false)
            );
            previous.Page.RaiseNavigatingFrom(leaving);
            if (leaving.Cancel)
                return false;
        }

        _backStack.RemoveAt(_backStack.Count - 1);
        _current = target;
        target.Page.Attach(this, target.Registration, _appDataContext());

        var args = new PageNavigationEventArgs(NavigationMode.Back, previous.Registration, target.Registration, null);
        Complete(previous, previous, target, args);
        return true;
    }

    /// <summary>Shows the new page, then notifies the old page, bindings and the new page, in that order.</summary>
    private void Complete(Entry? previous, Entry? dropped, Entry current, PageNavigationEventArgs args)
    {
        _present(current.Page, args.Mode);
        previous?.Page.RaiseNavigatedFrom(args);
        if (dropped is not null)
            CloseIfUnused(dropped);

        CurrentParentPage = ParentOf(current);
        CurrentPage = current.Registration;
        CurrentPageId = current.Registration?.PageId;
        CurrentPageName = current.Registration?.DisplayName;
        ActivePage = current.Page;
        CanGoBack = _backStack.Count > 0;

        current.Page.RaiseNavigatedTo(args);

        // The page may have navigated again from OnNavigatedTo; that navigation raised its own event.
        if (_current == current)
            Navigated?.Invoke(this, args);
    }

    /// <summary>Drops the oldest back stack entries beyond <see cref="MaxBackStackDepth"/>, closing their pages.</summary>
    private void TrimBackStack()
    {
        while (_backStack.Count > MaxBackStackDepth)
        {
            var dropped = _backStack[0];
            _backStack.RemoveAt(0);
            CloseIfUnused(dropped);
        }
    }

    private void CloseIfUnused(Entry entry)
    {
        var page = entry.Page;
        if (entry.Registration?.KeepAlive == true || _current?.Page == page || _backStack.Any(e => e.Page == page))
            return;
        page.Close();
    }
}
