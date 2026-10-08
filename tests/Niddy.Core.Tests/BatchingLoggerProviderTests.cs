using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Niddy.Core.Logging;
using Niddy.Core.Logging.Batching;

namespace Niddy.Core.Tests;

public sealed class BatchingLoggerProviderTests
{
    private sealed class RecordingProvider(BatchingLoggerOptions options) : BatchingLoggerProvider<BatchingLoggerOptions>(options)
    {
        private readonly Lock _lock = new();

        public List<List<LogEntry>> Batches { get; } = new();

        public List<Exception> Failures { get; } = new();

        /// <summary>When set, the first batch waits on it, so later entries pile up behind it.</summary>
        public TaskCompletionSource? Gate { get; init; }

        public bool ThrowOnWrite { get; set; }

        /// <summary>When set, each write awaits a short delay, to check where the writer resumes.</summary>
        public bool AwaitInWrite { get; init; }

        /// <summary>The thread each write started on, and the thread it was on after awaiting (if it did).</summary>
        public List<Thread> Threads { get; } = new();

        /// <summary>Whether each entry in <see cref="Threads"/> was a pool thread, captured while it was alive.</summary>
        public List<bool> OnPool { get; } = new();

        public bool AllOnBackgroundThreads { get; private set; } = true;

        public bool Closed { get; private set; }

        public List<LogEntry> All
        {
            get
            {
                lock (this._lock)
                {
                    return this.Batches.SelectMany(b => b).ToList();
                }
            }
        }

        protected override async Task WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
        {
            if (this.Gate != null)
            {
                await this.Gate.Task.ConfigureAwait(false);
            }
            if (this.ThrowOnWrite)
            {
                throw new IOException("database is down");
            }
            Thread started = Thread.CurrentThread;
            bool startedOnPool = started.IsThreadPoolThread;
            if (this.AwaitInWrite)
            {
                await Task.Delay(1);
            }
            lock (this._lock)
            {
                this.Threads.Add(started);
                this.Threads.Add(Thread.CurrentThread);
                this.OnPool.Add(startedOnPool);
                this.OnPool.Add(Thread.CurrentThread.IsThreadPoolThread);
                this.AllOnBackgroundThreads &= Thread.CurrentThread.IsBackground;
                this.Batches.Add(batch.ToList());
            }
        }

        protected override Task CloseAsync()
        {
            this.Closed = true;
            return Task.CompletedTask;
        }

        protected override void OnWriteFailed(Exception exception, IReadOnlyList<LogEntry> batch)
        {
            lock (this._lock)
            {
                this.Failures.Add(exception);
            }
        }
    }

