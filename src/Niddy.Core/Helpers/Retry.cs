namespace Niddy.Helpers;

/// <summary>
/// Helpers for retrying operations with exponential backoff.
/// </summary>
public static class Retry
{
    /// <summary>
    /// Executes an action, retrying on failure with exponential backoff.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    /// <param name="maxRetries">Maximum number of attempts.</param>
    /// <param name="baseDelayMs">Initial delay in milliseconds (doubles each attempt).</param>
    /// <param name="maxDelayMs">Maximum delay cap in milliseconds.</param>
    public static void Execute(Action action, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                action();
                break;
            }
            catch
            {
                if (i == maxRetries - 1)
                {
                    throw;
                }
                Thread.Sleep(Math.Min(baseDelayMs * (int)Math.Pow(2.0, i), maxDelayMs));
            }
        }
    }

    /// <summary>
    /// Executes a function, retrying on failure with exponential backoff.
    /// </summary>
    public static T Execute<T>(Func<T> func, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return func();
            }
            catch
            {
                if (i == maxRetries - 1)
                {
                    throw;
                }
                Thread.Sleep(Math.Min(baseDelayMs * (int)Math.Pow(2.0, i), maxDelayMs));
            }
        }
        throw new InvalidOperationException("Retry.Execute exhausted all attempts.");
    }

    /// <summary>
    /// Asynchronously executes an action, retrying on failure with exponential backoff.
    /// </summary>
    public static async Task ExecuteAsync(Func<Task> action, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000, CancellationToken cancellationToken = default(CancellationToken))
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await action();
                break;
            }
            catch
            {
                if (attempt == maxRetries - 1)
                {
                    throw;
                }
                await Task.Delay(Math.Min(baseDelayMs * (int)Math.Pow(2.0, attempt), maxDelayMs), cancellationToken);
            }
        }
    }

    /// <summary>
    /// Asynchronously executes a function, retrying on failure with exponential backoff.
    /// </summary>
    public static async Task<T> ExecuteAsync<T>(Func<Task<T>> func, int maxRetries = 3, int baseDelayMs = 100, int maxDelayMs = 3000, CancellationToken cancellationToken = default(CancellationToken))
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await func();
            }
            catch
            {
                if (attempt == maxRetries - 1)
                {
                    throw;
                }
                await Task.Delay(Math.Min(baseDelayMs * (int)Math.Pow(2.0, attempt), maxDelayMs), cancellationToken);
            }
        }
        throw new InvalidOperationException("Retry.ExecuteAsync exhausted all attempts.");
    }
}
