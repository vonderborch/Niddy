using Niddy.Core.Logging.Batching;

namespace Niddy.Core.Logging.Files;

/// <summary>
/// Settings for <see cref="FileLoggerProvider"/>. The level, scopes, clock, batching and shutdown settings come from
/// <see cref="BatchingLoggerOptions"/>.
/// </summary>
public sealed class FileLoggerOptions : BatchingLoggerOptions
{
    /// <summary>The folder log files are written to. It's created if needed.</summary>
    public required string Directory { get; set; }

    /// <summary>The start of each file's name; files are named <c>{prefix}-{yyyyMMdd}.log</c>. Defaults to <c>"log"</c>.</summary>
    public string FileNamePrefix { get; set; } = "log";

    /// <summary>
    /// The size a file may grow to before a new one is started for the same day (<c>{prefix}-{yyyyMMdd}_1.log</c>, and
    /// so on). Defaults to 10 MB; zero or less means no limit.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>How many log files to keep; older ones are deleted when a new file is started. Defaults to 7; zero or less keeps all.</summary>
    public int RetainedFileCount { get; set; } = 7;
}
