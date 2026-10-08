using Avalonia.Controls;
using Niddy.Avalonia.Hosting;

namespace Niddy.Avalonia.Toasts;

/// <summary>
///     Displays ephemeral, non-blocking toast notifications. They show in the <see cref="ToastHost"/> nearest the
///     source control (see <see cref="ToastHost.FindInVisualTree"/>), or, if there is none, in the running
///     <see cref="NiddyApp"/>'s <see cref="AppShell.Toasts"/>.
/// </summary>
public static class Toast
{
    /// <summary>Default display duration applied when no <c>duration</c> is passed.</summary>
    public static TimeSpan DefaultDuration { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Default display duration for toasts with an action button, which need longer to react to.</summary>
    public static TimeSpan DefaultActionDuration { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>
    ///     Shows a toast notification. Click it to dismiss it early; the countdown pauses while the pointer is over it.
    ///     Showing the same message and type while it's still up doesn't add another toast: the existing one restarts
    ///     its countdown and shows a count (see <see cref="ToastHost.MergeDuplicates"/>).
    /// </summary>
    /// <param name="source">
    ///     A control under a <see cref="ToastHost"/>, e.g. the calling page. Without one, the running
    ///     <see cref="NiddyApp"/>'s host is used, so a page that isn't attached yet can still show toasts.
    /// </param>
    /// <param name="message">The message to display.</param>
    /// <param name="type">Visual style. Defaults to <see cref="ToastType.Info"/>.</param>
    /// <param name="duration">
    ///     How long the toast stays visible. Defaults to <see cref="DefaultDuration"/>, or
    ///     <see cref="DefaultActionDuration"/> with an action. Zero keeps it until dismissed.
    /// </param>
    /// <param name="actionText">The text of a button on the toast, e.g. "Undo" or "Retry".</param>
    /// <param name="action">Runs when the button is clicked, after the toast is dismissed.</param>
    /// <param name="key">
    ///     Toasts with the same key are merged rather than stacked, e.g. "sync-status". Defaults to the type and message
    ///     (when <see cref="ToastHost.MergeDuplicates"/> is on).
    /// </param>
    /// <returns>A handle to update or dismiss the toast.</returns>
    /// <exception cref="InvalidOperationException">
    ///     No <see cref="ToastHost"/> was found around <paramref name="source"/>, and the app isn't a <see cref="NiddyApp"/>
    ///     (or hasn't initialized yet).
    /// </exception>
    public static ToastHandle Show(
        Control source,
        string message,
        ToastType type = ToastType.Info,
        TimeSpan? duration = null,
        string? actionText = null,
        Action? action = null,
        string? key = null
    )
    {
        ToastHost host = FindHost(source);
        bool hasAction = actionText is not null && action is not null;
        TimeSpan actualDuration = duration ?? (hasAction ? DefaultActionDuration : DefaultDuration);
        key ??= host.MergeDuplicates ? $"{type}\n{message}" : null;

        if (key is not null && host.Find(key) is { } existing)
        {
            existing.Item.Message = message;
            existing.Item.Type = type;
            existing.Item.SetAction(actionText, action);
            existing.Item.Count++;
            existing.Item.Restart(actualDuration);
            return existing;
        }

        ToastItem item = new(message, type, actualDuration, actionText, action) { Key = key };
        return host.AddToast(item);
    }

    /// <summary>
    ///     Shows a toast with a progress bar that stays until you call <see cref="ToastHandle.Complete"/> or
    ///     <see cref="ToastHandle.Dismiss"/>. Report progress through the handle, which is an <see cref="IProgress{T}"/>.
    /// </summary>
    /// <param name="source">
    ///     A control under a <see cref="ToastHost"/>, e.g. the calling page. Without one, the running
    ///     <see cref="NiddyApp"/>'s host is used, so a page that isn't attached yet can still show toasts.
    /// </param>
    /// <param name="message">The message to display.</param>
    /// <param name="isIndeterminate">Whether the bar starts indeterminate; reporting progress makes it determinate.</param>
    /// <param name="cancelText">The text of a button that cancels the work, e.g. "Cancel".</param>
    /// <param name="cancel">Runs when the cancel button is clicked, after the toast is dismissed.</param>
    /// <returns>A handle to report progress and complete or dismiss the toast.</returns>
    /// <exception cref="InvalidOperationException">
    ///     No <see cref="ToastHost"/> was found around <paramref name="source"/>, and the app isn't a <see cref="NiddyApp"/>
    ///     (or hasn't initialized yet).
    /// </exception>
    /// <example>
    /// <code>
    /// ToastHandle toast = Toast.ShowProgress(this, "Exporting…");
    /// await ExportAsync(toast);
    /// toast.Complete("Exported");
    /// </code>
    /// </example>
    public static ToastHandle ShowProgress(
        Control source,
        string message,
        bool isIndeterminate = true,
        string? cancelText = null,
        Action? cancel = null
    )
    {
        ToastItem item = new(message, ToastType.Info, TimeSpan.Zero, cancelText, cancel)
        {
            Progress = isIndeterminate ? double.NaN : 0,
        };
        return FindHost(source).AddToast(item);
    }

    /// <summary>The host used when the source control has none. Replaceable by tests, where the app isn't a <see cref="NiddyApp"/>.</summary>
    internal static Func<ToastHost?> AppHost { get; set; } = static () => NiddyApp.Current?.ShellOrNull?.Toasts;

    private static ToastHost FindHost(Control source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return ToastHost.FindInVisualTree(source)
            ?? AppHost()
            ?? throw new InvalidOperationException(
                $"No {nameof(ToastHost)} was found around the control, and there is no {nameof(NiddyApp)} shell to fall back on. "
                + $"Put a {nameof(ToastHost)} (or an {nameof(AppShell)}) around your content."
            );
    }
}
