namespace Niddy.Core.Resilience;

/// <summary>
/// The base for the exceptions <see cref="Retry"/> throws when it stops retrying:
/// <see cref="MaxRetriesException"/> and <see cref="RetryBlockedException"/>. Catch this to handle both.
/// </summary>
/// <remarks>
/// <see cref="Exception.InnerException"/> is the last attempt's exception; <see cref="Exceptions"/> has every
/// attempt's.
/// </remarks>
public abstract class RetryException : Exception
{
    private protected RetryException(IReadOnlyList<Exception> exceptions, Func<int, string> describe)
        : this(Checked(exceptions).ToArray(), describe)
    {
    }

    private RetryException(Exception[] exceptions, Func<int, string> describe)
        : base($"{describe(exceptions.Length)}: {exceptions[^1].Message}", exceptions[^1])
    {
        this.Exceptions = exceptions;
    }

    /// <summary>What each attempt threw, in order. The last one is also the
    /// <see cref="Exception.InnerException"/>.</summary>
    public IReadOnlyList<Exception> Exceptions { get; }

    /// <summary>The number of attempts that were made (and failed).</summary>
    public int Attempts => this.Exceptions.Count;

    private static IReadOnlyList<Exception> Checked(IReadOnlyList<Exception> exceptions)
    {
        ArgumentNullException.ThrowIfNull(exceptions);
        if (exceptions.Count == 0)
        {
            throw new ArgumentException("At least one exception is required.", nameof(exceptions));
        }

        return exceptions;
    }
}

/// <summary>
/// Thrown by <see cref="Retry"/> when every attempt failed. <see cref="Exception.InnerException"/> is the last
/// attempt's exception.
/// </summary>
public sealed class MaxRetriesException : RetryException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MaxRetriesException"/> class.
    /// </summary>
    /// <param name="exceptions">What each attempt threw, in order. At least one.</param>
    public MaxRetriesException(IReadOnlyList<Exception> exceptions)
        : base(exceptions, count => count == 1 ? "Gave up after 1 attempt" : $"Gave up after {count} attempts")
    {
    }
}

/// <summary>
/// Thrown by <see cref="Retry"/> when <see cref="RetryOptions.OnRetry"/> returned false to stop retrying.
/// <see cref="Exception.InnerException"/> is the exception of the attempt it was called for.
/// </summary>
public sealed class RetryBlockedException : RetryException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryBlockedException"/> class.
    /// </summary>
    /// <param name="exceptions">What each attempt threw, in order. At least one.</param>
    public RetryBlockedException(IReadOnlyList<Exception> exceptions)
        : base(exceptions, count => $"Retrying was stopped after attempt {count}")
    {
    }
}
