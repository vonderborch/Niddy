namespace Niddy.Core.Logging.Batching;

/// <summary>
/// Runs tasks one at a time on a single background thread. A writer loop started on it stays on it across awaits, since
/// awaiting captures the current scheduler.
/// </summary>
internal sealed class LogWriterScheduler : TaskScheduler
{
    private static readonly Lock SharedGate = new();

    private static LogWriterScheduler? _shared;

    private static int _sharedUsers;

    // A plain object rather than a Lock: the thread sleeps in Monitor.Wait, which blocks rather than spinning.
    private readonly object _gate = new();

    private readonly Queue<Task> _tasks = new();

    private readonly Thread _thread;

    private bool _completed;

    /// <summary>The scheduler's thread is started here; throws <see cref="PlatformNotSupportedException"/> where threads can't be.</summary>
    /// <param name="name">The thread's name, shown in debuggers and dumps.</param>
    public LogWriterScheduler(string name)
    {
        if (OperatingSystem.IsBrowser() || OperatingSystem.IsWasi())
        {
            throw new PlatformNotSupportedException("Threads can't be started on this platform.");
        }
        this._thread = new Thread(this.Run) { IsBackground = true, Name = name };
        this._thread.Start();
    }

    /// <inheritdoc />
    public override int MaximumConcurrencyLevel => 1;

    /// <summary>Gets the shared scheduler, starting it if no provider is using it.</summary>
    public static LogWriterScheduler AcquireShared()
    {
        lock (SharedGate)
        {
            _shared ??= new LogWriterScheduler("Niddy log writer (shared)");
            _sharedUsers++;
            return _shared;
        }
    }

    /// <summary>Gives up a use of the shared scheduler, stopping its thread when it was the last one.</summary>
    /// <param name="scheduler">The scheduler <see cref="AcquireShared"/> returned.</param>
    public static void ReleaseShared(LogWriterScheduler scheduler)
    {
        lock (SharedGate)
        {
            if (!ReferenceEquals(scheduler, _shared))
            {
                return;
            }
            _sharedUsers--;
            if (_sharedUsers == 0)
            {
                _shared = null;
                scheduler.Complete();
            }
        }
    }

    /// <summary>Lets the thread finish once the queued tasks have run. Anything queued later runs on the thread pool.</summary>
    public void Complete()
    {
        lock (this._gate)
        {
            this._completed = true;
            Monitor.PulseAll(this._gate);
        }
    }

    /// <inheritdoc />
    protected override void QueueTask(Task task)
    {
        lock (this._gate)
        {
            if (!this._completed)
            {
                this._tasks.Enqueue(task);
                Monitor.Pulse(this._gate);
                return;
            }
        }
        // A write still running when its provider gave up waiting can resume after the thread has stopped.
        System.Threading.ThreadPool.UnsafeQueueUserWorkItem(t => this.TryExecuteTask(t), task, preferLocal: false);
    }

    /// <inheritdoc />
    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return Thread.CurrentThread == this._thread && this.TryExecuteTask(task);
    }

    /// <inheritdoc />
    protected override IEnumerable<Task> GetScheduledTasks()
    {
        lock (this._gate)
        {
            return this._tasks.ToArray();
        }
    }

    private void Run()
    {
        while (true)
        {
            Task task;
            lock (this._gate)
            {
                while (this._tasks.Count == 0 && !this._completed)
                {
                    Monitor.Wait(this._gate);
                }
                if (this._tasks.Count == 0)
                {
                    return;
                }
                task = this._tasks.Dequeue();
            }
            this.TryExecuteTask(task);
        }
    }
}
