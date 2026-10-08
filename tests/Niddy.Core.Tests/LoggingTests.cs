using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Niddy.Core.Logging;
using Niddy.Core.Logging.Batching;
using Niddy.Core.Logging.Files;

namespace Niddy.Core.Tests;

public sealed class FileLoggerTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-log-").FullName;
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 9, 26, 8, 30, 0, TimeSpan.Zero));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private FileLoggerProvider CreateProvider(Action<FileLoggerOptions>? configure = null)
    {
        FileLoggerOptions options = new() { Directory = _dir, UseUtc = true, TimeProvider = _time, FileNamePrefix = "app" };
        configure?.Invoke(options);
        return new FileLoggerProvider(options);
    }

    [Fact]
    public void Log_WritesFormattedLinesAboveTheMinimumLevel()
    {
        using (FileLoggerProvider provider = CreateProvider())
        {
            ILogger logger = provider.CreateLogger("Tests.Sync");
            logger.LogDebug("hidden");
            logger.LogInformation("Synced {Count} items", 12);
            logger.LogError(new InvalidOperationException("boom"), "Failed");
        }

        string text = File.ReadAllText(Path.Combine(_dir, "app-20260926.log"));
        Assert.DoesNotContain("hidden", text);
        Assert.Contains("2026-09-26 08:30:00.000 +00:00 [INF] Tests.Sync: Synced 12 items", text);
        Assert.Contains("[ERR] Tests.Sync: Failed", text);
        Assert.Contains("System.InvalidOperationException: boom", text);
    }

    [Fact]
    public void Log_StartsANewFileEachDay()
    {
        using (FileLoggerProvider provider = CreateProvider())
        {
            ILogger logger = provider.CreateLogger("T");
            logger.LogInformation("day one");
            _time.Advance(TimeSpan.FromDays(1));
            logger.LogInformation("day two");
        }

        Assert.Contains("day one", File.ReadAllText(Path.Combine(_dir, "app-20260926.log")));
        Assert.Contains("day two", File.ReadAllText(Path.Combine(_dir, "app-20260927.log")));
    }

    [Fact]
    public void Log_RollsBySizeAndDeletesOldFiles()
    {
        using (FileLoggerProvider provider = CreateProvider(o =>
        {
            o.MaxFileSizeBytes = 200;
            o.RetainedFileCount = 2;
        }))
        {
            ILogger logger = provider.CreateLogger("T");
            for (int i = 0; i < 10; i++)
            {
                logger.LogInformation("line {Index} {Padding}", i, new string('x', 80));
            }
        }

        string?[] files = Directory.GetFiles(_dir, "app-*.log").Select(Path.GetFileName).Order().ToArray();
        Assert.Equal(2, files.Length);
        Assert.Contains("line 9", File.ReadAllText(Path.Combine(_dir, files.OrderByDescending(f => f!.Length).ThenByDescending(f => f).First()!)));
    }

    [Fact]
    public void Scopes_AreIncludedWhenEnabled()
    {
        using (ILoggerFactory factory = LoggerFactory.Create(b => b.AddFile(_dir, o =>
        {
            o.IncludeScopes = true;
            o.UseUtc = true;
            o.TimeProvider = _time;
            o.FileNamePrefix = "scoped";
        })))
        {
            ILogger logger = factory.CreateLogger("T");
            using (logger.BeginScope("Request {Id}", 7))
            {
                logger.LogWarning("slow");
            }
        }

        Assert.Contains("[WRN] T: slow => Request 7", File.ReadAllText(Path.Combine(_dir, "scoped-20260926.log")));
    }

    [Fact]
    public void AddFile_DisposingTheFactoryWritesEveryQueuedLine()
    {
        using (ILoggerFactory factory = LoggerFactory.Create(b => b.AddFile(_dir, o =>
        {
            o.UseUtc = true;
            o.TimeProvider = _time;
            o.FileNamePrefix = "queued";
        })))
        {
            ILogger logger = factory.CreateLogger("T");
            for (int i = 0; i < 500; i++)
            {
                logger.LogInformation("line {Index}", i);
            }
        }

        Assert.Equal(500, File.ReadAllLines(Path.Combine(_dir, "queued-20260926.log")).Length);
    }

    [Fact]
    public void AddFile_RejectsABlankDirectory()
    {
        Assert.Throws<ArgumentException>(() => LoggerFactory.Create(b => b.AddFile(" ")));
    }
}
