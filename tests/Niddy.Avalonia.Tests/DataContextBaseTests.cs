using Niddy.Avalonia.DataContexts;

namespace Niddy.Avalonia.Tests;

public class DataContextBaseTests
{
    private sealed class TestDataContext : DataContextBase
    {
        public Task Run(Func<Task> work) => RunAsync(work);

        public Task<int> Run(Func<Task<int>> work) => RunAsync(work);
    }

    [Fact]
    public async Task RunAsync_IsBusyWhileRunning()
    {
        var context = new TestDataContext();
        var gate = new TaskCompletionSource();

        var run = context.Run(() => gate.Task);
        Assert.True(context.IsBusy);

        gate.SetResult();
        await run;
        Assert.False(context.IsBusy);
        Assert.Null(context.ErrorMessage);
    }

    [Fact]
    public async Task RunAsync_RecordsTheErrorAndReturnsDefault()
    {
        var context = new TestDataContext();

        var result = await context.Run(() => Task.FromException<int>(new InvalidOperationException("broken")));

        Assert.Equal(0, result);
        Assert.Equal("broken", context.ErrorMessage);
        Assert.False(context.IsBusy);
    }

    [Fact]
    public async Task RaisesPropertyChangedAfterUpdating()
    {
        var context = new TestDataContext();
        var seen = new List<(string?, bool)>();
        context.PropertyChanged += (_, e) => seen.Add((e.PropertyName, context.IsBusy));

        await context.Run(() => Task.CompletedTask);

        Assert.Contains((nameof(DataContextBase.IsBusy), true), seen);
        Assert.Contains((nameof(DataContextBase.IsBusy), false), seen);
    }
}
