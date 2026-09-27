using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Niddy.Avalonia.Utilities;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     Shows one <see cref="Page" /> at a time and navigates between them, with a back stack. Its navigation state is
///     <see cref="PageControlDataContext" />, which the pages share and navigation bars can bind to.
///     <para>
///         The first page is <see cref="StartingPage" />, shown when the view is added to a window or page unless a
///         page was already navigated to. Each page's data contexts come from <see cref="AppDataContext" /> and the
///         page itself; see <see cref="Page{T1,T2}" />.
///     </para>
///     <para>
///         A page view inside a page shows that page's child pages (those registered with it as their parent), if it
///         has any; otherwise top-level pages. Set <see cref="ParentPage" /> to choose explicitly.
///     </para>
///     <para>
///         By default the platform back request (Android back button, iOS back gesture, browser back) goes back a
///         page; see <see cref="HandleBackRequests" />. An open overlay dialog is always dismissed first, and with
///         nested page views the most recently attached one handles it first.
///     </para>
///     Usage in AXAML:
///     <code>
///         &lt;pageSystem:PageView StartingPage="{x:Type local:HomePage}" /&gt;
///     </code>
/// </summary>
public partial class PageView : UserControl
{
    /// <summary>Defines the <see cref="StartingPage" /> property.</summary>
    public static readonly StyledProperty<Type?> StartingPageProperty = AvaloniaProperty.Register<PageView, Type>("StartingPage");

    /// <summary>Defines the <see cref="ParentPage" /> property.</summary>
    public static readonly StyledProperty<Type?> ParentPageProperty = AvaloniaProperty.Register<PageView, Type>("ParentPage");

    /// <summary>Defines the <see cref="AppDataContext" /> property.</summary>
    public static readonly StyledProperty<object?> AppDataContextProperty = AvaloniaProperty.Register<PageView, object>("AppDataContext");

    /// <summary>Defines the <see cref="HandleBackRequests" /> property.</summary>
    public static readonly StyledProperty<bool> HandleBackRequestsProperty = AvaloniaProperty.Register<PageView, bool>("HandleBackRequests", defaultValue: true);

    /// <summary>Defines the <see cref="HandleShortcuts" /> property.</summary>
    public static readonly StyledProperty<bool> HandleShortcutsProperty = AvaloniaProperty.Register<PageView, bool>("HandleShortcuts", defaultValue: true);

    /// <summary>Defines the <see cref="PageTransition" /> property.</summary>
    public static readonly StyledProperty<IPageTransition?> PageTransitionProperty = AvaloniaProperty.Register<PageView, IPageTransition>("PageTransition");

    private IDisposable? _backRegistration;

    private TopLevel? _shortcutTopLevel;

    private Page? _containingPage;

    private bool _watchingRegistry;

    /// <summary>
    ///     The type of the first page to show, e.g. <c>{x:Type local:HomePage}</c> in AXAML or <c>HomePage.PageId</c> in
    ///     code. Changing it after a page is shown resets the view: it navigates to the new page and clears the back stack.
    /// </summary>
    public Type? StartingPage
    {
        get
        {
            return GetValue(StartingPageProperty);
        }
        set
        {
            SetValue(StartingPageProperty, value);
        }
    }

    /// <summary>
    ///     The type of the page whose child pages this view shows, or null for top-level pages. If not set, a view
    ///     inside a page uses that page when it has child pages.
    /// </summary>
    public Type? ParentPage
    {
        get
        {
            return GetValue(ParentPageProperty);
        }
        set
        {
            SetValue(ParentPageProperty, value);
        }
    }

    /// <summary>
    ///     The app-wide data context given to every page in this view, separate from the view's own
    ///     <see cref="DataContext" />. Read when a page is first shown. If not set, a view inside a page
    ///     uses the app data context of the view showing that page.
    /// </summary>
    public object? AppDataContext
    {
        get
        {
            return GetValue(AppDataContextProperty);
        }
        set
        {
            SetValue(AppDataContextProperty, value);
        }
    }

