using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Niddy.Avalonia.Hosting;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     The base class of every page shown in a <see cref="PageView"/>. Most pages derive from
///     <see cref="Page{TAppDataContext, TPageDataContext}"/>, which adds typed data contexts; derive from this
///     class directly for a simple page whose <c>DataContext</c> is the app data context.
///     <para>
///         Register a page with <see cref="PageRegistrationAttribute"/> (with the <c>Niddy.Avalonia.Generators</c>
///         package) or <see cref="PageRegistry.Register{TPage}(string?, bool, IEnumerable{Type}?, IEnumerable{Type}?, bool?, PageIcon?, string?)"/>,
///         or show it without registering it with <see cref="PageControlDataContext.Push(Page, bool, object?)"/>.
///         A page's ID is its type: <c>MyPage.PageId</c>.
///     </para>
/// </summary>
public abstract class Page : UserControl
{
    private PageControlDataContext? _pageControl;
    private bool _dataContextsCreated;

    /// <summary>
    ///     The navigation state of the <see cref="PageView"/> showing this page: the current page, the available
    ///     pages, the back stack and navigation methods. Shared by every page in that view.
    /// </summary>
    /// <exception cref="InvalidOperationException">The page hasn't been shown yet.</exception>
    public PageControlDataContext PageControlDataContext => _pageControl ?? throw NotShownYet();

    /// <summary>This page's registration, or null if it was pushed without one.</summary>
    public PageRegistration? Registration { get; private set; }

    /// <summary>Whether the app runs in desktop or mobile mode. See <see cref="NiddyApp.CurrentMode"/>.</summary>
    public AppMode AppMode => NiddyApp.CurrentMode;

    /// <summary>The app data context given to the page view, set before the page is first shown.</summary>
    private protected object? AppDataContextValue { get; private set; }

    /// <summary>Whether the page view showing this page has a previous page to go back to.</summary>
    protected bool CanGoBack => _pageControl?.CanGoBack ?? false;

    /// <inheritdoc cref="PageControlDataContext.NavigateTo(PageId, bool, object?)"/>
    protected bool NavigateTo(PageId pageId, bool addToBackStack = true, object? parameter = null) =>
        PageControlDataContext.NavigateTo(pageId, addToBackStack, parameter);

    /// <inheritdoc cref="PageControlDataContext.NavigateTo{TPage}(bool, object?)"/>
    protected bool NavigateTo<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page =>
        PageControlDataContext.NavigateTo<TPage>(addToBackStack, parameter);

    /// <inheritdoc cref="PageControlDataContext.NavigateToChild{TPage}(bool, object?)"/>
    protected bool NavigateToChild<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page =>
        PageControlDataContext.NavigateToChild<TPage>(addToBackStack, parameter);

    /// <inheritdoc cref="PageControlDataContext.NavigateToSibling{TPage}(bool, object?)"/>
    protected bool NavigateToSibling<TPage>(bool addToBackStack = true, object? parameter = null) where TPage : Page =>
        PageControlDataContext.NavigateToSibling<TPage>(addToBackStack, parameter);

    /// <inheritdoc cref="PageControlDataContext.NavigateToParent(bool, object?)"/>
    protected bool NavigateToParent(bool addToBackStack = true, object? parameter = null) =>
        PageControlDataContext.NavigateToParent(addToBackStack, parameter);

    /// <inheritdoc cref="PageControlDataContext.Push(Page, bool, object?)"/>
    protected bool Push(Page page, bool addToBackStack = true, object? parameter = null) =>
        PageControlDataContext.Push(page, addToBackStack, parameter);

    /// <inheritdoc cref="PageControlDataContext.GoBack"/>
    protected bool GoBack() => PageControlDataContext.GoBack();

    /// <summary>
    ///     Called before the page is left. Set <see cref="PageNavigatingFromEventArgs.Cancel"/> to stay, e.g. to keep
    ///     unsaved changes.
    /// </summary>
    protected virtual void OnNavigatingFrom(PageNavigatingFromEventArgs e) { }

    /// <summary>
    ///     Called after the page is shown, including when it is returned to by going back. The data contexts are
    ///     set by then, and <see cref="PageNavigationEventArgs.Parameter"/> holds any navigation parameter.
    /// </summary>
    protected virtual void OnNavigatedTo(PageNavigationEventArgs e) { }

    /// <summary>Called after another page replaces this one. The page may still be returned to by going back.</summary>
    protected virtual void OnNavigatedFrom(PageNavigationEventArgs e) { }

