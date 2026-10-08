using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using Niddy.Core.Logging.Batching;

namespace Niddy.Core.Logging.Files;

/// <summary>
/// An <see cref="ILoggerProvider" /> that writes to daily, size-capped files, deleting old ones. Lines look like
/// <c>2026-09-26 14:03:12.345 +01:00 [INF] MyApp.Sync: Synced 12 items</c>, followed by the exception, if any.
/// </summary>
/// <remarks>
/// Logging never blocks on disk: lines are queued and written by a background task (see
/// <see cref="BatchingLoggerProvider{TOptions}"/>). Disposing the provider (which disposing the
/// <see cref="ILoggerFactory" /> does) writes everything still queued, so dispose it on exit. If writing fails, e.g.
/// because the disk is full, lines are dropped rather than crashing the app.
/// </remarks>
/// <example>
/// <code>
/// using ILoggerFactory loggerFactory = LoggerFactory.Create(b =&gt; b.AddFile(paths.Logs));
/// </code>
/// </example>
[ProviderAlias("File")]
public sealed class FileLoggerProvider : BatchingLoggerProvider<FileLoggerOptions>
{
    private StreamWriter? _stream;

    private string? _currentPath;

    private DateOnly _currentDate;

    /// <summary>The full path of the folder log files are written to.</summary>
    public string Directory { get; }

    /// <summary>The file currently being written to, or null before the first line.</summary>
    public string? CurrentFile => Volatile.Read(in this._currentPath);

    /// <summary>Creates a provider that writes to <see cref="Directory" />.</summary>
    /// <param name="options">The settings; <see cref="FileLoggerOptions.Directory"/> is required.</param>
    public FileLoggerProvider(FileLoggerOptions options)
        : base(Validate(options))
    {
        this.Directory = System.IO.Directory.CreateDirectory(options.Directory).FullName;
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

    // Writes are synchronous on purpose. Async writes to a FileStream opened for synchronous I/O are handed to the
    // thread pool, which would undo a Dedicated or Shared writer thread; local disk writes are short anyway.

    /// <inheritdoc />
    protected override Task WriteBatchAsync(IReadOnlyList<LogEntry> batch, CancellationToken cancellationToken)
    {
        foreach (LogEntry entry in batch)
        {
            // One bad write (e.g. a full disk) drops that line and reopens the file for the next, not the whole batch.
            try
            {
                string line = Format(entry);
                StreamWriter stream = GetStream(DateOnly.FromDateTime(entry.Timestamp.DateTime), line.Length);
                stream.Write(line);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                CloseStream();
            }
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            this._stream?.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            CloseStream();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task CloseAsync()
    {
        CloseStream();
        return Task.CompletedTask;
    }

    private static FileLoggerOptions Validate(FileLoggerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.Directory);
        return options;
    }

    private static string Format(LogEntry entry)
    {
        StringBuilder builder = new(entry.Message.Length + entry.Category.Length + 48);
        builder.Append(entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture))
            .Append(" [").Append(LevelName(entry.Level)).Append("] ")
            .Append(entry.Category)
            .Append(": ")
            .Append(entry.Message);
        foreach (object? scope in entry.Scopes)
        {
            builder.Append(" => ").Append(scope);
        }
        if (entry.Exception != null)
        {
            builder.AppendLine().Append(entry.Exception);
        }
        builder.AppendLine();
        return builder.ToString();
    }

    private StreamWriter GetStream(DateOnly date, int nextLength)
    {
        long maxFileSizeBytes = this.Options.MaxFileSizeBytes;
        if (this._stream != null && date == this._currentDate && (maxFileSizeBytes <= 0 || this._stream.BaseStream.Length == 0L || this._stream.BaseStream.Length + nextLength <= maxFileSizeBytes))
        {
            return this._stream;
        }
        CloseStream();
        string path = NextPath(date, nextLength);
        FileStream stream = new(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        this._stream = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        this._currentDate = date;
        Volatile.Write(ref this._currentPath, path);
        DeleteOldFiles();
        return this._stream;
    }

    private string NextPath(DateOnly date, int nextLength)
    {
        string name = $"{this.Options.FileNamePrefix}-{date:yyyyMMdd}";
        for (int index = 0; ; index++)
        {
            string path = Path.Combine(this.Directory, index == 0 ? name + ".log" : $"{name}_{index}.log");
            FileInfo file = new(path);
            // Use the first file with room for the next entry. An empty file always has room, so one big entry
            // can't run the loop forever.
            if (!file.Exists || this.Options.MaxFileSizeBytes <= 0 || file.Length == 0L || file.Length + nextLength <= this.Options.MaxFileSizeBytes)
            {
                return path;
            }
        }
    }

    private void DeleteOldFiles()
    {
        if (this.Options.RetainedFileCount <= 0)
        {
            return;
        }
        try
        {
            IEnumerable<FileInfo> enumerable = (from f in (from f in new DirectoryInfo(this.Directory).GetFiles(this.Options.FileNamePrefix + "-*.log")
                    orderby f.LastWriteTimeUtc descending
                    select f).ThenByDescending<FileInfo, string>((FileInfo f) => f.Name, StringComparer.Ordinal)
                where !string.Equals(f.FullName, this._currentPath, StringComparison.Ordinal)
                select f).Skip(this.Options.RetainedFileCount - 1);
            foreach (FileInfo item in enumerable)
            {
                item.Delete();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void CloseStream()
    {
        try
        {
            this._stream?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
        this._stream = null;
    }
}
