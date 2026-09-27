using Avalonia.Threading;

namespace Niddy.Avalonia.Toast;

/// <summary>
///     A toast that is showing, returned by <see cref="Show(Control,string,ToastType,Nullable{TimeSpan},string,Action,string)" /> and <see cref="ShowProgress(Control,string,bool,string,Action)" />, to update or
///     dismiss it. Its members can be used from any thread.
/// </summary>
public sealed class ToastHandle : IProgress<double>
{
    private readonly ToastItem _item;

    private readonly TaskCompletionSource _dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

    internal ToastItem Item => _item;

    /// <summary>Completes when the toast is dismissed, by timing out, being clicked, its action or <see cref="Dismiss" />.</summary>
    public Task Dismissed => _dismissed.Task;

    /// <summary>Whether the toast has been dismissed.</summary>
    public bool IsDismissed => _dismissed.Task.IsCompleted;

    internal ToastHandle(ToastItem item)
    {
        _item = item;
        item.Dismissed += (_, _) =>
        {
            _dismissed.TrySetResult();
        };
    }

    /// <summary>Changes the message.</summary>
    public void Update(string message, ToastType? type = null)
    {
        OnUIThread(() =>
        {
            _item.Message = message;
            if (type.HasValue)
            {
                ToastType valueOrDefault = type.GetValueOrDefault();
                if (true)
                {
                    _item.Type = valueOrDefault;
                }
            }
        });
    }

    /// <summary>
    ///     Shows progress from 0 to 1 on a progress toast, making it determinate. Pass <see cref="NaN" /> to make
    ///     it indeterminate again.
    /// </summary>
    public void Report(double value)
    {
        OnUIThread(() =>
        {
            _item.Progress = value;
        });
    }

    /// <summary>
    ///     Turns a progress toast into an ordinary one: removes the progress bar and any action, optionally changes the
    ///     message and style, and dismisses it after <paramref name="duration" />.
    /// </summary>
    /// <param name="message">The new message, or null to keep the current one.</param>
    /// <param name="type">The new style. Defaults to <see cref="Success" />.</param>
    /// <param name="duration">How long it stays up now. Defaults to <see cref="DefaultDuration" />.</param>
    public void Complete(string? message = null, ToastType type = ToastType.Success, TimeSpan? duration = null)
    {
        OnUIThread(() =>
        {
            if (message != null)
            {
                _item.Message = message;
            }
            _item.Type = type;
            _item.Progress = null;
            _item.SetAction(null, null);
            _item.Restart(duration ?? Toast.DefaultDuration);
        });
    }

    /// <summary>Removes the toast.</summary>
    public void Dismiss()
    {
        OnUIThread(_item.Dismiss);
    }

    private static void OnUIThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }
}
