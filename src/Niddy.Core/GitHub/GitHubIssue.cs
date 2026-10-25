using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Niddy.Core.SystemInfo;

namespace Niddy.Core.GitHub;

/// <summary>
/// The contents of a GitHub issue: a bug report, a feature request or anything else. Pass it to
/// <see cref="GitHubIssues.CreateAsync"/> to open it through the API, or to <see cref="GitHubIssues.OpenInBrowser"/>
/// to let the user review and submit it themselves.
/// </summary>
/// <example>
/// <code>
/// GitHubIssue issue = GitHubIssue.Bug("Crash when saving", "Steps: open a file, press Save.") with
/// {
///     Reporter = new GitHubIssueReporter(Name: "Jane Doe", Email: "jane@example.com"),
///     Details = new Dictionary&lt;string, string&gt; { ["File size"] = "12 MB" },
///     IncludeSystemInfo = true,
/// };
/// </code>
/// </example>
public sealed record GitHubIssue
{
    private const int MaxGeneratedTitleLength = 120;

    /// <summary>The issue title. Required, and can't be blank.</summary>
    public required string Title { get; init; }

    /// <summary>The issue text, in GitHub-flavored Markdown. Optional.</summary>
    public string? Body { get; init; }

    /// <summary>
    /// Label names to add, e.g. <c>bug</c>. Through the API GitHub silently drops them unless the token has push
    /// access to the repository; labels that don't exist yet are created.
    /// </summary>
    public IReadOnlyList<string> Labels { get; init; } = [];

    /// <summary>
    /// Usernames to assign. Through the API GitHub silently drops them unless the token has push access to the
    /// repository.
    /// </summary>
    public IReadOnlyList<string> Assignees { get; init; } = [];

    /// <summary>
    /// The number of a milestone to add the issue to. Needs push access, like <see cref="Labels"/>. Not included in
    /// <see cref="GitHubIssues.GetNewIssueUrl"/>, whose form takes milestone names.
    /// </summary>
    public int? Milestone { get; init; }

    /// <summary>
    /// The issue type, e.g. <c>Bug</c> or <c>Feature</c>, for organizations that have issue types set up. Null leaves it
    /// unset.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>Who is reporting the issue, shown in a "Reported by" line. Optional.</summary>
    public GitHubIssueReporter? Reporter { get; init; }

    /// <summary>Extra name/value pairs, shown as a table under the body, e.g. the document that failed to open.</summary>
    public IReadOnlyDictionary<string, string>? Details { get; init; }

    /// <summary>
    /// Titled blocks of extra content, such as logs, shown after <see cref="Body"/> and collapsed by default. When the
    /// body is too long for GitHub, these are cut first.
    /// </summary>
    public IReadOnlyList<GitHubIssueSection> Sections { get; init; } = [];

    /// <summary>
    /// Whether to add the app's name and version, the OS and its version, the architecture and the .NET runtime to the
    /// details table. Nothing identifying, such as the machine or user name, is included.
    /// </summary>
    public bool IncludeSystemInfo { get; init; }

    /// <summary>
    /// Identifies the problem, so that reports of the same one go to one issue. When it's set,
    /// <see cref="GitHubIssues.CreateAsync"/> first looks for an open issue with the same fingerprint and, if there is
    /// one, adds the report to it as a comment instead of opening another. Any text works, e.g. <c>export-crash</c>;
    /// only a hash of it goes in the issue, as a hidden marker at the end of the body. <see cref="FromException"/> sets
    /// it to the exception's type and where it was thrown. Null, the default, always opens a new issue.
    /// </summary>
    public string? Fingerprint { get; init; }

