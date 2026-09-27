using System.Runtime.ExceptionServices;

namespace Niddy.Threading;

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
            lock (_gate)
            {
                return _pending != null;
            }
        }
    }

    /// <summary>Raised when an action throws. If nothing handles it, the exception is rethrown where the action ran.</summary>
    public event EventHandler<Exception>? Error;

    /// <summary>Creates a debouncer.</summary>
    /// <param name="delay">How long calls must stop arriving before the action runs.</param>
    /// <param name="timeProvider">The clock to use; defaults to <see cref="System" />.</param>
    /// <param name="captureContext">
    /// Whether to run actions on the current <see cref="SynchronizationContext" />. When false, they run on the thread pool.
    /// </param>
    public Debouncer(TimeSpan delay, TimeProvider? timeProvider = null, bool captureContext = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(delay, TimeSpan.Zero);
        Delay = delay;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _context = (captureContext ? SynchronizationContext.Current : null);
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
        lock (_gate)
        {
            _pending = null;
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            CancelRunning();
        }
    }

    /// <summary>Runs the pending action now, on the calling thread, if there is one. Useful before closing.</summary>
    public void Flush()
    {
        Func<CancellationToken, Task> func;
        CancellationTokenSource cts;
        lock (_gate)
        {
            func = Take(out cts);
            _timer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
        if (func != null)
        {
            RunAsync(func, cts);
        }
    }

    /// <summary>Drops the pending action and stops the timer. A running action's token is cancelled.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (!_disposed)
            {
                _disposed = true;
                _pending = null;
                CancelRunning();
                _timer?.Dispose();
                _timer = null;
            }
        }
    }

    private void Schedule(Func<CancellationToken, Task> action)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _pending = action;
            CancelRunning();
            if (_timer == null)
            {
                _timer = _timeProvider.CreateTimer(state =>
                {
                    ((Debouncer)state).OnTimer();
                }, this, Delay, Timeout.InfiniteTimeSpan);
            }
            else
            {
                _timer.Change(Delay, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnTimer()
    {
        Func<CancellationToken, Task> func;
        CancellationTokenSource cts;
        lock (_gate)
        {
            func = Take(out cts);
        }
        if (func == null)
        {
            return;
        }
        if (_context == null)
        {
            RunAsync(func, cts);
            return;
        }
        _context.Post(state =>
        {
            var (debouncer, action, cts2) = ((Debouncer, Func<CancellationToken, Task>, CancellationTokenSource))state;
            debouncer.RunAsync(action, cts2);
        }, (this, func, cts));
    }

    private Func<CancellationToken, Task>? Take(out CancellationTokenSource? cts)
    {
        Func<CancellationToken, Task> pending = _pending;
        _pending = null;
        cts = null;
        if (pending == null)
        {
            return null;
        }
        CancelRunning();
        _running = (cts = new CancellationTokenSource());
        return pending;
    }

    private void CancelRunning()
    {
        _running?.Cancel();
        _running = null;
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
        catch (Exception ex2)
        {
            Exception ex3 = ex2;
            ActionFailed.Report(this, Error, ex3);
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
        ExceptionDispatchInfo state = ExceptionDispatchInfo.Capture(exception);
        SynchronizationContext current = SynchronizationContext.Current;
        if (current != null)
        {
            current.Post(obj =>
            {
                ((ExceptionDispatchInfo)obj).Throw();
            }, state);
        }
        else
        {
            ThreadPool.QueueUserWorkItem(exceptionDispatchInfo =>
            {
                exceptionDispatchInfo.Throw();
            }, state, preferLocal: false);
        }
    }
}
