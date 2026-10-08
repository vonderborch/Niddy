namespace Niddy.Core.Logging.Batching;

/// <summary>Where a <see cref="BatchingLoggerProvider{TOptions}"/> runs its background writer.</summary>
/// <remarks>
/// None of these use CPU while there's nothing to write: an idle writer is either an awaiting task or a blocked thread.
/// In the browser and on WASI, where threads can't be started, <see cref="Dedicated"/> and <see cref="Shared"/> fall
/// back to <see cref="ThreadPool"/>.
/// </remarks>
public enum LogWriterThread
{
    /// <summary>
    /// The writer borrows a thread-pool thread only while it's writing and holds none while idle. Cheapest, but if the
    /// app starves the thread pool, entries are written late.
    /// </summary>
    ThreadPool,

    /// <summary>
    /// The provider gets its own background thread. Writes aren't delayed by a starved thread pool, at the cost of one
    /// thread per provider.
    /// </summary>
    Dedicated,

    /// <summary>
    /// One background thread is shared by every provider that chooses this. Writers take turns, so a writer that blocks
    /// (rather than awaiting) holds up the others. Only use it for writers that await their I/O. The thread stops when
    /// the last provider using it is disposed.
    /// </summary>
    Shared,
}
