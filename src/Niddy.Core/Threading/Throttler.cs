namespace Niddy.Threading;

/// <summary>
/// Runs an action at most once per <see cref="Interval" />, however often <see cref="Invoke(Action)" /> is called. By default
/// the first call runs straight away and the last call in each interval runs when it ends, so the final state is
/// never missed. Typical for progress updates, scroll and pointer-move handling.
/// </summary>
/// <remarks>
/// Leading calls run on the calling thread. Trailing calls run on the <see cref="SynchronizationContext" /> that was
/// current when the throttler was created (e.g. the UI thread), or on the thread pool if there was none. An exception
/// from a trailing call goes to <see cref="Error" />, or is rethrown there if nothing handles it.
/// </remarks>
public sealed class Throttler : IDisposable
{
    private readonly Lock _gate = new();

    private readonly TimeProvider _timeProvider;

    private readonly SynchronizationContext? _context;

    private ITimer? _timer;

    private Action? _pending;

    private bool _inInterval;

    private bool _disposed;

    /// <summary>The shortest time between two runs.</summary>
    public TimeSpan Interval { get; }

    /// <summary>Whether a call outside an interval runs straight away.</summary>
    public bool Leading { get; }

    /// <summary>Whether the last call during an interval runs when it ends.</summary>
    public bool Trailing { get; }

    /// <summary>Raised when a trailing call throws. If nothing handles it, the exception is rethrown where the call ran.</summary>
    public event EventHandler<Exception>? Error;

    /// <summary>Creates a throttler.</summary>
    /// <param name="interval">The shortest time between two runs.</param>
    /// <param name="leading">Whether a call outside an interval runs straight away. Defaults to true.</param>
    /// <param name="trailing">Whether the last call during an interval runs when it ends. Defaults to true.</param>
    /// <param name="timeProvider">The clock to use; defaults to <see cref="System" />.</param>
    /// <param name="captureContext">
    /// Whether to run trailing calls on the current <see cref="SynchronizationContext" />. When false, they run on the thread pool.
    /// </param>
    public Throttler(TimeSpan interval, bool leading = true, bool trailing = true, TimeProvider? timeProvider = null, bool captureContext = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        if (!leading && !trailing)
        {
            throw new ArgumentException("At least one of leading and trailing must be true.", "trailing");
        }
        Interval = interval;
        Leading = leading;
        Trailing = trailing;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _context = (captureContext ? SynchronizationContext.Current : null);
    }

    /// <summary>Runs <paramref name="action" /> now, at the end of the current interval, or not at all, as configured.</summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        bool flag = false;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_inInterval)
            {
                if (Trailing)
                {
                    _pending = action;
                }
            }
            else
            {
                StartInterval();
                if (Leading)
                {
                    flag = true;
                }
                else
                {
                    _pending = action;
                }
            }
        }
        if (flag)
        {
            action();
        }
    }

    /// <summary>Drops a pending trailing call.</summary>
    public void Cancel()
    {
        lock (_gate)
        {
            _pending = null;
        }
    }

    /// <summary>Drops a pending trailing call and stops the timer.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending = null;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void StartInterval()
    {
        _inInterval = true;
        if (_timer == null)
        {
            _timer = _timeProvider.CreateTimer(state =>
            {
                ((Throttler)state).OnIntervalEnded();
            }, this, Interval, Timeout.InfiniteTimeSpan);
        }
        else
        {
            _timer.Change(Interval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnIntervalEnded()
    {
        Action pending;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
            pending = _pending;
            _pending = null;
            if (pending == null)
            {
                _inInterval = false;
            }
            else
            {
                StartInterval();
            }
        }
        if (pending == null)
        {
            return;
        }
        if (_context == null)
        {
            Run(pending);
            return;
        }
        _context.Post(state =>
        {
            var (throttler, action) = ((Throttler, Action))state;
            throttler.Run(action);
        }, (this, pending));
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            ActionFailed.Report(this, Error, exception);
        }
    }
}
