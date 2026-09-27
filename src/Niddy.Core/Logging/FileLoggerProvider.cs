using System.Globalization;
using System.Text;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Niddy.Logging;

/// <summary>
/// An <see cref="ILoggerProvider" /> that writes to daily, size-capped files, deleting old ones. Lines look like
/// <c>2026-09-26 14:03:12.345 +01:00 [INF] MyApp.Sync: Synced 12 items</c>, followed by the exception, if any.
/// </summary>
/// <remarks>
/// Logging never blocks on disk: lines are queued and written by a background task. Disposing the provider (which
/// disposing the <see cref="ILoggerFactory" /> does) writes everything still queued, so dispose it on exit. If writing
/// fails, e.g. because the disk is full, lines are dropped rather than crashing the app.
/// </remarks>
/// <example>
/// <code>
/// using var loggerFactory = LoggerFactory.Create(b =&gt; b.AddFile(paths.Logs));
/// </code>
/// </example>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : ILoggerProvider, IDisposable, ISupportExternalScope
{
    private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return provider._scopeProvider?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None && logLevel >= provider.Options.MinimumLevel && Volatile.Read(in provider._disposed) == 0;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
            {
                string text = formatter(state, exception);
                if (!string.IsNullOrEmpty(text) || exception != null)
                {
                    provider.Enqueue(logLevel, category, text, exception);
                }
            }
        }
    }

    private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    private readonly Task _writer;

    private IExternalScopeProvider? _scopeProvider;

    private StreamWriter? _stream;

    private string? _currentPath;

    private DateOnly _currentDate;

    private int _disposed;

    /// <summary>The settings this provider was created with.</summary>
    public FileLoggerOptions Options { get; }

    /// <summary>The full path of the folder log files are written to.</summary>
    public string Directory { get; }

    /// <summary>The file currently being written to, or null before the first line.</summary>
    public string? CurrentFile => Volatile.Read(in _currentPath);

    /// <summary>Creates a provider that writes to <see cref="Directory" />.</summary>
    public FileLoggerProvider(FileLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory);
        Options = options;
        Directory = System.IO.Directory.CreateDirectory(options.Directory).FullName;
        _writer = Task.Run((Func<Task?>)WriteLoopAsync);
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    /// <summary>Stops accepting lines and waits (up to five seconds) until the queued ones are written.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }
        _queue.Writer.TryComplete();
        try
        {
            _writer.Wait(TimeSpan.FromSeconds(5L));
        }
        catch (AggregateException)
        {
        }
    }

    internal static string LevelName(LogLevel level)
    {
        string result = level switch
        {
            LogLevel.Trace => "TRC", 
            LogLevel.Debug => "DBG", 
            LogLevel.Information => "INF", 
            LogLevel.Warning => "WRN", 
            LogLevel.Error => "ERR", 
            LogLevel.Critical => "CRT", 
            _ => "???", 
        };
        return result;
    }

    private DateTimeOffset Now()
    {
        DateTimeOffset utcNow = Options.TimeProvider.GetUtcNow();
        return Options.UseUtc ? utcNow : utcNow.ToOffset(Options.TimeProvider.LocalTimeZone.GetUtcOffset(utcNow));
    }

    private void Enqueue(LogLevel level, string category, string message, Exception? exception)
    {
        StringBuilder stringBuilder = new StringBuilder(message.Length + category.Length + 48);
        DateTimeOffset dateTimeOffset = Now();
        stringBuilder.Append(dateTimeOffset.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture)).Append(" [").Append(LevelName(level))
            .Append("] ")
            .Append(category)
            .Append(": ")
            .Append(message);
        if (Options.IncludeScopes)
        {
            _scopeProvider?.ForEachScope((scope, builder) =>
            {
                builder.Append(" => ").Append(scope);
            }, stringBuilder);
        }
        if (exception != null)
        {
            stringBuilder.AppendLine().Append(exception);
        }
        stringBuilder.AppendLine();
        _queue.Writer.TryWrite(dateTimeOffset.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + stringBuilder);
    }

    private async Task WriteLoopAsync()
    {
        ChannelReader<string> reader = _queue.Reader;
        while (await reader.WaitToReadAsync().ConfigureAwait(continueOnCapturedContext: false))
        {
            string entry;
            while (reader.TryRead(out entry))
            {
                try
                {
                    DateOnly date = DateOnly.ParseExact(entry.AsSpan(0, 8), "yyyyMMdd".AsSpan(), CultureInfo.InvariantCulture);
                    StreamWriter stream = GetStream(date, entry.Length - 8);
                    await stream.WriteAsync(entry.AsMemory(8)).ConfigureAwait(continueOnCapturedContext: false);
                }
                catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
                {
                    CloseStream();
                }
            }
            try
            {
                if (_stream != null)
                {
                    await _stream.FlushAsync().ConfigureAwait(continueOnCapturedContext: false);
                }
            }
            catch (Exception ex2) when (((ex2 is IOException || ex2 is UnauthorizedAccessException) ? 1 : 0) != 0)
            {
                CloseStream();
            }
        }
        CloseStream();
    }

    private StreamWriter GetStream(DateOnly date, int nextLength)
    {
        long maxFileSizeBytes = Options.MaxFileSizeBytes;
        if (_stream != null && date == _currentDate && (maxFileSizeBytes <= 0 || _stream.BaseStream.Length == 0L || _stream.BaseStream.Length + nextLength <= maxFileSizeBytes))
        {
            return _stream;
        }
        CloseStream();
        string text = NextPath(date, nextLength);
        FileStream stream = new FileStream(text, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        _stream = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        _currentDate = date;
        Volatile.Write(ref _currentPath, text);
        DeleteOldFiles();
        return _stream;
    }

    private string NextPath(DateOnly date, int nextLength)
    {
        string text = $"{Options.FileNamePrefix}-{date:yyyyMMdd}";
        int num = 0;
        string text2;
        while (true)
        {
            text2 = Path.Combine(Directory, (num == 0) ? (text + ".log") : $"{text}_{num}.log");
            FileInfo fileInfo = new FileInfo(text2);
            if (!fileInfo.Exists || Options.MaxFileSizeBytes <= 0 || fileInfo.Length == 0L || fileInfo.Length + nextLength <= Options.MaxFileSizeBytes)
            {
                break;
            }
            num++;
        }
        return text2;
    }

    private void DeleteOldFiles()
    {
        if (Options.RetainedFileCount <= 0)
        {
            return;
        }
        try
        {
            IEnumerable<FileInfo> enumerable = (from f in (from f in new DirectoryInfo(Directory).GetFiles(Options.FileNamePrefix + "-*.log")
                    orderby f.LastWriteTimeUtc descending
                    select f).ThenByDescending<FileInfo, string>((FileInfo f) => f.Name, StringComparer.Ordinal)
                where !string.Equals(f.FullName, _currentPath, StringComparison.Ordinal)
                select f).Skip(Options.RetainedFileCount - 1);
            foreach (FileInfo item in enumerable)
            {
                item.Delete();
            }
        }
        catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
        {
        }
    }

    private void CloseStream()
    {
        try
        {
            _stream?.Dispose();
        }
        catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
        {
        }
        _stream = null;
    }
}
