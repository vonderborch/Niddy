# GitHubIssue

`Niddy.Core.GitHub` · Niddy.Core · [source](../../../src/Niddy.Core/GitHub/GitHubIssue.cs)

What goes in a GitHub issue: a title, a Markdown body, labels and so on, plus optional collapsible sections (logs, a stack trace), a reporter, a table of extra details and system info. Pass it to [`GitHubIssues`](GitHubIssues.md) to create it through the API or open it in the browser. It's a record with `init` properties, so `with` makes variations.

## API

| Member | Description |
|---|---|
| `required string Title` | Can't be blank. Trimmed when sent. |
| `string? Body` | The issue text, in GitHub-flavored Markdown. |
| `IReadOnlyList<string> Labels` | Label names, e.g. `bug`. Default empty. Blanks and duplicates are dropped. Needs push access through the API. |
| `IReadOnlyList<string> Assignees` | Usernames, with or without `@`. Default empty. Needs push access through the API. |
| `int? Milestone` | A milestone number. Needs push access. Left out of `GetNewIssueUrl`. |
| `string? Type` | An issue type such as `Bug` or `Feature`, for organizations that have issue types set up. GitHub rejects an unknown one (`ErrorCodes.Invalid`). |
| `IReadOnlyList<GitHubIssueSection> Sections` | Titled blocks such as logs, after the body and collapsed by default. See [GitHubIssueSection](GitHubIssueSection.md). Default empty. |
| `GitHubIssueReporter? Reporter` | Who is reporting it, shown in a "Reported by" line. |
| `IReadOnlyDictionary<string, string>? Details` | Extra name/value pairs, shown as a table. |
| `bool IncludeSystemInfo` | Adds rows for the app (entry assembly name and informational version), the OS and its version, the architecture and the .NET runtime. No machine name, user name or paths. |
| `string? Fingerprint` | Identifies the problem, so repeat reports go to one issue instead of opening duplicates. See [Fingerprint](#fingerprint). Null (the default) always opens a new issue; `FromException` sets it. |
| `static GitHubIssue Bug(string title, string? body = null)` | With the `bug` label. |
| `static GitHubIssue FeatureRequest(string title, string? body = null)` | With the `enhancement` label. |
| `static GitHubIssue Question(string title, string? body = null)` | With the `question` label. |
| `static GitHubIssue Documentation(string title, string? body = null)` | With the `documentation` label. |
| `static GitHubIssue FromException(Exception exception, string? title = null, string? body = null)` | A crash report (see below). |
| `string FormatBody()` | The Markdown that becomes the issue body (see below). |

The `bug`, `enhancement`, `question` and `documentation` labels are among GitHub's defaults for new repositories. For other labels, set `Labels` yourself, e.g. `GitHubIssue.Bug(title) with { Labels = ["crash"] }`.

### FromException

A crash report in one call:

- **Title:** the exception's type and the first line of its message, e.g. `IOException: Disk full`, cut to 120 characters. A `TargetInvocationException`, or an `AggregateException` with one inner exception, is unwrapped first, so the title names the real cause. Pass `title` to set your own.
- **Body:** `body`, if given, e.g. what the user was doing.
- **Sections:** one expanded code section, "Exception", with `exception.ToString()`: the type, message and stack trace of the exception and every inner exception.
- The `bug` label, and `IncludeSystemInfo` on.
- **Fingerprint:** the type of the real cause and the method it was thrown from, e.g. `System.IO.IOException at MyApp.Saver.Save(String path)`. The message, file and line number are left out, so the same crash matches however its message varies (a different path, say) and across builds. An exception that was never thrown has no stack trace and uses the first line of its message instead. Set `Fingerprint = null` to always open a new issue, or your own value to group differently.

Exception messages can contain paths from the user's machine, such as a home folder with their user name in it. Before sending to a public repository, show the user `FormatBody()` or use `OpenInBrowser`, where they see everything before submitting.

### Fingerprint

When an issue has a `Fingerprint`, [`GitHubIssues.CreateAsync`](GitHubIssues.md#duplicates) looks for an open issue with the same one before creating it. If there is one, the report is added to it as a comment instead, so a crash that hits a thousand users makes one issue with a thousand comments rather than a thousand issues.

Any text works: `"export-crash"`, an error code, `$"{nameof(Exporter)}:{format}"`. Only a hash of it goes in the issue, as a hidden HTML comment at the end of the body, e.g. `<!-- fingerprint: nfp3f9a0c1b2d4e5f60 -->`, so it can hold details you wouldn't publish. Two issues match only when their fingerprints are exactly the same text (after trimming); a blank one counts as none.

### FormatBody

With no sections, reporter, details, system info or fingerprint, it's just `Body` (trailing whitespace trimmed). Otherwise it's the body, the sections, a rule, then the reporter line and the table, and last the fingerprint marker:

````markdown
The app crashed when I pressed Save.

### Exception

```
System.IO.IOException: Disk full
   at MyApp.Saver.Save() in Saver.cs:line 42
```

<details>
<summary>log-20261011.log</summary>

```
2026-10-11 14:03:12 [Error] Save failed
```

</details>

---

**Reported by:** Jane Doe · @jane · jane@example.com

| Detail | Value |
|---|---|
| Document | report.txt |
| App | MyApp 1.4.0 |
| OS | MacOS 15.1.0 |
| Architecture | Arm64 |
| Runtime | .NET 10.0.0 |

<!-- fingerprint: nfp3f9a0c1b2d4e5f60 -->
````

**Length.** GitHub limits a body to 65,536 characters, and a browser URL holds far less. When `GitHubIssues` has to cut, it cuts sections first, sharing the space evenly so short ones stay whole and each cut one is marked "… (cut)". Only if that isn't enough does it cut `Body`. The reporter, details and fingerprint marker are always kept. `FormatBody()` itself never cuts.

`Details` come first, in the dictionary's order, then the system info. `|` and `\` in names and values are escaped, and line breaks become `<br>`, so a value can't break the table.

### GitHubIssueReporter

`sealed record GitHubIssueReporter(string? Name = null, string? Email = null, string? GitHubUsername = null)`. Set any of them; the "Reported by" line shows those that aren't blank, and is left out when none are set.

| Parameter | Notes |
|---|---|
| `Name` | Shown as written. |
| `Email` | Public on a public repository, like the rest of the issue. |
| `GitHubUsername` | With or without `@`. It's written as an `@mention`, so GitHub notifies them and they can follow the issue. |

## Example

```csharp
using Niddy.Core.GitHub;

GitHubIssue issue = new()
{
    Title = "Export to PDF loses images",
    Body = "Images in the document are missing from the PDF.",
    Labels = ["bug", "export"],
    Reporter = new GitHubIssueReporter(GitHubUsername: "jane"),
    Details = new Dictionary<string, string> { ["Images"] = "12", ["Format"] = "PNG" },
    Sections = [GitHubIssueSection.FromLatestLogFile(paths.Logs)],
    IncludeSystemInfo = true,
};

string preview = issue.FormatBody();   // show it to the user before sending

// A crash report, plus the end of the log
GitHubIssue crash = GitHubIssue.FromException(exception, body: "I was exporting a PDF.");
crash = crash with { Sections = [.. crash.Sections, GitHubIssueSection.FromLatestLogFile(paths.Logs)] };

// Group reports of a known failure into one issue
GitHubIssue failed = GitHubIssue.Bug("Sync failed", error.Message) with { Fingerprint = $"sync:{error.Code}" };
```

## See also

- [GitHubIssues](GitHubIssues.md): creating or opening the issue
- [GitHubIssueSection](GitHubIssueSection.md): logs and other collapsible blocks
- [Platform](../SystemInfo/Platform.md): where the OS details come from
