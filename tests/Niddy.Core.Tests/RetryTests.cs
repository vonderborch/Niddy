using Niddy.Helpers;

namespace Niddy.Core.Tests;

public class RetryTests
{
    [Fact]
    public void Execute_RetriesUntilSuccess()
    {
        var attempts = 0;

        var result = Retry.Execute(() => ++attempts < 3 ? throw new IOException() : attempts, baseDelayMs: 1);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task ExecuteAsync_GivesUpAfterMaxRetries()
    {
        var attempts = 0;

        await Assert.ThrowsAnyAsync<Exception>(() => Retry.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.Yield();
            throw new IOException();
        }, maxRetries: 2, baseDelayMs: 1, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(2, attempts);
    }
}
