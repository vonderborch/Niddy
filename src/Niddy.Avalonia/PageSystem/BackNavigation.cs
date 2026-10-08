using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Niddy.Avalonia.PageSystem;

/// <summary>
///     Routes the platform back request (Android back button, iOS back gesture, browser back)
///     to the most relevant handler for a <see cref="TopLevel"/>.
///     <para>
///         <see cref="TopLevel.BackRequested"/> runs handlers in subscription order, which would let an
///         outer page view navigate before an inner one or before an open dialog is dismissed. This
///         class subscribes once per <see cref="TopLevel"/> and instead tries handlers by priority,
///         then most-recently registered first (child controls attach after their parents).
///     </para>
/// </summary>
internal static class BackNavigation
{
    /// <summary>Priority for open overlay dialogs, which should always be dismissed first.</summary>
    internal const int DialogPriority = 100;

    /// <summary>Priority for page navigation.</summary>
    internal const int PagePriority = 0;

    private sealed record Registration(int Priority, long Order, Func<bool> Handler);

    private static readonly Dictionary<TopLevel, List<Registration>> Registrations = new();
    private static long _nextOrder;

    /// <summary>
    ///     Registers a back handler for <paramref name="topLevel"/>. The handler returns true if it
    ///     handled the request. Dispose the result to unregister.
    /// </summary>
    internal static IDisposable Register(TopLevel topLevel, int priority, Func<bool> handler)
    {
        if (!Registrations.TryGetValue(topLevel, out List<Registration>? list))
        {
            list = new List<Registration>();
            Registrations[topLevel] = list;
            topLevel.BackRequested += OnBackRequested;
        }

        Registration registration = new(priority, _nextOrder++, handler);
        list.Add(registration);

        return new Unregistration(() =>
        {
            list.Remove(registration);
            if (list.Count == 0 && Registrations.Remove(topLevel))
            {
                topLevel.BackRequested -= OnBackRequested;
            }
        });
    }

    private sealed class Unregistration(Action unregister) : IDisposable
    {
        private Action? _unregister = unregister;

        public void Dispose()
        {
            _unregister?.Invoke();
            _unregister = null;
        }
    }

    private static void OnBackRequested(object? sender, RoutedEventArgs e)
    {
        if (e.Handled || sender is not TopLevel topLevel || !Registrations.TryGetValue(topLevel, out List<Registration>? list))
        {
            return;
        }

        List<Registration> ordered = list
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.Order)
            .ToList();

        foreach (Registration registration in ordered)
        {
            if (registration.Handler())
            {
                e.Handled = true;
                return;
            }
        }
    }
}