    /// <summary>Whether the platform back request goes back a page in this view. Defaults to true.</summary>
    public bool HandleBackRequests
    {
        get
        {
            return GetValue(HandleBackRequestsProperty);
        }
        set
        {
            SetValue(HandleBackRequestsProperty, value);
        }
    }

    /// <summary>
    ///     Whether pressing the <see cref="Shortcut" /> of one of the view's available pages anywhere
    ///     in its window navigates to it. Defaults to true. Keys already handled, e.g. by a text box, are ignored.
    /// </summary>
    public bool HandleShortcuts
    {
        get
        {
            return GetValue(HandleShortcutsProperty);
        }
        set
        {
            SetValue(HandleShortcutsProperty, value);
        }
    }

    /// <summary>
    ///     The animation between pages, e.g. <c>new PageSlide(TimeSpan.FromMilliseconds(200))</c>. It runs in reverse
    ///     when going back. Null (the default) switches instantly.
    /// </summary>
    public IPageTransition? PageTransition
    {
        get
        {
            return GetValue(PageTransitionProperty);
        }
        set
        {
            SetValue(PageTransitionProperty, value);
        }
    }

    /// <summary>The view's navigation state and methods, shared by its pages.</summary>
    public PageControlDataContext PageControlDataContext { get; }

    /// <inheritdoc cref="CanGoBack" />
    public bool CanGoBack => PageControlDataContext.CanGoBack;

    /// <summary>Raised after every navigation in this view, including going back.</summary>
    public event EventHandler<PageNavigationEventArgs>? Navigated;

    /// <summary>Creates an empty page view.</summary>
    public PageView()
    {
        InitializeComponent();
        PageControlDataContext = new PageControlDataContext(Present, ResolveAppDataContext);
        PageControlDataContext.Navigated += (_, e) =>
        {
            Navigated?.Invoke(this, e);
        };
    }

    /// <inheritdoc cref="NavigateTo(PageId,bool,object)" />
    public bool NavigateTo(PageId pageId, bool addToBackStack = true, object? parameter = null)
    {
        return PageControlDataContext.NavigateTo(pageId, addToBackStack, parameter);
    }

    /// <inheritdoc cref="NavigateTo{T}(bool,object)" />
    public bool NavigateTo<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page
    {
        return PageControlDataContext.NavigateTo<TPage>(addToBackStack, parameter);
    }

    /// <inheritdoc cref="Push(Page,bool,object)" />
    public bool Push(Page page, bool addToBackStack = true, object? parameter = null)
    {
        return PageControlDataContext.Push(page, addToBackStack, parameter);
    }

    /// <inheritdoc cref="Push{T}(bool,object)" />
    public bool Push<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page, new()
    {
        return PageControlDataContext.Push<TPage>(addToBackStack, parameter);
    }

    /// <inheritdoc cref="GoBack" />
    public bool GoBack()
    {
        return PageControlDataContext.GoBack();
    }

    /// <inheritdoc cref="ClearBackStack" />
    public void ClearBackStack()
    {
        PageControlDataContext.ClearBackStack();
    }

