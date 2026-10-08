using Niddy.Core.Threading;

namespace Niddy.Core.Tests;

public class ThreadingTests
{
    [Fact]
    public void Guard_CheckSetSucceedsOnlyOnceUntilReset()
    {
        Guard guard = new();

        Assert.False(guard.IsClaimed);
        Assert.True(guard.TryClaim);
        Assert.False(guard.TryClaim);
        Assert.True(guard == true);

        guard.Release();
        Assert.True(guard.TryClaim);
    }

    [Fact]
    public void Guard_CheckSetIsExclusiveAcrossThreads()
    {
        Guard guard = new();
        int winners = 0;

        Parallel.For(0, 1000, _ =>
        {
            if (guard.TryClaim)
            {
                Interlocked.Increment(ref winners);
            }
        });

        Assert.Equal(1, winners);
    }

    [Fact]
    public void Increment_StopsAtMaximumAndReturnsPreviousValue()
    {
        int value = 0;

        Assert.Equal(0, AtomicOperations.Increment(ref value, 2));
        Assert.Equal(1, AtomicOperations.Increment(ref value, 2));
        Assert.Equal(2, AtomicOperations.Increment(ref value, 2));
        Assert.Equal(2, value);
    }

    [Fact]
    public void Increment_IsAtomicAcrossThreads()
    {
        int value = 0;

        Parallel.For(0, 10_000, _ => AtomicOperations.Increment(ref value, 5_000));

        Assert.Equal(5_000, value);
    }

    [Fact]
    public void Decrement_StopsAtMinimum()
    {
        long value = 1L;

        AtomicOperations.Decrement(ref value, 0L);
        AtomicOperations.Decrement(ref value, 0L);

        Assert.Equal(0L, value);
    }

    [Fact]
    public void CompareAndSwap_OnlySwapsWhenExpectedMatches()
    {
        int value = 5;

        Assert.False(AtomicOperations.CompareAndSwap(ref value, 10, 4));
        Assert.Equal(5, value);
        Assert.True(AtomicOperations.CompareAndSwap(ref value, 10, 5, out int original));
        Assert.Equal(5, original);
        Assert.Equal(10, value);
    }
}
