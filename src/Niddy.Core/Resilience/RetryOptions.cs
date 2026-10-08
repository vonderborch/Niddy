namespace Niddy.Core.Resilience;

/// <summary>
/// How <see cref="Retry"/> retries: how many attempts to make, how long to wait between them, and which failures
/// are worth another attempt.
/// </summary>
/// <remarks>
/// <para>
/// The wait after failed attempt <c>n</c> (1-based) is <c>min(BaseDelay × 2^(n-1), MaxDelay)</c>, plus a random
/// extra wait of up to <see cref="MaxJitter"/>. Set <see cref="BaseDelay"/> and <see cref="MaxDelay"/> to the same
/// value for a fixed wait.
/// </para>
/// <para>
/// Options are immutable, so one instance can be shared (for example in a <c>static readonly</c> field); derive
/// variants with <c>with</c>. Values are checked when they're set.
/// </para>
/// </remarks>
public sealed record RetryOptions
{
    /// <summary>The longest wait <see cref="Retry"/> can sleep for: <see cref="int.MaxValue"/> milliseconds
    /// (about 24.8 days).</summary>
    internal static readonly TimeSpan LongestWait = TimeSpan.FromMilliseconds(int.MaxValue);

    /// <summary>The defaults: 3 attempts, waits of 100 ms doubling up to 3 s, no jitter, and every exception
    /// retried.</summary>
    public static RetryOptions Default { get; } = new();

    /// <summary>The total number of attempts, including the first. At least 1; defaults to 3.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than 1.</exception>
    public int MaxAttempts
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1, nameof(this.MaxAttempts));
            field = value;
        }
    } = 3;

    /// <summary>The wait after the first failed attempt, doubling after each one after that. Defaults to
    /// 100 ms.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or longer than
    /// <see cref="int.MaxValue"/> milliseconds.</exception>
    public TimeSpan BaseDelay
    {
        get;
        init => field = CheckWait(value, nameof(this.BaseDelay));
    } = TimeSpan.FromMilliseconds(100);

    /// <summary>The cap on the doubling wait (<see cref="MaxJitter"/> is added on top). Defaults to 3 s.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or longer than
    /// <see cref="int.MaxValue"/> milliseconds.</exception>
    public TimeSpan MaxDelay
    {
        get;
        init => field = CheckWait(value, nameof(this.MaxDelay));
    } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The most extra time to add to each wait, picked at random between zero and this. Jitter spreads out
    /// callers that failed together (many clients hitting a server that just restarted) so their retries don't
    /// arrive at the same moment. Defaults to zero (no jitter).
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative or longer than
    /// <see cref="int.MaxValue"/> milliseconds.</exception>
    public TimeSpan MaxJitter
    {
        get;
        init => field = CheckWait(value, nameof(this.MaxJitter));
    }

    /// <summary>
    /// Decides whether an exception is worth another attempt. An exception it rejects is thrown straight away,
    /// unchanged. Null (the default) retries every exception.
    /// </summary>
    public Func<Exception, bool>? ShouldRetry { get; init; }

    /// <summary>
    /// Called after a failed attempt, before the wait, when another attempt will follow. Return true to go on
    /// retrying, or false to stop: <see cref="Retry"/> then throws a <see cref="RetryBlockedException"/>. Use it
    /// to log, or to fix what made the attempt fail. Not called after the last attempt.
    /// </summary>
    public Func<RetryAttempt, bool>? OnRetry { get; init; }

    private static TimeSpan CheckWait(TimeSpan value, string name)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, TimeSpan.Zero, name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(value, LongestWait, name);
        return value;
    }
}

/// <summary>
/// A failed attempt, passed to <see cref="RetryOptions.OnRetry"/> before <see cref="Retry"/> waits and tries again.
/// </summary>
/// <param name="Number">The number of the attempt that failed, starting at 1.</param>
/// <param name="Exception">What the attempt threw.</param>
/// <param name="Delay">How long <see cref="Retry"/> will wait before the next attempt, jitter included.</param>
public readonly record struct RetryAttempt(int Number, Exception Exception, TimeSpan Delay);
