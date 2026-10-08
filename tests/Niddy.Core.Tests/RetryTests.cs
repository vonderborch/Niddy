using Niddy.Core.Resilience;

namespace Niddy.Core.Tests;

public class RetryTests
{
    private static readonly RetryOptions NoWait = new() { BaseDelay = TimeSpan.Zero };

    [Fact]
    public void Execute_RetriesUntilSuccess()
    {
        int attempts = 0;

        int result = Retry.Execute(() => ++attempts < 3 ? throw new IOException() : attempts, NoWait);

        Assert.Equal(3, result);
    }

    [Fact]
    public void Execute_UsesTheDefaultsWithoutOptions()
    {
        int attempts = 0;

        Assert.Throws<MaxRetriesException>(() => Retry.Execute(() =>
        {
            attempts++;
            throw new IOException();
        }));

        Assert.Equal(RetryOptions.Default.MaxAttempts, attempts);
    }

    [Fact]
    public void Execute_ThrowsMaxRetriesExceptionWithEveryFailure()
    {
        int attempts = 0;

        MaxRetriesException ex = Assert.Throws<MaxRetriesException>(() => Retry.Execute(
            () => throw new IOException($"fail {++attempts}"), NoWait with { MaxAttempts = 3 }));

        Assert.Equal(3, ex.Attempts);
        Assert.Equal(["fail 1", "fail 2", "fail 3"], ex.Exceptions.Select(e => e.Message));
        Assert.Same(ex.Exceptions[^1], ex.InnerException);
        Assert.Equal("Gave up after 3 attempts: fail 3", ex.Message);
    }

    [Fact]
    public void Execute_WithOneAttempt_ThrowsMaxRetriesExceptionWithoutRetrying()
    {
        MaxRetriesException ex = Assert.Throws<MaxRetriesException>(() => Retry.Execute(
            () => throw new IOException("nope"), NoWait with { MaxAttempts = 1 }));

        Assert.Equal(1, ex.Attempts);
        Assert.Equal("Gave up after 1 attempt: nope", ex.Message);
    }

    [Fact]
    public void Execute_ThrowsExceptionsShouldRetryRejectsUnchanged()
    {
        int attempts = 0;

        Assert.Throws<InvalidOperationException>(() => Retry.Execute(() =>
        {
            attempts++;
            throw new InvalidOperationException();
        }, NoWait with { MaxAttempts = 5, ShouldRetry = ex => ex is IOException }));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public void Execute_CallsOnRetryBeforeEachRetry()
    {
        List<(string Message, int Number)> calls = new();
        int attempts = 0;

        Retry.Execute(() =>
        {
            if (++attempts < 3)
            {
                throw new IOException($"fail {attempts}");
            }
        }, NoWait with { OnRetry = a => { calls.Add((a.Exception.Message, a.Number)); return true; } });

        Assert.Equal([("fail 1", 1), ("fail 2", 2)], calls);
    }

    [Fact]
    public void Execute_DoesNotCallOnRetryAfterTheLastAttempt()
    {
        int calls = 0;

        Assert.Throws<MaxRetriesException>(() => Retry.Execute(
            () => throw new IOException(), NoWait with { MaxAttempts = 2, OnRetry = _ => ++calls > 0 }));

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Execute_OnRetryReturningFalse_ThrowsRetryBlockedException()
    {
        int attempts = 0;

        RetryBlockedException ex = Assert.Throws<RetryBlockedException>(() => Retry.Execute(
            () => throw new IOException($"fail {++attempts}"),
            NoWait with { MaxAttempts = 5, OnRetry = a => a.Number < 2 }));

        Assert.Equal(2, attempts);
        Assert.Equal(2, ex.Attempts);
        Assert.Equal("fail 2", ex.InnerException!.Message);
        Assert.Equal("Retrying was stopped after attempt 2: fail 2", ex.Message);
    }

    [Fact]
    public void Execute_OnRetryIsToldTheWait()
    {
        List<TimeSpan> delays = new();
        RetryOptions options = new()
        {
            MaxAttempts = 4,
            BaseDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(3),
            OnRetry = a => { delays.Add(a.Delay); return true; },
        };

        Assert.Throws<MaxRetriesException>(() => Retry.Execute(() => throw new IOException(), options));

        Assert.Equal([TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2), TimeSpan.FromMilliseconds(3)], delays);
    }

    [Fact]
    public void RetryExceptions_CanBeCaughtTogether()
    {
        Assert.ThrowsAny<RetryException>(() => Retry.Execute(() => throw new IOException(), NoWait));
        Assert.ThrowsAny<RetryException>(() => Retry.Execute(() => throw new IOException(), NoWait with { OnRetry = _ => false }));
    }

    [Fact]
    public void RetryExceptions_RequireAtLeastOneException()
    {
        Assert.Throws<ArgumentException>(() => new MaxRetriesException([]));
        Assert.Throws<ArgumentNullException>(() => new RetryBlockedException(null!));
    }

    [Fact]
    public void RetryExceptions_CopyTheList()
    {
        List<Exception> failures = new() { new IOException() };

        MaxRetriesException ex = new(failures);
        failures.Add(new IOException());

        Assert.Equal(1, ex.Attempts);
    }

    [Fact]
    public void RetryOptions_RejectsBadValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxAttempts = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { BaseDelay = TimeSpan.FromMilliseconds(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxDelay = TimeSpan.FromMilliseconds(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxJitter = TimeSpan.FromMilliseconds(-1) });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetryOptions { MaxDelay = TimeSpan.FromDays(30) });
        Assert.Throws<ArgumentOutOfRangeException>(() => RetryOptions.Default with { MaxAttempts = -1 });
    }

