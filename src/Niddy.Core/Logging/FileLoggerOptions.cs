using Microsoft.Extensions.Logging;

namespace Niddy.Logging;

/// <summary>Settings for <see cref="FileLoggerProvider"/>.</summary>
public sealed class FileLoggerOptions
{
    /// <summary>The folder log files are written to. It's created if needed.</summary>
    public required string Directory { get; set; }

    /// <summary>The start of each file's name; files are named <c>{prefix}-{yyyyMMdd}.log</c>. Defaults to <c>"log"</c>.</summary>
    public string FileNamePrefix { get; set; } = "log";

    /// <summary>The least severe level written. Defaults to <see cref="LogLevel.Information"/>.</summary>
    public LogLevel MinimumLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// The size a file may grow to before a new one is started for the same day (<c>{prefix}-{yyyyMMdd}_1.log</c>, and
    /// so on). Defaults to 10 MB; zero or less means no limit.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>How many log files to keep; older ones are deleted when a new file is started. Defaults to 7; zero or less keeps all.</summary>
    public int RetainedFileCount { get; set; } = 7;

    /// <summary>Whether to include the active logging scopes in each line. Defaults to false.</summary>
    public bool IncludeScopes { get; set; }

    /// <summary>Whether to use UTC timestamps and file dates instead of local time. Defaults to false.</summary>
    public bool UseUtc { get; set; }

    /// <summary>The clock used for timestamps and daily files. Defaults to the system clock; replace it in tests.</summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
