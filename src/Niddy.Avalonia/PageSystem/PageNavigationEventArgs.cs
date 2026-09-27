namespace Niddy.Avalonia.PageSystem;

/// <summary>The direction of a navigation.</summary>
public enum NavigationMode
{
    /// <summary>To a page by ID or a pushed page.</summary>
    Forward,

    /// <summary>Back to the previous page.</summary>
    Back,
}

/// <summary>Describes a navigation between two pages.</summary>
public class PageNavigationEventArgs : EventArgs
{
    internal PageNavigationEventArgs(NavigationMode mode, PageRegistration? from, PageRegistration? to, object? parameter)
    {
        Mode = mode;
        From = from;
        To = to;
        Parameter = parameter;
    }

    /// <summary>Whether this is a forward or back navigation.</summary>
    public NavigationMode Mode { get; }

    /// <summary>The page being left, or null if there was none or it was pushed without a registration.</summary>
    public PageRegistration? From { get; }

    /// <summary>The page being shown, or null if it was pushed without a registration.</summary>
    public PageRegistration? To { get; }

    /// <summary>The parameter passed to the navigation, e.g. the item a details page should show. Null when going back.</summary>
    public object? Parameter { get; }
}

/// <summary>
///     Describes a navigation away from a page, which the page can <see cref="Cancel"/>. To ask the user first,
///     cancel it, and call <see cref="Continue"/> once they agree:
///     <code>
///         protected override void OnNavigatingFrom(PageNavigatingFromEventArgs e)
///         {
///             if (!HasUnsavedChanges) return;
///             e.Cancel = true;
///             _ = ConfirmLeavingAsync(e);
///         }
///
///         private async Task ConfirmLeavingAsync(PageNavigatingFromEventArgs e)
///         {
///             if (await Dialog.Confirmation.Open(this, "Discard changes?", "Your changes will be lost."))
///                 e.Continue();
///         }
///     </code>
/// </summary>
public sealed class PageNavigatingFromEventArgs : PageNavigationEventArgs
{
    private readonly Func<bool> _continue;

    internal PageNavigatingFromEventArgs(
        NavigationMode mode,
        PageRegistration? from,
        PageRegistration? to,
        object? parameter,
        Func<bool> @continue
    ) : base(mode, from, to, parameter)
    {
        _continue = @continue;
    }

    /// <summary>Set to true to stay on the page.</summary>
    public bool Cancel { get; set; }

    /// <summary>
    ///     Performs the cancelled navigation after all, without asking the page again. Does nothing if another
    ///     navigation has happened since.
    /// </summary>
    /// <returns>True if the navigation happened.</returns>
    public bool Continue() => _continue();
}