    /// <summary>
    ///     Called once the page can't be shown again: it was left without adding it to the back stack, going back
    ///     left it, or it dropped off the back stack. Release resources here. Pages kept alive are never closed.
    /// </summary>
    protected virtual void OnClosed() { }

    /// <summary>The page view inside this page that shows its child pages, if any.</summary>
    internal PageView? ChildView { get; set; }

    /// <summary>Creates the page's data contexts the first time it is shown.</summary>
    private protected virtual void CreateDataContexts() => DataContext = AppDataContextValue;

    internal void Attach(PageControlDataContext pageControl, PageRegistration? registration, object? appDataContext)
    {
        _pageControl = pageControl;
        Registration = registration;
        if (_dataContextsCreated)
            return;

        AppDataContextValue = appDataContext;
        CreateDataContexts();
        _dataContextsCreated = true;
    }

    internal void RaiseNavigatingFrom(PageNavigatingFromEventArgs e) => OnNavigatingFrom(e);

    internal void RaiseNavigatedTo(PageNavigationEventArgs e) => OnNavigatedTo(e);

    internal void RaiseNavigatedFrom(PageNavigationEventArgs e) => OnNavigatedFrom(e);

    internal virtual void Close() => OnClosed();

    private protected InvalidOperationException NotShownYet() =>
        new($"{GetType().Name} hasn't been shown in a {nameof(PageView)} yet, so its data contexts aren't set.");
}

/// <summary>
///     A page with typed data contexts:
///     <list type="bullet">
///         <item><see cref="AppDataContext"/>: the app-wide data context shared by every page.</item>
///         <item><see cref="Page.PageControlDataContext"/>: the navigation state of the page view showing the page.</item>
///         <item>
///             <see cref="PageDataContext"/>: the page's own data context, created when the page is first shown and
///             kept for as long as the page. It is also the page's <c>DataContext</c>, so
///             bindings in the page's AXAML bind to it. It is disposed when the page closes if it is
///             <see cref="IDisposable"/>.
///         </item>
///     </list>
///     <code>
///         [PageRegistration(Children = [typeof(GeneralSettingsPage)])]
///         public partial class SettingsPage : Page&lt;MainDataContext, SettingsPageDataContext&gt;
///     </code>
///     In <c>SettingsPage.axaml</c>, use the non-generic <see cref="Page"/> as the root element:
///     <c>&lt;pageSystem:Page x:Class="MyApp.SettingsPage" ...&gt;</c>.
/// </summary>
/// <typeparam name="TAppDataContext">
///     The app data context's type. The page view's app data context (<c>NiddyAppOptions.AppDataContext</c> or
///     <see cref="PageView.AppDataContext"/>) must be one.
/// </typeparam>
/// <typeparam name="TPageDataContext">
///     The page's own data context. Created with its parameterless constructor unless
///     <see cref="CreatePageDataContext"/> is overridden.
/// </typeparam>
public abstract class Page<
    TAppDataContext,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] TPageDataContext
> : Page
    where TAppDataContext : class
    where TPageDataContext : class
{
    private TAppDataContext? _appDataContext;
    private TPageDataContext? _pageDataContext;

    /// <summary>The app-wide data context shared by every page.</summary>
    /// <exception cref="InvalidOperationException">The page hasn't been shown yet.</exception>
    public TAppDataContext AppDataContext => _appDataContext ?? throw NotShownYet();

    /// <summary>The page's own data context, also its <c>DataContext</c>.</summary>
    /// <exception cref="InvalidOperationException">The page hasn't been shown yet.</exception>
    public TPageDataContext PageDataContext => _pageDataContext ?? throw NotShownYet();

    /// <summary>
    ///     Creates the page's data context when the page is first shown. <see cref="AppDataContext"/> and
    ///     <see cref="Page.PageControlDataContext"/> are available by then. Defaults to the parameterless constructor.
    /// </summary>
    protected virtual TPageDataContext CreatePageDataContext() => Activator.CreateInstance<TPageDataContext>();

    private protected override void CreateDataContexts()
    {
        _appDataContext = AppDataContextValue as TAppDataContext ?? throw new InvalidOperationException(
            $"{GetType().Name} needs an app data context of type {typeof(TAppDataContext).Name}, but the page view's is " +
            $"{AppDataContextValue?.GetType().Name ?? "null"}. Set NiddyAppOptions.AppDataContext or PageView.AppDataContext."
        );
        _pageDataContext = CreatePageDataContext();
        DataContext = _pageDataContext;
    }

    internal override void Close()
    {
        base.Close();
        (_pageDataContext as IDisposable)?.Dispose();
    }
}
