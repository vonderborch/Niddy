using Microsoft.Extensions.Time.Testing;
using Niddy.Threading;

namespace Niddy.Core.Tests;

public class DebouncerTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Invoke_RunsOnlyTheLastActionAfterTheDelay()
    {
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100), _time, captureContext: false);
        var runs = new List<int>();

        debouncer.Invoke(() => runs.Add(1));
        _time.Advance(TimeSpan.FromMilliseconds(60));
        debouncer.Invoke(() => runs.Add(2));
        _time.Advance(TimeSpan.FromMilliseconds(60));

        Assert.Empty(runs);
        Assert.True(debouncer.IsPending);

        _time.Advance(TimeSpan.FromMilliseconds(40));

        Assert.Equal([2], runs);
        Assert.False(debouncer.IsPending);
    }

    [Fact]
    public void Cancel_DropsThePendingAction()
    {
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100), _time, captureContext: false);
        var ran = false;

        debouncer.Invoke(() => ran = true);
        debouncer.Cancel();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.False(ran);
    }

    [Fact]
    public void Flush_RunsThePendingActionNow()
    {
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(100), _time, captureContext: false);
        var runs = 0;

        debouncer.Invoke(() => runs++);
        debouncer.Flush();
        _time.Advance(TimeSpan.FromSeconds(1));

        Assert.Equal(1, runs);
    }

    [Fact]
    public void Invoke_CancelsTheTokenOfARunningAsyncAction()
    {
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(10), _time, captureContext: false);
        var gate = new TaskCompletionSource();
        CancellationToken first = default;

        debouncer.Invoke(async token =>
        {
            first = token;
            await gate.Task;
        });
        _time.Advance(TimeSpan.FromMilliseconds(10));
        Assert.False(first.IsCancellationRequested);

        debouncer.Invoke(_ => Task.CompletedTask);

        Assert.True(first.IsCancellationRequested);
        gate.SetResult();
    }

    [Fact]
    public void Error_ReceivesExceptionsFromTheAction()
    {
        using var debouncer = new Debouncer(TimeSpan.FromMilliseconds(10), _time, captureContext: false);
        Exception? error = null;
        debouncer.Error += (_, ex) => error = ex;

        debouncer.Invoke(() => throw new InvalidOperationException("boom"));
        _time.Advance(TimeSpan.FromMilliseconds(10));

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void Invoke_AfterDispose_Throws()
    {
        var debouncer = new Debouncer(TimeSpan.FromMilliseconds(10), _time, captureContext: false);
        debouncer.Dispose();

        Assert.Throws<ObjectDisposedException>(() => debouncer.Invoke(() => { }));
    }
}

public class ThrottlerTests
{
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public void Invoke_RunsTheFirstCallNowAndTheLastAtTheEndOfTheInterval()
    {
        using var throttler = new Throttler(TimeSpan.FromMilliseconds(100), timeProvider: _time, captureContext: false);
        var runs = new List<int>();

        throttler.Invoke(() => runs.Add(1));
        throttler.Invoke(() => runs.Add(2));
        throttler.Invoke(() => runs.Add(3));
        Assert.Equal([1], runs);

        _time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal([1, 3], runs);

        // The trailing run started a new interval, so the next call waits for it.
        throttler.Invoke(() => runs.Add(4));
        Assert.Equal([1, 3], runs);
        _time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal([1, 3, 4], runs);
    }

    [Fact]
    public void Invoke_AfterAQuietInterval_RunsNowAgain()
    {
        using var throttler = new Throttler(TimeSpan.FromMilliseconds(100), timeProvider: _time, captureContext: false);
        var runs = 0;

        throttler.Invoke(() => runs++);
        _time.Advance(TimeSpan.FromMilliseconds(150));
        throttler.Invoke(() => runs++);

        Assert.Equal(2, runs);
    }

    [Fact]
    public void LeadingOnly_DropsCallsDuringTheInterval()
    {
        using var throttler = new Throttler(TimeSpan.FromMilliseconds(100), trailing: false, timeProvider: _time, captureContext: false);
        var runs = new List<int>();

        throttler.Invoke(() => runs.Add(1));
        throttler.Invoke(() => runs.Add(2));
        _time.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal([1], runs);
    }

    [Fact]
    public void TrailingOnly_WaitsForTheEndOfTheInterval()
    {
        using var throttler = new Throttler(TimeSpan.FromMilliseconds(100), leading: false, timeProvider: _time, captureContext: false);
        var runs = new List<int>();

        throttler.Invoke(() => runs.Add(1));
        throttler.Invoke(() => runs.Add(2));
        Assert.Empty(runs);

        _time.Advance(TimeSpan.FromMilliseconds(100));
        Assert.Equal([2], runs);
    }

    [Fact]
    public void Constructor_RejectsNeitherLeadingNorTrailing() =>
        Assert.Throws<ArgumentException>(() => new Throttler(TimeSpan.FromSeconds(1), leading: false, trailing: false));
}