    /// <summary>
    /// The word the hidden marker carries for <see cref="Fingerprint"/>, searched for to find the issue again: <c>nfp</c>
    /// and 16 hex digits of its hash. Null when there's no fingerprint.
    /// </summary>
    internal string? FingerprintToken => string.IsNullOrWhiteSpace(this.Fingerprint)
        ? null
        : "nfp" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(this.Fingerprint.Trim())).AsSpan(0, 8));

    /// <summary>A bug report: <paramref name="title"/> and <paramref name="body"/> with the <c>bug</c> label.</summary>
    /// <param name="title">The issue title.</param>
    /// <param name="body">The issue text, in Markdown.</param>
    public static GitHubIssue Bug(string title, string? body = null) =>
        new() { Title = title, Body = body, Labels = ["bug"] };

    /// <summary>A feature request: <paramref name="title"/> and <paramref name="body"/> with the <c>enhancement</c> label.</summary>
    /// <param name="title">The issue title.</param>
    /// <param name="body">The issue text, in Markdown.</param>
    public static GitHubIssue FeatureRequest(string title, string? body = null) =>
        new() { Title = title, Body = body, Labels = ["enhancement"] };

    /// <summary>A question: <paramref name="title"/> and <paramref name="body"/> with the <c>question</c> label.</summary>
    /// <param name="title">The issue title.</param>
    /// <param name="body">The issue text, in Markdown.</param>
    public static GitHubIssue Question(string title, string? body = null) =>
        new() { Title = title, Body = body, Labels = ["question"] };

    /// <summary>
    /// A documentation problem: <paramref name="title"/> and <paramref name="body"/> with the <c>documentation</c>
    /// label.
    /// </summary>
    /// <param name="title">The issue title.</param>
    /// <param name="body">The issue text, in Markdown.</param>
    public static GitHubIssue Documentation(string title, string? body = null) =>
        new() { Title = title, Body = body, Labels = ["documentation"] };

    /// <summary>
    /// A crash report for <paramref name="exception"/>: the <c>bug</c> label, system info, and the full exception
    /// (type, message, stack trace and inner exceptions) in an "Exception" section. The title is the exception's
    /// type and the first line of its message, after unwrapping a <see cref="TargetInvocationException"/> or an
    /// <see cref="AggregateException"/> with one inner exception. The <see cref="Fingerprint"/> is that exception's
    /// type and the method it was thrown from, so the same crash goes to one issue however its message varies; set it
    /// to null to always open a new issue.
    /// </summary>
    /// <param name="exception">The exception to report.</param>
    /// <param name="title">The title. Null makes one from the exception.</param>
    /// <param name="body">Text to put before the exception, e.g. what the user was doing.</param>
    /// <remarks>
    /// Exception messages can contain file paths and other details from the user's machine, such as their user name in
    /// a home folder path. Show the issue to the user before sending it to a public repository.
    /// </remarks>
    public static GitHubIssue FromException(Exception exception, string? title = null, string? body = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Exception cause = exception;
        while (cause is TargetInvocationException { InnerException: not null }
               || cause is AggregateException { InnerExceptions.Count: 1 })
        {
            cause = cause.InnerException!;
        }

        string message = cause.Message.ReplaceLineEndings("\n").Split("\n")[0].Trim();
        string generated = message.Length == 0 ? cause.GetType().Name : $"{cause.GetType().Name}: {message}";
        return new GitHubIssue
        {
            Title = title ?? (generated.Length <= MaxGeneratedTitleLength
                ? generated
                : generated[..(MaxGeneratedTitleLength - 1)].TrimEnd() + "…"),
            Body = body,
            Labels = ["bug"],
            IncludeSystemInfo = true,
            Sections = [GitHubIssueSection.Code("Exception", exception.ToString(), collapsed: false)],
            Fingerprint = $"{cause.GetType().FullName} at {ThrownFrom(cause) ?? message}",
        };
    }

    /// <summary>
    /// The Markdown that becomes the issue body: <see cref="Body"/>, then the <see cref="Sections"/>, then a
    /// "Reported by" line for <see cref="Reporter"/> and a table of <see cref="Details"/> and, if
    /// <see cref="IncludeSystemInfo"/> is set, the system info, and last the hidden marker for
    /// <see cref="Fingerprint"/>. With none of those set, it's just <see cref="Body"/>, or an empty string.
    /// </summary>
    public string FormatBody() => this.FormatBody(int.MaxValue);

    /// <summary>
    /// <see cref="FormatBody()"/>, at most <paramref name="maxLength"/> characters. The fingerprint marker is kept unless
    /// there's no room for anything else.
    /// </summary>
    internal string FormatBody(int maxLength)
    {
        if (this.FingerprintToken is not { } token)
        {
            return this.FormatContent(maxLength);
        }

        string marker = $"<!-- fingerprint: {token} -->";
        if (maxLength < marker.Length + 2)
        {
            return this.FormatContent(maxLength);
        }

        string content = this.FormatContent(maxLength - marker.Length - 2);
        return content.Length == 0 ? marker : $"{content}\n\n{marker}";
    }

    /// <summary>
    /// The body without the fingerprint marker, at most <paramref name="maxLength"/> characters. Sections are cut first,
    /// sharing the space evenly so short ones stay whole, then <see cref="Body"/>; the reporter and details are kept.
    /// </summary>
    internal string FormatContent(int maxLength)
    {
        string body = this.Body?.TrimEnd() ?? string.Empty;
        string? footer = this.FormatFooter();
        string full = Compose(body, this.Sections.Select(section => section.Format()), footer);
        if (full.Length <= maxLength)
        {
            return full;
        }

        // Share the space left after everything else between the sections' contents, shortest first.
        int[] lengths = [.. this.Sections.Select(section => section.Content.TrimEnd().Length)];
        int[] allowed = new int[lengths.Length];
        string[] empty = [.. this.Sections.Select(section => section.Format(0))];
        int remaining = maxLength - Compose(body, empty, footer).Length;
        if (remaining >= 0)
        {
            int[] order = [.. Enumerable.Range(0, lengths.Length).OrderBy(i => lengths[i])];
            for (int k = 0; k < order.Length; k++)
            {
                int i = order[k];
                allowed[i] = Math.Min(lengths[i], remaining / (order.Length - k));
                remaining -= allowed[i];
            }

            string fitted = Compose(body, this.Sections.Select((section, i) => section.Format(allowed[i])), footer);
            if (fitted.Length <= maxLength)
            {
                return fitted;
            }
        }

        // Still too long: the sections keep only their titles, and the body is cut.
        int bodyLength = maxLength - Compose(string.Empty, empty, footer).Length - "\n\n".Length;
        string cutBody = bodyLength <= 0 ? string.Empty : GitHubIssueSection.Cut(body, bodyLength, keepEnd: false);
        string result = Compose(cutBody, empty, footer);
        return result.Length <= maxLength ? result : GitHubIssueSection.Cut(result, maxLength, keepEnd: false);
    }

    /// <summary>
    /// The method at the top of the exception's stack trace, e.g. <c>App.Exporter.Save(String path)</c>, or null if it
    /// was never thrown. The "at" before it and the file and line after it are left out: they depend on the language,
    /// the build and the machine.
    /// </summary>
    private static string? ThrownFrom(Exception exception)
    {
        string? line = exception.StackTrace?.ReplaceLineEndings("\n").Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.Length > 0);
        if (line is null)
        {
            return null;
        }

        int start = line.IndexOf(' ') + 1;
        int end = line.IndexOf(')', start);
        return end < 0 ? line[start..] : line[start..(end + 1)];
    }

    private static string Compose(string body, IEnumerable<string> sections, string? footer)
    {
        string main = string.Join("\n\n", sections.Prepend(body).Where(part => part.Length > 0));
        return footer is null ? main : main.Length == 0 ? footer : $"{main}\n\n---\n\n{footer}";
    }

    /// <summary>The "Reported by" line and the details table, or null if there's neither.</summary>
    private string? FormatFooter()
    {
        string? reporter = this.Reporter?.Format();
        List<KeyValuePair<string, string>> details = [.. this.Details ?? new Dictionary<string, string>()];
        if (this.IncludeSystemInfo)
        {
            details.AddRange(GetSystemInfo());
        }

        if (reporter is null && details.Count == 0)
        {
            return null;
        }

        StringBuilder text = new();
        if (reporter is not null)
        {
            text.Append("**Reported by:** ").Append(reporter).Append('\n');
        }

        if (details.Count > 0)
        {
            if (reporter is not null)
            {
                text.Append('\n');
            }

            text.Append("| Detail | Value |\n|---|---|\n");
            foreach ((string name, string value) in details)
            {
                text.Append("| ").Append(EscapeCell(name)).Append(" | ").Append(EscapeCell(value)).Append(" |\n");
            }
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>The app, OS, architecture and runtime, as table rows.</summary>
    internal static IEnumerable<KeyValuePair<string, string>> GetSystemInfo()
    {
        AssemblyName? app = Assembly.GetEntryAssembly()?.GetName();
        if (app?.Name is not null)
        {
            string? version = Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? app.Version?.ToString();
            yield return new("App", version is null ? app.Name : $"{app.Name} {version}");
        }

        string os;
        try
        {
            Platform platform = Platform.Info;
            os = $"{platform.OS} {platform.OSVersion}";
        }
        catch (PlatformNotSupportedException)
        {
            // The app declared supported OSes and this isn't one of them; still worth reporting.
            os = RuntimeInformation.OSDescription;
        }

        yield return new("OS", os);
        yield return new("Architecture", RuntimeInformation.ProcessArchitecture.ToString());
        yield return new("Runtime", RuntimeInformation.FrameworkDescription);
    }

    private static string EscapeCell(string value) =>
        value.Replace("\\", "\\\\").Replace("|", "\\|").ReplaceLineEndings("<br>");
}
