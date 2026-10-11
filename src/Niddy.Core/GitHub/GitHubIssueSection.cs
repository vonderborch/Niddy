using System.Net;
using System.Text;

namespace Niddy.Core.GitHub;

/// <summary>
/// A titled block of extra content in a <see cref="GitHubIssue"/>, such as a log or a stack trace. By default it's
/// collapsed (a <c>&lt;details&gt;</c> block the reader expands), so long content doesn't swamp the issue. When the
/// issue body is too long for GitHub, sections are cut before anything else.
/// </summary>
/// <example>
/// <code>
/// GitHubIssue issue = GitHubIssue.Bug("Sync fails") with
/// {
///     Sections = [GitHubIssueSection.FromLatestLogFile(paths.Logs), GitHubIssueSection.Code("Config", json)],
/// };
/// </code>
/// </example>
public sealed record GitHubIssueSection
{
    /// <summary>The note that replaces content cut to fit GitHub's limits.</summary>
    internal const string CutNote = "… (cut)";

    /// <summary>The heading, or the summary line the reader clicks to expand a collapsed section.</summary>
    public required string Title { get; init; }

    /// <summary>The content: Markdown, or plain text if <see cref="IsCode"/> is set.</summary>
    public required string Content { get; init; }

    /// <summary>Whether to show <see cref="Content"/> as a code block, as written, rather than as Markdown.</summary>
    public bool IsCode { get; init; }

    /// <summary>Whether the section starts collapsed. Default true.</summary>
    public bool IsCollapsed { get; init; } = true;

    /// <summary>
    /// Whether to keep the end of <see cref="Content"/>, rather than the start, when it has to be cut. Set it for logs,
    /// where the latest lines matter most.
    /// </summary>
    public bool KeepEnd { get; init; }

    /// <summary>A section of Markdown.</summary>
    /// <param name="title">The heading or summary line.</param>
    /// <param name="markdown">The content, in Markdown.</param>
    /// <param name="collapsed">Whether it starts collapsed.</param>
    public static GitHubIssueSection Text(string title, string markdown, bool collapsed = true) =>
        new() { Title = title, Content = markdown, IsCollapsed = collapsed };

    /// <summary>A section shown as a code block, e.g. a config file or a stack trace.</summary>
    /// <param name="title">The heading or summary line.</param>
    /// <param name="content">The content, shown as written.</param>
    /// <param name="collapsed">Whether it starts collapsed.</param>
    public static GitHubIssueSection Code(string title, string content, bool collapsed = true) =>
        new() { Title = title, Content = content, IsCode = true, IsCollapsed = collapsed };

    /// <summary>
    /// A collapsed code section with the last <paramref name="lines"/> lines of a log file, read even while a logger
    /// is writing to it. It never throws for a missing or unreadable file; the section says what went wrong
    /// instead, so it's safe in a crash handler.
    /// </summary>
    /// <param name="path">The log file.</param>
    /// <param name="lines">How many lines from the end to include. At most the last 64 KB are read.</param>
    /// <param name="title">The summary line. Null uses the file name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lines"/> is less than 1.</exception>
    public static GitHubIssueSection FromLogFile(string path, int lines = 100, string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfLessThan(lines, 1);
        string content;
        try
        {
            content = ReadLastLines(path, lines);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            content = $"Couldn't read {Path.GetFileName(path)}: {ex.Message}";
        }

        return new GitHubIssueSection
        {
            Title = title ?? Path.GetFileName(path), Content = content, IsCode = true, KeepEnd = true,
        };
    }

    /// <summary>
    /// <see cref="FromLogFile"/> for the most recently written file in <paramref name="directory"/>, e.g. the current
    /// file of a rolling file logger. Like <see cref="FromLogFile"/>, it never throws for a missing or unreadable
    /// directory or file.
    /// </summary>
    /// <param name="directory">The log folder, e.g. <c>AppPaths.Logs</c>.</param>
    /// <param name="lines">How many lines from the end to include.</param>
    /// <param name="searchPattern">Which files count as logs. Default <c>*.log</c>.</param>
    /// <param name="title">The summary line. Null uses the file name.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lines"/> is less than 1.</exception>
    public static GitHubIssueSection FromLatestLogFile(string directory, int lines = 100, string searchPattern = "*.log",
        string? title = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentOutOfRangeException.ThrowIfLessThan(lines, 1);
        FileInfo? latest;
        try
        {
            latest = new DirectoryInfo(directory).EnumerateFiles(searchPattern)
                .MaxBy(file => file.LastWriteTimeUtc);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return Code(title ?? "Log", $"Couldn't read the log folder: {ex.Message}");
        }

        return latest is null
            ? Code(title ?? "Log", $"No {searchPattern} files in the log folder.")
            : FromLogFile(latest.FullName, lines, title);
    }

    /// <summary>The section as Markdown, with its content cut to <paramref name="contentLength"/> characters if set.</summary>
    internal string Format(int? contentLength = null)
    {
        string content = this.Content.TrimEnd();
        if (contentLength is int max && content.Length > max)
        {
            content = Cut(content, max, this.KeepEnd);
        }

        string block = this.IsCode ? CodeBlock(content) : content;
        string title = this.Title.Trim();
        return this.IsCollapsed
            ? $"<details>\n<summary>{WebUtility.HtmlEncode(title)}</summary>\n\n{block}\n\n</details>"
            : $"### {title}\n\n{block}";
    }

    /// <summary><paramref name="text"/> in a fenced code block whose fence is longer than any run of backticks in it.</summary>
    internal static string CodeBlock(string text)
    {
        int longest = 0;
        int run = 0;
        foreach (char c in text)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }

        string fence = new('`', Math.Max(3, longest + 1));
        return $"{fence}\n{text}\n{fence}";
    }

    /// <summary>The start or end of <paramref name="text"/> with a note, <paramref name="max"/> characters at most.</summary>
    internal static string Cut(string text, int max, bool keepEnd)
    {
        int keep = max - CutNote.Length - 1;
        if (keep <= 0)
        {
            return max >= CutNote.Length ? CutNote : string.Empty;
        }

        if (keepEnd)
        {
            int start = text.Length - keep;
            if (char.IsLowSurrogate(text[start]))
            {
                start++;
            }

            return CutNote + "\n" + text[start..];
        }

        if (char.IsHighSurrogate(text[keep - 1]))
        {
            keep--;
        }

        return text[..keep] + "\n" + CutNote;
    }

    private static string ReadLastLines(string path, int lines)
    {
        const int MaxBytes = 64 * 1024;
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        long start = Math.Max(0, stream.Length - MaxBytes);
        stream.Seek(start, SeekOrigin.Begin);
        using StreamReader reader = new(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: start == 0);
        string text = reader.ReadToEnd();

        string[] all = text.ReplaceLineEndings("\n").TrimEnd('\n').Split('\n');
        // Reading from the middle of the file starts partway through a line; drop it.
        int skip = start > 0 && all.Length > 1 ? 1 : 0;
        return string.Join("\n", all.Skip(skip).TakeLast(lines));
    }
}
