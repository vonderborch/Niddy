using Microsoft.Extensions.Logging;

namespace Niddy.Core.Logging.Batching;

/// <summary>Settings shared by every <see cref="BatchingLoggerProvider{TOptions}"/>. Derive from it to add your own.</summary>
public class BatchingLoggerOptions
{
    /// <summary>The least severe level written. Defaults to <see cref="LogLevel.Information"/>.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>Whether to capture the active logging scopes in each <see cref="LogEntry"/>. Defaults to false.</summary>
    public bool IncludeScopes { get; set; }

    /// <summary>Whether to use UTC timestamps instead of local time. Defaults to false.</summary>
    public bool UseUtc { get; set; }

    /// <summary>The clock used for timestamps. Defaults to the system clock; replace it in tests.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;

    /// <summary>The most entries handed to the writer at once. Defaults to 100.</summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// How long a part-full batch waits for more entries before it's written. A batch is written as soon as it holds
    /// <see cref="MaxBatchSize"/> entries or this much time has passed, whichever comes first. Defaults to zero, which
    /// writes entries as soon as the writer picks them up. Set it for writers where each write is costly (a database,
    /// the network), e.g. two seconds. Disposing never waits for it.
    /// </summary>
    public TimeSpan FlushInterval { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// The most entries waiting to be written; when it's full, new entries are dropped. Defaults to 0, meaning no limit.
    /// Set it for slow writers (a database, the network) so a backlog can't use up memory.
    /// </summary>
    public int MaxQueueLength { get; set; }

    /// <summary>
    /// Where the background writer runs: borrowing thread-pool threads (the default), on a thread of its own, or on one
    /// thread shared with other providers. See <see cref="LogWriterThread"/>.
    /// </summary>
    public LogWriterThread WriterThread { get; set; } = LogWriterThread.ThreadPool;

    /// <summary>
    /// How long disposing waits for queued entries to be written. When it runs out, the writer's cancellation token is
    /// cancelled and anything still queued is lost. Defaults to five seconds.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
