using System.Runtime.ExceptionServices;

namespace Niddy.Core.Resilience;

/// <summary>
/// Runs an operation, retrying with exponential backoff (and optional jitter) when it throws. See
/// <see cref="RetryOptions"/> for the settings.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>When every attempt fails, a <see cref="MaxRetriesException"/> is thrown.</item>
/// <item>When <see cref="RetryOptions.OnRetry"/> returns false, a <see cref="RetryBlockedException"/> is
/// thrown.</item>
/// <item>An exception <see cref="RetryOptions.ShouldRetry"/> rejects is thrown straight away, unchanged.</item>
/// </list>
/// In the first two cases the last attempt's exception is the <see cref="Exception.InnerException"/>.
/// </remarks>
public static class Retry
{
    /// <summary>
    /// Runs an action, retrying on failure.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="options">How to retry. Null uses <see cref="RetryOptions.Default"/>.</param>
    /// <exception cref="MaxRetriesException">Every attempt failed.</exception>
    /// <exception cref="RetryBlockedException"><see cref="RetryOptions.OnRetry"/> returned false.</exception>
    public static void Execute(Action action, RetryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(action);
        Execute<object?>(() =>
        {
            action();
            return null;
        }, options);
    }

    /// <summary>
    /// Runs a function, retrying on failure.
    /// </summary>
    /// <param name="func">The function to run.</param>
    /// <param name="options">How to retry. Null uses <see cref="RetryOptions.Default"/>.</param>
    /// <returns>The first successful result.</returns>
    /// <exception cref="MaxRetriesException">Every attempt failed.</exception>
    /// <exception cref="RetryBlockedException"><see cref="RetryOptions.OnRetry"/> returned false.</exception>
    public static T Execute<T>(Func<T> func, RetryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(func);
        RetryRun run = new(options, CancellationToken.None);

        while (true)
        {
            TimeSpan delay;
            try
            {
                return func();
            }
            catch (Exception ex) when (run.CanRetry(ex))
            {
                delay = run.Failed(ex);
            }

            Thread.Sleep(delay);
        }
    }

    /// <summary>
    /// Runs an asynchronous action, retrying on failure.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <param name="options">How to retry. Null uses <see cref="RetryOptions.Default"/>.</param>
    /// <param name="cancellationToken">Checked before each attempt, and cancels the waits between them. Once it's
    /// cancelled, a failed attempt isn't retried: its exception is thrown unchanged. Pass it to
    /// <paramref name="action"/> as well to cancel an attempt in progress.</param>
    /// <exception cref="MaxRetriesException">Every attempt failed.</exception>
    /// <exception cref="RetryBlockedException"><see cref="RetryOptions.OnRetry"/> returned false.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static Task ExecuteAsync(Func<Task> action, RetryOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteAsync<object?>(async () =>
        {
            await action();
            return null;
        }, options, cancellationToken);
    }

    /// <summary>
    /// Runs an asynchronous function, retrying on failure.
    /// </summary>
    /// <param name="func">The function to run.</param>
    /// <param name="options">How to retry. Null uses <see cref="RetryOptions.Default"/>.</param>
    /// <param name="cancellationToken">Checked before each attempt, and cancels the waits between them. Once it's
    /// cancelled, a failed attempt isn't retried: its exception is thrown unchanged. Pass it to
    /// <paramref name="func"/> as well to cancel an attempt in progress.</param>
    /// <returns>The first successful result.</returns>
    /// <exception cref="MaxRetriesException">Every attempt failed.</exception>
    /// <exception cref="RetryBlockedException"><see cref="RetryOptions.OnRetry"/> returned false.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> func, RetryOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(func);
        RetryRun run = new(options, cancellationToken);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TimeSpan delay;
            try
            {
                return await func();
            }
            catch (Exception ex) when (run.CanRetry(ex))
            {
                delay = run.Failed(ex);
            }

            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// <see cref="Execute(Action, RetryOptions?)"/>, but when retrying stops, the last attempt's exception is
    /// rethrown (with its original stack trace) instead of a <see cref="RetryException"/>. For helpers whose
    /// retrying is an implementation detail callers shouldn't have to know about.
    /// </summary>
    internal static void ExecuteAndRethrowLast(Action action, RetryOptions options)
    {
        try
        {
            Execute(action, options);
        }
        catch (RetryException ex)
        {
            ExceptionDispatchInfo.Throw(ex.InnerException!);
        }
    }

    /// <summary>
    /// The wait after failed attempt <paramref name="attempt"/> (1-based): <c>min(BaseDelay × 2^(attempt-1),
    /// MaxDelay)</c> plus up to <see cref="RetryOptions.MaxJitter"/>. The doubling can't overflow, however many
    /// attempts there are.
    /// </summary>
    internal static TimeSpan GetDelay(RetryOptions options, int attempt, Random random)
    {
        int shift = Math.Min(attempt - 1, 62);
        (long baseTicks, long maxTicks) = (options.BaseDelay.Ticks, options.MaxDelay.Ticks);
        long ticks = baseTicks > maxTicks >> shift ? maxTicks : baseTicks << shift;

        if (options.MaxJitter > TimeSpan.Zero)
        {
            ticks += random.NextInt64(options.MaxJitter.Ticks + 1);
        }

        return TimeSpan.FromTicks(Math.Min(ticks, RetryOptions.LongestWait.Ticks));
    }

    /// <summary>
    /// The state of one <c>Execute</c> call, shared by the sync and async loops: decides whether a failure is
    /// retried, and either returns the wait before the next attempt or throws.
    /// </summary>
    private sealed class RetryRun(RetryOptions? options, CancellationToken cancellationToken)
    {
        private readonly RetryOptions _options = options ?? RetryOptions.Default;
        private readonly List<Exception> _failures = [];

        /// <summary>False for failures that should be thrown unchanged: rejected by
        /// <see cref="RetryOptions.ShouldRetry"/>, or after cancellation.</summary>
        public bool CanRetry(Exception ex) =>
            !cancellationToken.IsCancellationRequested && (this._options.ShouldRetry?.Invoke(ex) ?? true);

        /// <summary>Records a failed attempt and returns the wait before the next one.</summary>
        /// <exception cref="MaxRetriesException">That was the last attempt.</exception>
        /// <exception cref="RetryBlockedException"><see cref="RetryOptions.OnRetry"/> returned false.</exception>
        public TimeSpan Failed(Exception ex)
        {
            this._failures.Add(ex);
            int attempt = this._failures.Count;
            if (attempt >= this._options.MaxAttempts)
            {
                throw new MaxRetriesException(this._failures);
            }

            TimeSpan delay = GetDelay(this._options, attempt, Random.Shared);
            if (this._options.OnRetry is { } onRetry && !onRetry(new RetryAttempt(attempt, ex, delay)))
            {
                throw new RetryBlockedException(this._failures);
            }

            return delay;
        }
    }
}
