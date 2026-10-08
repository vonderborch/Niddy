namespace Niddy.Core.Threading;

/// <summary>
/// Runs an action at most once per <see cref="Interval" />, however often <see cref="Invoke(Action)" /> is called. By
/// default the first call runs straight away and the last call in each interval runs when it ends, so the final state
/// is never missed. Typical for progress updates, scroll and pointer-move handling.
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
    /// <param name="timeProvider">The clock to use; defaults to <see cref="TimeProvider.System" />.</param>
    /// <param name="captureContext">
    /// Whether to run trailing calls on the current <see cref="SynchronizationContext" />. When false, they run on the thread pool.
    /// </param>
    public Throttler(TimeSpan interval, bool leading = true, bool trailing = true, TimeProvider? timeProvider = null, bool captureContext = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(interval, TimeSpan.Zero);
        if (!leading && !trailing)
        {
            throw new ArgumentException("At least one of leading and trailing must be true.", nameof(trailing));
        }

        this.Interval = interval;
        this.Leading = leading;
        this.Trailing = trailing;
        this._timeProvider = timeProvider ?? TimeProvider.System;
        this._context = captureContext ? SynchronizationContext.Current : null;
    }

    /// <summary>Runs <paramref name="action" /> now, at the end of the current interval, or not at all, as configured.</summary>
    public void Invoke(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        bool runNow = false;
        lock (this._gate)
        {
            ObjectDisposedException.ThrowIf(this._disposed, this);
            if (this._inInterval)
            {
                if (this.Trailing)
                {
                    this._pending = action;
                }
            }
            else
            {
                StartInterval();
                if (this.Leading)
                {
                    runNow = true;
                }
                else
                {
                    this._pending = action;
                }
            }
        }
        if (runNow)
        {
            action();
        }
    }

    /// <summary>Drops a pending trailing call.</summary>
    public void Cancel()
    {
        lock (this._gate)
        {
            this._pending = null;
        }
    }

    /// <summary>Drops a pending trailing call and stops the timer.</summary>
    public void Dispose()
    {
        lock (this._gate)
        {
            this._disposed = true;
            this._pending = null;
            this._timer?.Dispose();
            this._timer = null;
        }
    }

    private void StartInterval()
    {
        this._inInterval = true;
        if (this._timer == null)
        {
            this._timer = this._timeProvider.CreateTimer(static state => ((Throttler)state!).OnIntervalEnded(), this, this.Interval, Timeout.InfiniteTimeSpan);
        }
        else
        {
            this._timer.Change(this.Interval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnIntervalEnded()
    {
        Action? pending;
        lock (this._gate)
        {
            if (this._disposed)
            {
                return;
            }

            pending = this._pending;
            this._pending = null;
            if (pending == null)
            {
                this._inInterval = false;
                return;
            }
            StartInterval();
        }
        if (this._context == null)
        {
            Run(pending);
            return;
        }
        this._context.Post(static state =>
        {
            (Throttler throttler, Action action) = ((Throttler, Action))state!;
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
            ActionFailed.Report(this, this.Error, exception);
        }
    }
}