    [Theory]
    [InlineData(1, 100, 3000, 100)]
    [InlineData(2, 100, 3000, 200)]
    [InlineData(5, 100, 3000, 1600)]
    [InlineData(6, 100, 3000, 3000)]
    [InlineData(40, 100, 3000, 3000)]              // 100 × 2³⁹ ms would overflow a long of ticks
    [InlineData(1000, 1, int.MaxValue, int.MaxValue)]
    [InlineData(3, 0, 3000, 0)]
    [InlineData(3, 5000, 1000, 1000)]              // a base above the cap is capped
    public void GetDelay_DoublesUpToTheCapWithoutOverflowing(int attempt, int baseDelayMs, int maxDelayMs, int expectedMs)
    {
        RetryOptions options = new()
        {
            BaseDelay = TimeSpan.FromMilliseconds(baseDelayMs),
            MaxDelay = TimeSpan.FromMilliseconds(maxDelayMs),
        };

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMs), Retry.GetDelay(options, attempt, new Random(1)));
    }

    [Fact]
    public void GetDelay_AddsRandomJitterOnTop()
    {
        RetryOptions options = new()
        {
            BaseDelay = TimeSpan.FromMilliseconds(100),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            MaxJitter = TimeSpan.FromMilliseconds(50),
        };
        Random random = new(42);

        List<TimeSpan> delays = Enumerable.Range(0, 1000).Select(_ => Retry.GetDelay(options, 3, random)).ToList();

        Assert.All(delays, d => Assert.InRange(d, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(150)));
        Assert.True(delays.Distinct().Count() > 100, "The jitter should vary.");
        Assert.True(delays.Min() < TimeSpan.FromMilliseconds(105) && delays.Max() > TimeSpan.FromMilliseconds(145),
            "The jitter should cover the whole range.");
    }

    [Fact]
    public void GetDelay_NeverExceedsTheLongestWait()
    {
        RetryOptions options = new()
        {
            BaseDelay = RetryOptions.LongestWait,
            MaxDelay = RetryOptions.LongestWait,
            MaxJitter = RetryOptions.LongestWait,
        };

        Assert.Equal(RetryOptions.LongestWait, Retry.GetDelay(options, 1, new Random(1)));
    }

    [Fact]
    public void Execute_WithJitter_StillRetries()
    {
        int attempts = 0;

        int result = Retry.Execute(() => ++attempts < 3 ? throw new IOException() : attempts,
            NoWait with { MaxJitter = TimeSpan.FromMilliseconds(2) });

        Assert.Equal(3, result);
    }

    [Fact]
    public void Execute_ManyAttemptsWithASmallCap_DoesNotOverflow()
    {
        int attempts = 0;
        RetryOptions options = new() { MaxAttempts = 40, BaseDelay = TimeSpan.FromMilliseconds(1), MaxDelay = TimeSpan.FromMilliseconds(1) };

        int result = Retry.Execute(() => ++attempts < 40 ? throw new IOException() : attempts, options);

        Assert.Equal(40, result);
    }

    [Fact]
    public void ExecuteAndRethrowLast_ThrowsTheOriginalException()
    {
        int attempts = 0;

        IOException ex = Assert.Throws<IOException>(() => Retry.ExecuteAndRethrowLast(
            () => throw new IOException($"fail {++attempts}"), NoWait with { MaxAttempts = 2 }));

        Assert.Equal("fail 2", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_ThrowsMaxRetriesException()
    {
        int attempts = 0;

        MaxRetriesException ex = await Assert.ThrowsAsync<MaxRetriesException>(() => Retry.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.Yield();
            throw new IOException();
        }, NoWait with { MaxAttempts = 2 }, TestContext.Current.CancellationToken));

        Assert.Equal(2, attempts);
        Assert.IsType<IOException>(ex.InnerException);
    }

    [Fact]
    public async Task ExecuteAsync_OnRetryReturningFalse_ThrowsRetryBlockedException()
    {
        int attempts = 0;

        await Assert.ThrowsAsync<RetryBlockedException>(() => Retry.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.Yield();
            throw new TimeoutException();
        }, NoWait with { OnRetry = _ => false }, TestContext.Current.CancellationToken));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_StopsRetryingOnceCancelled()
    {
        using CancellationTokenSource cts = new();
        int attempts = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Retry.ExecuteAsync(async () =>
        {
            attempts++;
            await cts.CancelAsync();
            cts.Token.ThrowIfCancellationRequested();
        }, NoWait with { MaxAttempts = 5 }, cts.Token));

        Assert.Equal(1, attempts);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotStartWhenAlreadyCancelled()
    {
        bool ran = false;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Retry.ExecuteAsync(
            () => { ran = true; return Task.CompletedTask; },
            cancellationToken: new CancellationToken(canceled: true)));

        Assert.False(ran);
    }

    [Fact]
    public async Task ExecuteAsync_HonorsShouldRetryAndOnRetry()
    {
        List<int> retried = new();
        int attempts = 0;

        int result = await Retry.ExecuteAsync(async () =>
        {
            await Task.Yield();
            return ++attempts < 3 ? throw new TimeoutException() : attempts;
        }, NoWait with
        {
            ShouldRetry = ex => ex is TimeoutException,
            OnRetry = a => { retried.Add(a.Number); return true; },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(3, result);
        Assert.Equal([1, 2], retried);
    }
}