    /// <inheritdoc />
    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        _containingPage = this.GetLogicalAncestors().OfType<Page>().FirstOrDefault();
        if (!IsSet(ParentPageProperty))
        {
            PageControlDataContext.ParentPage = InferParentPage();
        }
        UpdateChildView();
        if (!_watchingRegistry)
        {
            PageRegistry.PagesChanged += OnPagesChanged;
            _watchingRegistry = true;
        }
        PageControlDataContext.RefreshAvailablePages();
        if (PageControlDataContext.ActivePage == null)
        {
            Type startingPage = StartingPage;
            if ((object)startingPage != null)
            {
                PageControlDataContext.NavigateTo(PageId.Of(startingPage), addToBackStack: false);
            }
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        PageRegistry.PagesChanged -= OnPagesChanged;
        _watchingRegistry = false;
        if (_containingPage?.ChildView == this)
        {
            _containingPage.ChildView = null;
        }
        _containingPage = null;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        TopLevel topLevel = TopLevel.GetTopLevel(this);
        if (topLevel != null)
        {
            _backRegistration = BackNavigation.Register(topLevel, 0, OnBackRequested);
            _shortcutTopLevel = topLevel;
            topLevel.AddHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown, RoutingStrategies.Bubble);
        }
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _backRegistration?.Dispose();
        _backRegistration = null;
        _shortcutTopLevel?.RemoveHandler(InputElement.KeyDownEvent, OnTopLevelKeyDown);
        _shortcutTopLevel = null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ParentPageProperty)
        {
            PageControlDataContext pageControlDataContext = PageControlDataContext;
            Type parentPage = ParentPage;
            pageControlDataContext.ParentPage = (((object)parentPage != null) ? PageId.Of(parentPage) : null);
            UpdateChildView();
        }
        else if (change.Property == PageTransitionProperty)
        {
            PageContent.PageTransition = PageTransition;
        }
        else
        {
            if (!(change.Property == StartingPageProperty) || PageControlDataContext.ActivePage == null)
            {
                return;
            }
            Type startingPage = StartingPage;
            if ((object)startingPage != null)
            {
                PageId pageId = PageId.Of(startingPage);
                if (PageControlDataContext.NavigateTo(pageId, addToBackStack: false) || PageControlDataContext.CurrentPageId == pageId)
                {
                    PageControlDataContext.ClearBackStack();
                }
            }
        }
    }

    private PageId? InferParentPage()
    {
        PageRegistration pageRegistration = _containingPage?.Registration;
        return (pageRegistration != null && pageRegistration.Children.Count > 0) ? pageRegistration.PageId : null;
    }

    /// <summary>Tells the containing page that this view shows its child pages, for <c>NavigateToChild</c>.</summary>
    private void UpdateChildView()
    {
        if (_containingPage != null)
        {
            PageRegistration registration = _containingPage.Registration;
            if (registration != null && PageControlDataContext.ParentPage == registration.PageId)
            {
                _containingPage.ChildView = this;
            }
            else if (_containingPage.ChildView == this)
            {
                _containingPage.ChildView = null;
            }
        }
    }

    private object? ResolveAppDataContext()
    {
        return IsSet(AppDataContextProperty) ? AppDataContext : this.GetLogicalAncestors().OfType<PageView>().FirstOrDefault()?.ResolveAppDataContext();
    }

    private void OnPagesChanged(object? sender, EventArgs e)
    {
        global::Avalonia.Threading.Dispatcher.UIThread.Post(PageControlDataContext.RefreshAvailablePages);
    }

    private void Present(Page page, NavigationMode mode)
    {
        PageContent.IsTransitionReversed = mode == NavigationMode.Back;
        PageContent.Content = page;
    }

    private void OnTopLevelKeyDown(object? sender, KeyEventArgs e)
    {
        if (!e.Handled && HandleShortcut(e.Key, e.KeyModifiers))
        {
            e.Handled = true;
        }
    }

    /// <summary>Navigates to the available page whose shortcut matches, if any.</summary>
    internal bool HandleShortcut(Key key, KeyModifiers modifiers)
    {
        if (!HandleShortcuts || !base.IsEffectivelyVisible)
        {
            return false;
        }
        foreach (PageRegistration availablePage in PageControlDataContext.AvailablePages)
        {
            KeyGesture shortcut = availablePage.Shortcut;
            if ((object)shortcut != null && shortcut.Key == key && shortcut.KeyModifiers == modifiers)
            {
                if (PageControlDataContext.CurrentPageId != availablePage.PageId)
                {
                    NavigateTo(availablePage.PageId);
                }
                return true;
            }
        }
        return false;
    }

    private bool OnBackRequested()
    {
        if (!HandleBackRequests || !CanGoBack)
        {
            return false;
        }
        GoBack();
        return true;
    }
}
