using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Niddy.Core.Logging.Batching;

/// <summary>
/// A base class for <see cref="ILoggerProvider"/>s that write somewhere slow (files, a database, the network). Logging
/// never blocks: entries are queued and handed, in batches, to <see cref="WriteBatchAsync"/> on a background task.
/// </summary>
/// <remarks>
/// Disposing the provider (which disposing the <see cref="ILoggerFactory"/> does when it owns the provider; see
/// <see cref="NiddyLogging.AddOwnedProvider"/>) writes everything still queued, waiting up to
/// <see cref="BatchingLoggerOptions.ShutdownTimeout"/>. Exceptions from the writer never reach the code that logged:
/// the batch is dropped and <see cref="OnWriteFailed"/> is called.
/// <para>
/// The hooks run one at a time, on the thread chosen by <see cref="BatchingLoggerOptions.WriterThread"/>. With a
/// <see cref="LogWriterThread.Dedicated"/> or <see cref="LogWriterThread.Shared"/> writer, awaiting inside a hook comes
/// back to that thread unless you use <c>ConfigureAwait(false)</c>. As on a UI thread, never block on async code there
/// (<c>.Result</c>, <c>.Wait()</c>): it can deadlock the writer for good.
/// </para>
/// </remarks>
/// <typeparam name="TOptions">The provider's settings.</typeparam>
public abstract class BatchingLoggerProvider<TOptions> : ILoggerProvider, ISupportExternalScope
    where TOptions : BatchingLoggerOptions
{
    private sealed class BatchingLogger(BatchingLoggerProvider<TOptions> provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return provider._scopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None && logLevel >= provider.Options.MinimumLevel && Volatile.Read(in provider._disposed) == 0;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            string message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
            {
                return;
            }
            provider.Enqueue(new LogEntry(
                provider.Now(),
                logLevel,
                category,
                eventId,
                message,
                exception,
                provider.CaptureScopes(),
                state as IReadOnlyList<KeyValuePair<string, object?>> ?? []));
        }
    }

    private readonly Channel<LogEntry> _queue;

    private readonly Task _writer;

    private readonly CancellationTokenSource _shutdown = new();

    // Null when the writer runs on the thread pool.
    private readonly LogWriterScheduler? _scheduler;

    private readonly LogWriterThread _writerThread;

    private IExternalScopeProvider? _scopeProvider;

    private int _disposed;

    /// <summary>The settings this provider was created with.</summary>
    public TOptions Options { get; }

    /// <summary>Creates the provider and starts its background writer.</summary>
    /// <param name="options">The settings.</param>
    protected BatchingLoggerProvider(TOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxBatchSize, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.FlushInterval, TimeSpan.FromMilliseconds(int.MaxValue), nameof(options));
        this.Options = options;
        this._queue = options.MaxQueueLength > 0
            ? Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(options.MaxQueueLength) { SingleReader = true, FullMode = BoundedChannelFullMode.DropWrite })
            : Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions { SingleReader = true });
        (this._scheduler, this._writerThread) = this.CreateScheduler(options.WriterThread);
        // Nothing reaches the writer until a logger exists, so the derived constructor has finished by then.
        this._writer = this._scheduler == null
            ? Task.Run(this.WriteLoopAsync)
            : Task.Factory.StartNew(this.WriteLoopAsync, CancellationToken.None, TaskCreationOptions.DenyChildAttach, this._scheduler).Unwrap();
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new BatchingLogger(this, categoryName);
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this._scopeProvider = scopeProvider;
    }

    /// <summary>
    /// Stops accepting entries and waits (up to <see cref="BatchingLoggerOptions.ShutdownTimeout"/>) until the queued
    /// ones are written and <see cref="CloseAsync"/> has run.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref this._disposed, 1) != 0)
        {
            return;
        }
        this._queue.Writer.TryComplete();
        try
        {
            if (!this._writer.Wait(this.Options.ShutdownTimeout))
            {
                this._shutdown.Cancel();
            }
        }
        catch (AggregateException)
        {
        }
        this.ReleaseScheduler();
        GC.SuppressFinalize(this);
    }

    /// <summary>Writes a batch of entries, oldest first. Called on the background task, one batch at a time.</summary>
    /// <param name="batch">Up to <see cref="BatchingLoggerOptions.MaxBatchSize"/> entries. Don't keep the list; it's reused.</param>
    /// <param name="cancellationToken">Cancelled if disposing runs out of time.</param>
    protected abstract Task WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken);

    /// <summary>Called after the queue empties, e.g. to flush a buffered stream. Does nothing by default.</summary>
    /// <param name="cancellationToken">Cancelled if disposing runs out of time.</param>
    protected virtual Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called once, after the last batch when the provider is disposed, to release what the writer holds (files,
    /// connections). Does nothing by default.
    /// </summary>
    protected virtual Task CloseAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Called when <see cref="WriteBatchAsync"/> or <see cref="FlushAsync"/> throws. The batch is dropped either way.
    /// Does nothing by default; override it to reset a connection or count failures. Don't log from here.
    /// </summary>
    /// <param name="exception">What was thrown.</param>
    /// <param name="batch">The entries that weren't written, or empty if flushing failed.</param>
    protected virtual void OnWriteFailed(Exception exception, IReadOnlyList<LogEntry> batch)
    {
    }

    private (LogWriterScheduler? Scheduler, LogWriterThread Mode) CreateScheduler(LogWriterThread mode)
    {
        try
        {
            return mode switch
            {
                LogWriterThread.Dedicated => (new LogWriterScheduler($"Niddy log writer ({this.GetType().Name})"), mode),
                LogWriterThread.Shared => (LogWriterScheduler.AcquireShared(), mode),
                _ => (null, LogWriterThread.ThreadPool),
            };
        }
        catch (PlatformNotSupportedException)
        {
            // No threads (the browser, WASI): fall back to the thread pool, which works everywhere.
            return (null, LogWriterThread.ThreadPool);
        }
    }

    private void ReleaseScheduler()
    {
        if (this._scheduler == null)
        {
            return;
        }
        if (this._writerThread == LogWriterThread.Shared)
        {
            LogWriterScheduler.ReleaseShared(this._scheduler);
        }
        else
        {
            this._scheduler.Complete();
        }
    }

    private DateTimeOffset Now()
    {
        DateTimeOffset utcNow = this.Options.TimeProvider.GetUtcNow();
        return this.Options.UseUtc ? utcNow : utcNow.ToOffset(this.Options.TimeProvider.LocalTimeZone.GetUtcOffset(utcNow));
    }

    private IReadOnlyList<object?> CaptureScopes()
    {
        if (!this.Options.IncludeScopes || this._scopeProvider == null)
        {
            return [];
        }
        List<object?> scopes = new();
        this._scopeProvider.ForEachScope((scope, list) => list.Add(scope), scopes);
        return scopes;
    }

    private void Enqueue(LogEntry entry)
    {
        this._queue.Writer.TryWrite(entry);
    }

    // The awaits below use ConfigureAwait(true) on purpose: with a Dedicated or Shared writer, that keeps the loop and
    // its hooks on the writer's thread. On the thread pool there's no context to capture, so it makes no difference.
    private async Task WriteLoopAsync()
    {
        CancellationToken token = this._shutdown.Token;
        ChannelReader<LogEntry> reader = this._queue.Reader;
        List<LogEntry> batch = new(Math.Min(this.Options.MaxBatchSize, 1024));
        while (await reader.WaitToReadAsync().ConfigureAwait(true))
        {
            batch.Clear();
            if (this.Options.FlushInterval > TimeSpan.Zero)
            {
                await this.FillBatchAsync(reader, batch).ConfigureAwait(true);
            }
            else
            {
                this.TakeAvailable(reader, batch);
            }
            if (batch.Count > 0)
            {
                try
                {
                    await this.WriteBatchAsync(batch, token).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    this.ReportFailure(ex, batch);
                }
            }
            if (!reader.TryPeek(out _))
            {
                try
                {
                    await this.FlushAsync(token).ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    this.ReportFailure(ex, []);
                }
            }
        }
        try
        {
            await this.CloseAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.ReportFailure(ex, []);
        }
    }

    private void TakeAvailable(ChannelReader<LogEntry> reader, List<LogEntry> batch)
    {
        while (batch.Count < this.Options.MaxBatchSize && reader.TryRead(out LogEntry? entry))
        {
            batch.Add(entry);
        }
    }

    /// <summary>Fills the batch until it's full, the flush interval passes, or the provider is disposed.</summary>
    private async Task FillBatchAsync(ChannelReader<LogEntry> reader, List<LogEntry> batch)
    {
        using CancellationTokenSource timeout = new(this.Options.FlushInterval, this.Options.TimeProvider);
        try
        {
            while (true)
            {
                this.TakeAvailable(reader, batch);
                // WaitToReadAsync returns false once the queue is completed and empty, so disposing doesn't wait.
                if (batch.Count >= this.Options.MaxBatchSize || !await reader.WaitToReadAsync(timeout.Token).ConfigureAwait(true))
                {
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The interval passed: write what we have.
        }
    }

    private void ReportFailure(Exception exception, IReadOnlyList<LogEntry> batch)
    {
        try
        {
            this.OnWriteFailed(exception, batch);
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}
