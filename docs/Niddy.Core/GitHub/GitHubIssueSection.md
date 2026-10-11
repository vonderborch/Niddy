# GitHubIssueSection

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubIssueSection.cs)

A titled block of extra content in a [`GitHubIssue`](GitHubIssue.md), such as a log, a config file or a stack trace. GitHub's API can't attach files, so this is how logs get into an issue. By default a section is collapsed: a `<details>` block the reader clicks to expand, so a long log doesn't swamp the issue. When the body is too long for GitHub, sections are cut before anything else.

## API

| Member | Description |
|---|---|
| `required string Title` | The heading, or the summary line of a collapsed section. |
| `required string Content` | Markdown, or plain text when `IsCode` is set. |
| `bool IsCode` | Shows `Content` as written, in a code block. The fence is made longer than any run of backticks in the content, so it can't break out. |
| `bool IsCollapsed` | Default true. False shows a `### Title` heading and the content. |
| `bool KeepEnd` | When the section has to be cut, keep the end rather than the start. Set for logs, where the latest lines matter most. |
| `static GitHubIssueSection Text(string title, string markdown, bool collapsed = true)` | A section of Markdown. |
| `static GitHubIssueSection Code(string title, string content, bool collapsed = true)` | A code block. |
| `static GitHubIssueSection FromLogFile(string path, int lines = 100, string? title = null)` | A collapsed code section with the last `lines` lines of a file, with `KeepEnd` set. The title defaults to the file name. |
| `static GitHubIssueSection FromLatestLogFile(string directory, int lines = 100, string searchPattern = "*.log", string? title = null)` | `FromLogFile` for the most recently written matching file in `directory`, e.g. the current file of Niddy's [rolling file logger](../Logging/Files/FileLoggerProvider.md). |

**The log helpers are safe in a crash handler.**
- They read files that a logger is still writing to.
- They read at most the last 64 KB.
- They never throw for a missing or unreadable file or folder. The section's content says what went wrong instead, e.g. `Couldn't read app.log: …` or `No *.log files in the log folder.`
- They do throw `ArgumentOutOfRangeException` if `lines` is less than 1, and `ArgumentException` for a blank path.

**Cutting.** When the body has to be cut, sections share the space left after the body, reporter and details. Short sections stay whole, and long ones get equal shares of the rest. A cut is marked "… (cut)", at the end, or at the start for `KeepEnd` sections.

Logs often hold paths, user names or data from the user's documents. On a public repository, the issue is public, so show the user what's included or let them submit it with `GitHubIssues.OpenInBrowser`.

## Example

```csharp
using Niddy.Core.GitHub;

GitHubIssue issue = GitHubIssue.Bug("Sync fails after sign-in", "It worked yesterday.") with
{
    Sections =
    [
        GitHubIssueSection.FromLatestLogFile(paths.Logs, lines: 200),
        GitHubIssueSection.Code("Settings", File.ReadAllText(settingsPath)),
        GitHubIssueSection.Text("Steps", "1. Sign in\n2. Press Sync", collapsed: false),
    ],
};
```

## See also

- [GitHubIssue](GitHubIssue.md), [GitHubIssues](GitHubIssues.md)
- [NiddyLogging](../Logging/NiddyLogging.md), [AppPaths](../IO/AppPaths.md)