    private static BatchingLoggerOptions Options(Action<BatchingLoggerOptions>? configure = null)
    {
        BatchingLoggerOptions options = new()
        {
            UseUtc = true,
            TimeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero)),
        };
        configure?.Invoke(options);
        return options;
    }

    [Fact]
    public void Dispose_WritesEverythingQueuedInBatchesNoBiggerThanMaxBatchSize()
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingProvider provider = new(Options(o => o.MaxBatchSize = 10)) { Gate = gate };
        ILogger logger = provider.CreateLogger("T");
        for (int i = 0; i < 95; i++)
        {
            logger.LogInformation("line {Index}", i);
        }
        gate.SetResult();
        provider.Dispose();

        Assert.All(provider.Batches, b => Assert.InRange(b.Count, 1, 10));
        Assert.Equal(Enumerable.Range(0, 95).Select(i => $"line {i}"), provider.All.Select(e => e.Message));
        Assert.True(provider.Closed);
    }

    [Fact]
    public void LogEntry_CarriesTheEventPropertiesAndScopes()
    {
        RecordingProvider provider = new(Options(o => o.IncludeScopes = true));
        provider.SetScopeProvider(new LoggerExternalScopeProvider());
        ILogger logger = provider.CreateLogger("Tests.Orders");
        InvalidOperationException error = new("boom");
        using (logger.BeginScope("request {Id}", 7))
        {
            logger.LogWarning(new EventId(42, "Saved"), error, "Saved order {OrderId} for {Customer}", 1001, "ada");
        }
        logger.LogDebug("below the minimum level");
        provider.Dispose();

        LogEntry entry = Assert.Single(provider.All);
        Assert.Equal(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero), entry.Timestamp);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Equal("Tests.Orders", entry.Category);
        Assert.Equal(42, entry.EventId.Id);
        Assert.Equal("Saved", entry.EventId.Name);
        Assert.Equal("Saved order 1001 for ada", entry.Message);
        Assert.Same(error, entry.Exception);
        Assert.Contains(new KeyValuePair<string, object?>("OrderId", 1001), entry.Properties);
        Assert.Contains(new KeyValuePair<string, object?>("Customer", "ada"), entry.Properties);
        Assert.Equal("request 7", Assert.Single(entry.Scopes)?.ToString());
    }

    [Fact]
    public void WriteFailures_AreReportedAndNeverThrown()
    {
        RecordingProvider provider = new(Options()) { ThrowOnWrite = true };
        ILogger logger = provider.CreateLogger("T");
        logger.LogInformation("lost");
        provider.Dispose();

        Assert.IsType<IOException>(Assert.Single(provider.Failures));
        Assert.Empty(provider.Batches);
        Assert.True(provider.Closed);
    }

    [Fact]
    public void MaxQueueLength_DropsNewEntriesWhenFull()
    {
        TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RecordingProvider provider = new(Options(o => o.MaxQueueLength = 5)) { Gate = gate };
        ILogger logger = provider.CreateLogger("T");
        for (int i = 0; i < 50; i++)
        {
            logger.LogInformation("line {Index}", i);
        }
        gate.SetResult();
        provider.Dispose();

        // The writer may have taken one batch off the queue before the gate, so a few more than five can get through.
        Assert.InRange(provider.All.Count, 5, 10);
        Assert.Equal("line 0", provider.All[0].Message);
    }

    [Fact]
    public void Logging_AfterDisposeIsIgnored()
    {
        RecordingProvider provider = new(Options());
        ILogger logger = provider.CreateLogger("T");
        provider.Dispose();

        Assert.False(logger.IsEnabled(LogLevel.Critical));
        logger.LogCritical("too late");
        Assert.Empty(provider.All);
    }

    private static async Task WaitUntil(Func<bool> condition, Action? step = null)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the writer.");
            step?.Invoke();
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task FlushInterval_HoldsAPartFullBatchUntilTheIntervalPasses()
    {
        FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        using RecordingProvider provider = new(Options(o =>
        {
            o.TimeProvider = time;
            o.MaxBatchSize = 10;
            o.FlushInterval = TimeSpan.FromSeconds(2);
        }));
        ILogger logger = provider.CreateLogger("T");
        logger.LogInformation("one");
        logger.LogInformation("two");
        logger.LogInformation("three");

        await Task.Delay(200, TestContext.Current.CancellationToken);
        Assert.Empty(provider.All);

        // The writer may not have started its timer yet, so keep moving the clock until it fires.
        await WaitUntil(() => provider.All.Count == 3, () => time.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal(["one", "two", "three"], Assert.Single(provider.Batches).Select(e => e.Message));
    }

    [Fact]
    public async Task FlushInterval_WritesAFullBatchWithoutWaiting()
    {
        using RecordingProvider provider = new(Options(o =>
        {
            o.MaxBatchSize = 5;
            o.FlushInterval = TimeSpan.FromHours(1);
        }));
        ILogger logger = provider.CreateLogger("T");
        for (int i = 0; i < 5; i++)
        {
            logger.LogInformation("line {Index}", i);
        }

        await WaitUntil(() => provider.All.Count == 5);
        Assert.Single(provider.Batches);
    }

    [Fact]
    public void FlushInterval_DisposeWritesWithoutWaitingForIt()
    {
        RecordingProvider provider = new(Options(o =>
        {
            o.FlushInterval = TimeSpan.FromHours(1);
            o.ShutdownTimeout = TimeSpan.FromSeconds(30);
        }));
        provider.CreateLogger("T").LogInformation("last words");
        DateTime started = DateTime.UtcNow;
        provider.Dispose();

        Assert.Equal("last words", Assert.Single(provider.All).Message);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
        Assert.True(provider.Closed);
    }

    [Theory]
    [InlineData(LogWriterThread.ThreadPool)]
    [InlineData(LogWriterThread.Dedicated)]
    [InlineData(LogWriterThread.Shared)]
    public void EveryWriterThread_WritesEverythingAndCloses(LogWriterThread mode)
    {
        RecordingProvider provider = new(Options(o =>
        {
            o.WriterThread = mode;
            o.MaxBatchSize = 7;
        })) { AwaitInWrite = true };
        ILogger logger = provider.CreateLogger("T");
        for (int i = 0; i < 50; i++)
        {
            logger.LogInformation("line {Index}", i);
        }
        provider.Dispose();

        Assert.Equal(Enumerable.Range(0, 50).Select(i => $"line {i}"), provider.All.Select(e => e.Message));
        Assert.True(provider.Closed);
    }

    [Fact]
    public void ThreadPool_IsTheDefaultAndUsesPoolThreads()
    {
        Assert.Equal(LogWriterThread.ThreadPool, new BatchingLoggerOptions().WriterThread);
        RecordingProvider provider = new(Options());
        provider.CreateLogger("T").LogInformation("hello");
        provider.Dispose();

        Assert.All(provider.OnPool, Assert.True);
    }

    [Fact]
    public void Dedicated_RunsOnItsOwnThreadEvenAfterAwaitingAndStopsItOnDispose()
    {
        RecordingProvider provider = new(Options(o => o.WriterThread = LogWriterThread.Dedicated)) { AwaitInWrite = true };
        ILogger logger = provider.CreateLogger("T");
        for (int i = 0; i < 3; i++)
        {
            logger.LogInformation("line {Index}", i);
        }
        provider.Dispose();

        Thread thread = provider.Threads[0];
        Assert.Equal("Niddy log writer (RecordingProvider)", thread.Name);
        Assert.All(provider.OnPool, Assert.False);
        Assert.True(provider.AllOnBackgroundThreads);
        Assert.All(provider.Threads, t => Assert.Same(thread, t));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The dedicated thread should stop once the provider is disposed.");
    }

    [Fact]
    public void Shared_UsesOneThreadForEveryProviderAndStopsItWithTheLast()
    {
        RecordingProvider first = new(Options(o => o.WriterThread = LogWriterThread.Shared)) { AwaitInWrite = true };
        RecordingProvider second = new(Options(o => o.WriterThread = LogWriterThread.Shared)) { AwaitInWrite = true };
        first.CreateLogger("A").LogInformation("from first");
        second.CreateLogger("B").LogInformation("from second");
        first.Dispose();

        Thread thread = first.Threads[0];
        Assert.Equal("Niddy log writer (shared)", thread.Name);
        Assert.All(first.Threads, t => Assert.Same(thread, t));
        Assert.False(thread.Join(TimeSpan.FromMilliseconds(100)), "The shared thread should keep running while a provider uses it.");

        second.CreateLogger("B").LogInformation("still running");
        second.Dispose();
        Assert.Equal(["from second", "still running"], second.All.Select(e => e.Message));
        Assert.All(second.Threads, t => Assert.Same(thread, t));
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "The shared thread should stop with the last provider.");
    }

    [Fact]
    public async Task Dedicated_WorksWithFlushInterval()
    {
        FakeTimeProvider time = new(new DateTimeOffset(2026, 10, 7, 9, 0, 0, TimeSpan.Zero));
        using RecordingProvider provider = new(Options(o =>
        {
            o.TimeProvider = time;
            o.WriterThread = LogWriterThread.Dedicated;
            o.FlushInterval = TimeSpan.FromSeconds(2);
        }));
        provider.CreateLogger("T").LogInformation("held");

        await WaitUntil(() => provider.All.Count == 1, () => time.Advance(TimeSpan.FromSeconds(1)));
        Assert.Equal("Niddy log writer (RecordingProvider)", provider.Threads[0].Name);
    }

    [Fact]
    public void Constructor_RejectsAZeroBatchSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RecordingProvider(Options(o => o.MaxBatchSize = 0)));
    }

    [Fact]
    public void AddOwnedProvider_DisposingTheFactoryDisposesTheProvider()
    {
        RecordingProvider? provider = null;
        ILoggerFactory factory = NiddyLogging.CreateFactory(b => b.AddOwnedProvider(_ => provider = new RecordingProvider(Options())));
        factory.CreateLogger("T").LogInformation("hello");
        factory.Dispose();

        Assert.NotNull(provider);
        Assert.True(provider.Closed);
        Assert.Equal("hello", Assert.Single(provider.All).Message);
    }

    [Fact]
    public void CreateFactory_WithoutADirectoryAppliesTheMinimumLevel()
    {
        RecordingProvider? provider = null;
        ILoggerFactory factory = NiddyLogging.CreateFactory(
            b => b.AddOwnedProvider(_ => provider = new RecordingProvider(Options(o => o.MinimumLevel = LogLevel.Trace))),
            LogLevel.Warning);
        ILogger logger = factory.CreateLogger("T");
        logger.LogInformation("filtered by the factory");
        logger.LogWarning("kept");
        factory.Dispose();

        Assert.Equal("kept", Assert.Single(provider!.All).Message);
    }
}
