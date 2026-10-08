using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;

namespace Niddy.Core.Threading;

/// <summary>
/// Runs an action once calls stop arriving for <see cref="Delay" />: each <see cref="Invoke(Action)" /> restarts the
/// wait and replaces the pending action, so only the last one runs. Typical for search boxes, autosave and
/// resize handling.
/// </summary>
/// <remarks>
/// Actions run on the <see cref="SynchronizationContext" /> that was current when the debouncer was created (e.g.
/// the UI thread), or on the thread pool if there was none. An exception from an action goes to <see cref="Error" />,
/// or is rethrown there if nothing handles it.
/// </remarks>
/// <example>
/// <code>
/// private readonly Debouncer _search = new(TimeSpan.FromMilliseconds(300));
///
/// partial void OnQueryChanged(string value) =&gt;
///     _search.Invoke(async ct =&gt; Results = await SearchAsync(value, ct));
/// </code>
/// </example>
public sealed class Debouncer : IDisposable
{
    private readonly Lock _gate = new();

    private readonly TimeProvider _timeProvider;

    private readonly SynchronizationContext? _context;

    private ITimer? _timer;

    private Func<CancellationToken, Task>? _pending;

    private CancellationTokenSource? _running;

    private bool _disposed;

    /// <summary>How long calls must stop arriving before the action runs.</summary>
    public TimeSpan Delay { get; }

    /// <summary>Whether an action is waiting to run.</summary>
    public bool IsPending
    {
        get
        {
            lock (this._gate)
            {
                return this._pending != null;
            }
        }
    }

    /// <summary>Raised when an action throws. If nothing handles it, the exception is rethrown where the action ran.</summary>
    public event EventHandler<Exception>? Error;

    /// <summary>Creates a debouncer.</summary>
    /// <param name="delay">How long calls must stop arriving before the action runs.</param>
    /// <param name="timeProvider">The clock to use; defaults to <see cref="TimeProvider.System" />.</param>
    /// <param name="captureContext">
    /// Whether to run actions on the current <see cref="SynchronizationContext" />. When false, they run on the thread pool.
    /// </param>
    public Debouncer(TimeSpan delay, TimeProvider? timeProvider = null, bool captureContext = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        this.Delay = delay;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._context = captureContext ? SynchronizationContext.Current : null;
    }

    /// <summary>Schedules <paramref name="action" /> to run after <see cref="Delay" />, replacing any pending action.</summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Schedule(_ =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Schedules <paramref name="action" /> to run after <see cref="Delay" />, replacing any pending action. Its token is
    /// cancelled as soon as another call arrives, so stale work (such as an outdated search) can stop early.
    /// </summary>
    public void Invoke(Func<CancellationToken, Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Schedule(action);
    }

    /// <summary>Drops the pending action, and cancels the token of one that is running.</summary>
    public void Cancel()
    {
        lock (this._gate)
        {
            this._pending = null;
            this._timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            CancelRunning();
        }
    }

    /// <summary>Runs the pending action now, on the calling thread, if there is one. Useful before closing.</summary>
    public void Flush()
    {
        Func<CancellationToken, Task>? action;
        CancellationTokenSource? cts;
        lock (this._gate)
        {
            this._timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            if (!TryTake(out action, out cts))
            {
                return;
            }
        }
        _ = RunAsync(action, cts);
    }

    /// <summary>Drops the pending action and stops the timer. A running action's token is cancelled.</summary>
    public void Dispose()
    {
        lock (this._gate)
        {
            if (this._disposed)
            {
                return;
            }

            this._disposed = true;
            this._pending = null;
            CancelRunning();
            this._timer?.Dispose();
            this._timer = null;
        }
    }

    private void Schedule(Func<CancellationToken, Task> action)
    {
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            this._pending = action;
            CancelRunning();
            if (this._timer == null)
            {
                this._timer = this._timeProvider.CreateTimer(static state => ((Debouncer)state!).OnTimer(), this, this.Delay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                this._timer.Change(this.Delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnTimer()
    {
        Func<CancellationToken, Task>? action;
        CancellationTokenSource? cts;
        lock (this._gate)
        {
            if (!TryTake(out action, out cts))
            {
                return;
            }
        }
        if (this._context == null)
        {
            _ = RunAsync(action, cts);
            return;
        }
        this._context.Post(static state =>
        {
            (Debouncer debouncer, Func<CancellationToken, Task> action, CancellationTokenSource cts) = ((Debouncer, Func<CancellationToken, Task>, CancellationTokenSource))state!;
            _ = debouncer.RunAsync(action, cts);
        }, (this, action, cts));
    }

    /// <summary>Takes the pending action, if any, and starts a new token for it. Call under the lock.</summary>
    private bool TryTake(
        [NotNullWhen(true)] out Func<CancellationToken, Task>? action,
        [NotNullWhen(true)] out CancellationTokenSource? cts
    )
    {
        action = this._pending;
        this._pending = null;
        cts = null;
        if (action == null)
        {
            return false;
        }

        CancelRunning();
        this._running = cts = new CancellationTokenSource();
        return true;
    }

    private void CancelRunning()
    {
        this._running?.Cancel();
        this._running = null;
    }

    private async Task RunAsync(Func<CancellationToken, Task> action, CancellationTokenSource cts)
    {
        try
        {
            await action(cts.Token);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ActionFailed.Report(this, this.Error, ex);
        }
    }
}

/// <summary>Reports an exception from a debounced or throttled action.</summary>
internal static class ActionFailed
{
    public static void Report(object sender, EventHandler<Exception>? handler, Exception exception)
    {
        if (handler != null)
        {
            handler(sender, exception);
            return;
        }
        ExceptionDispatchInfo info = ExceptionDispatchInfo.Capture(exception);
        if (SynchronizationContext.Current is { } context)
        {
            context.Post(static state => ((ExceptionDispatchInfo)state!).Throw(), info);
        }
        else
        {
            ThreadPool.QueueUserWorkItem(static state => state.Throw(), info, preferLocal: false);
        }
    }
}
