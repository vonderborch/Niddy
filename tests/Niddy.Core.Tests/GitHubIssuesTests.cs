using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace Niddy.Core.Tests;

// Nothing here reaches GitHub: every request goes to a fake handler.
public class GitHubIssuesTests
{
    private const string Created = """{"number": 42, "html_url": "https://github.com/owner/repo/issues/42"}""";

    [Fact]
    public async Task CreateAsync_PostsTheIssueWithGitHubsHeaders()
    {
        FakeHandler handler = new(HttpStatusCode.Created, Created);
        GitHubIssue issue = new()
        {
            Title = "  Crash  ",
            Body = "It crashed.",
            Labels = ["bug", " ", "bug", "ui"],
            Assignees = ["@jane"],
            Milestone = 3,
            Type = "Bug",
        };

        await GitHubIssues.CreateAsync("owner/repo", "secret", issue, new HttpClient(handler),
            TestContext.Current.CancellationToken);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues", request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer secret", request.Headers.Authorization!.ToString());
        Assert.Equal("application/vnd.github+json", Assert.Single(request.Headers.Accept).MediaType);
        Assert.Equal("2022-11-28", Assert.Single(request.Headers.GetValues("X-GitHub-Api-Version")));
        Assert.NotEmpty(request.Headers.UserAgent);

        JsonObject json = JsonNode.Parse(handler.Bodies[0])!.AsObject();
        Assert.Equal("Crash", (string?)json["title"]);
        Assert.Equal("It crashed.", (string?)json["body"]);
        Assert.Equal(["bug", "ui"], json["labels"]!.AsArray().Select(n => (string)n!));
        Assert.Equal(["jane"], json["assignees"]!.AsArray().Select(n => (string)n!));
        Assert.Equal(3, (int?)json["milestone"]);
        Assert.Equal("Bug", (string?)json["type"]);
    }

    [Fact]
    public async Task CreateAsync_LeavesOutWhatIsntSet()
    {
        FakeHandler handler = new(HttpStatusCode.Created, Created);

        await GitHubIssues.CreateAsync("owner/repo", "secret", new GitHubIssue { Title = "Idea" },
            new HttpClient(handler), TestContext.Current.CancellationToken);

        JsonObject json = JsonNode.Parse(handler.Bodies[0])!.AsObject();
        Assert.Equal(["title"], json.Select(p => p.Key));
    }

    [Theory]
    [InlineData("https://github.example.com/api/v3/")]
    [InlineData("https://github.example.com/api/v3")]
    public async Task CreateAsync_UsesTheClientsBaseAddress(string baseAddress)
    {
        FakeHandler handler = new(HttpStatusCode.Created, Created);
        HttpClient client = new(handler) { BaseAddress = new Uri(baseAddress) };

        await GitHubIssues.CreateAsync("owner/repo", "secret", new GitHubIssue { Title = "T" }, client,
            TestContext.Current.CancellationToken);

        Assert.Equal("https://github.example.com/api/v3/repos/owner/repo/issues",
            handler.Requests[0].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task CreateAsync_ReturnsTheNewIssue()
    {
        Result<GitHubIssueReference> result = await Create(new FakeHandler(HttpStatusCode.Created, Created));

        Assert.True(result.IsSuccess);
        Assert.Equal(new GitHubIssueReference(42, new Uri("https://github.com/owner/repo/issues/42")), result.Value);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"number": "x"}""")]
    [InlineData("""{"number": 1}""")]
    public async Task CreateAsync_FailsOnAnUnreadableSuccess(string content)
    {
        Result<GitHubIssueReference> result = await Create(new FakeHandler(HttpStatusCode.Created, content));

        Assert.Equal(GitHubIssues.ErrorCodes.Failed, result.Error!.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubIssues.ErrorCodes.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubIssues.ErrorCodes.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubIssues.ErrorCodes.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, GitHubIssues.ErrorCodes.NotFound)]
    [InlineData(HttpStatusCode.Gone, GitHubIssues.ErrorCodes.IssuesDisabled)]
    [InlineData(HttpStatusCode.UnprocessableEntity, GitHubIssues.ErrorCodes.Invalid)]
    [InlineData(HttpStatusCode.InternalServerError, GitHubIssues.ErrorCodes.Failed)]
    public async Task CreateAsync_MapsErrorStatuses(HttpStatusCode status, string code)
    {
        Result<GitHubIssueReference> result = await Create(new FakeHandler(status, "{}"));

        Assert.Equal(code, result.Error!.Code);
        HttpRequestException exception = Assert.IsType<HttpRequestException>(result.Error.Exception);
        Assert.Equal(status, exception.StatusCode);
    }

    [Fact]
    public async Task CreateAsync_TellsRateLimitsFromForbidden()
    {
        FakeHandler handler = new(HttpStatusCode.Forbidden, "{}");
        handler.Headers["x-ratelimit-remaining"] = "0";

        Result<GitHubIssueReference> result = await Create(handler);

        Assert.Equal(GitHubIssues.ErrorCodes.RateLimited, result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_IncludesGitHubsMessage()
    {
        const string content = """
            {"message": "Validation Failed", "errors": [{"resource": "Issue", "field": "type", "code": "invalid"}]}
            """;

        Result<GitHubIssueReference> result = await Create(new FakeHandler(HttpStatusCode.UnprocessableEntity, content));

        Assert.Contains("Validation Failed (type invalid)", result.Error!.Message);
    }

    [Fact]
    public async Task CreateAsync_ReportsNetworkFailures()
    {
        Result<GitHubIssueReference> result = await Create(new FakeHandler(new HttpRequestException("No route")));

        Assert.Equal(GitHubIssues.ErrorCodes.Network, result.Error!.Code);
        Assert.Contains("No route", result.Error.Message);
    }

    [Fact]
    public async Task CreateAsync_ReportsTimeoutsAsNetworkFailures()
    {
        Result<GitHubIssueReference> result =
            await Create(new FakeHandler(new TaskCanceledException("Timed out", new TimeoutException())));

        Assert.Equal(GitHubIssues.ErrorCodes.Network, result.Error!.Code);
    }

    [Fact]
    public async Task CreateAsync_ThrowsWhenCanceled()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => GitHubIssues.CreateAsync("owner/repo",
            "secret", new GitHubIssue { Title = "T" }, new HttpClient(new FakeHandler(HttpStatusCode.Created, Created)),
            cts.Token));
    }

    [Fact]
    public async Task CreateAsync_CutsBodiesToGitHubsLimit()
    {
        FakeHandler handler = new(HttpStatusCode.Created, Created);

        await GitHubIssues.CreateAsync("owner/repo", "secret",
            new GitHubIssue { Title = "T", Body = new string('x', 70_000) }, new HttpClient(handler),
            TestContext.Current.CancellationToken);

        string body = (string)JsonNode.Parse(handler.Bodies[0])!["body"]!;
        Assert.True(body.Length <= GitHubIssues.MaxBodyLength);
        Assert.True(body.Length > GitHubIssues.MaxBodyLength - 100, "the body should fill most of the space");
        Assert.EndsWith(GitHubIssueSection.CutNote, body);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("owner")]
    [InlineData("owner/")]
    [InlineData("/repo")]
    [InlineData("owner/repo/extra")]
    [InlineData("own er/repo")]
    public async Task CreateAsync_RejectsBadRepositories(string repository)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            GitHubIssues.CreateAsync(repository, "secret", new GitHubIssue { Title = "T" },
                cancellationToken: TestContext.Current.CancellationToken));
        Assert.ThrowsAny<ArgumentException>(() => GitHubIssues.GetNewIssueUrl(repository, new GitHubIssue { Title = "T" }));
    }

    [Fact]
    public async Task CreateAsync_RejectsABlankTokenOrTitle()
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            GitHubIssues.CreateAsync("owner/repo", " ", new GitHubIssue { Title = "T" },
                cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            GitHubIssues.CreateAsync("owner/repo", "secret", new GitHubIssue { Title = " " },
                cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentNullException>(() =>
            GitHubIssues.CreateAsync("owner/repo", "secret", null!,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void GetNewIssueUrl_FillsInTheForm()
    {
        GitHubIssue issue = GitHubIssue.Bug("Crash & burn", "Line 1\nLine 2") with
        {
            Assignees = ["jane"],
            Milestone = 3,
            Type = "Bug",
        };

        string url = GitHubIssues.GetNewIssueUrl("owner/repo", issue);

        Assert.StartsWith("https://github.com/owner/repo/issues/new?", url);
        Assert.Contains("title=Crash%20%26%20burn", url);
        Assert.Contains("labels=bug", url);
        Assert.Contains("assignees=jane", url);
        Assert.Contains("type=Bug", url);
        Assert.Contains("body=Line%201%0ALine%202", url);
        Assert.DoesNotContain("milestone", url);
    }

    [Fact]
    public void GetNewIssueUrl_UsesTheBaseAddress()
    {
        string url = GitHubIssues.GetNewIssueUrl("owner/repo", new GitHubIssue { Title = "T" },
            new Uri("https://github.example.com"));

        Assert.Equal("https://github.example.com/owner/repo/issues/new?title=T", url);
    }

    [Fact]
    public void GetNewIssueUrl_ShortensLongBodies()
    {
        string url = GitHubIssues.GetNewIssueUrl("owner/repo",
            new GitHubIssue { Title = "T", Body = string.Concat(Enumerable.Repeat("ü 😀 ", 5_000)) });

        Assert.True(url.Length <= GitHubIssues.MaxUrlLength);
        Assert.EndsWith(Uri.EscapeDataString(GitHubIssueSection.CutNote), url);
        Assert.True(url.Length > GitHubIssues.MaxUrlLength - 100, "the body should fill most of the space");
    }

    [Fact]
    public void FormatBody_IsJustTheBodyByDefault()
    {
        Assert.Equal("Text", new GitHubIssue { Title = "T", Body = "Text\n\n" }.FormatBody());
        Assert.Equal(string.Empty, new GitHubIssue { Title = "T" }.FormatBody());
    }

    [Fact]
    public void FormatBody_AddsTheReporterAndDetails()
    {
        GitHubIssue issue = new()
        {
            Title = "T",
            Body = "Text",
            Reporter = new GitHubIssueReporter("Jane Doe", "jane@example.com", "@jane"),
            Details = new Dictionary<string, string> { ["Path"] = @"C:\a|b", ["Note"] = "one\ntwo" },
        };

        Assert.Equal("""
            Text

            ---

            **Reported by:** Jane Doe · @jane · jane@example.com

            | Detail | Value |
            |---|---|
            | Path | C:\\a\|b |
            | Note | one<br>two |
            """.ReplaceLineEndings("\n"), issue.FormatBody());
    }

    [Fact]
    public void FormatBody_SkipsAnEmptyReporter()
    {
        GitHubIssue issue = new() { Title = "T", Body = "Text", Reporter = new GitHubIssueReporter(" ") };

        Assert.Equal("Text", issue.FormatBody());
    }

    [Fact]
    public void FormatBody_AddsSystemInfo()
    {
        string body = new GitHubIssue { Title = "T", IncludeSystemInfo = true }.FormatBody();

        Assert.StartsWith("| Detail | Value |", body);
        Assert.Contains("| OS |", body);
        Assert.Contains("| Architecture |", body);
        Assert.Contains("| Runtime | .NET", body);
        Assert.DoesNotContain(Environment.MachineName, body);
        Assert.DoesNotContain(Environment.UserName, body);
    }

    [Fact]
    public void Factories_SetTheUsualLabels()
    {
        Assert.Equal(["bug"], GitHubIssue.Bug("T").Labels);
        Assert.Equal(["enhancement"], GitHubIssue.FeatureRequest("T", "Body").Labels);
        Assert.Equal(["question"], GitHubIssue.Question("T").Labels);
        Assert.Equal(["documentation"], GitHubIssue.Documentation("T").Labels);
        Assert.Equal("Body", GitHubIssue.FeatureRequest("T", "Body").Body);
    }

    [Fact]
    public void GetNewIssueUrl_CutsSectionsBeforeTheBody()
    {
        GitHubIssue issue = new()
        {
            Title = "T",
            Body = "Keep me",
            Sections = [GitHubIssueSection.Code("Log", new string('x', 20_000))],
            Details = new Dictionary<string, string> { ["Version"] = "1.2" },
        };

        string url = Uri.UnescapeDataString(GitHubIssues.GetNewIssueUrl("owner/repo", issue));

        Assert.True(url.Length <= GitHubIssues.MaxUrlLength);
        Assert.Contains("Keep me", url);
        Assert.Contains("| Version | 1.2 |", url);
        Assert.Contains(GitHubIssueSection.CutNote, url);
    }

    [Fact]
    public void FormatBody_AddsSectionsAfterTheBody()
    {
        GitHubIssue issue = new()
        {
            Title = "T",
            Body = "Text",
            Sections =
            [
                GitHubIssueSection.Code("Log <1>", "line ``` 1"),
                GitHubIssueSection.Text("Notes", "**bold**", collapsed: false),
            ],
            Reporter = new GitHubIssueReporter("Jane"),
        };

        Assert.Equal("""
            Text

            <details>
            <summary>Log &lt;1&gt;</summary>

            ````
            line ``` 1
            ````

            </details>

            ### Notes

            **bold**

            ---

            **Reported by:** Jane
            """.ReplaceLineEndings("\n"), issue.FormatBody());
    }

    [Fact]
    public void FormatBody_SharesTheSpaceBetweenSections()
    {
        GitHubIssue issue = new()
        {
            Title = "T",
            Body = "Body",
            Sections =
            [
                GitHubIssueSection.Code("Short", "tiny"),
                GitHubIssueSection.Code("Head", "START" + new string('a', 5_000)),
                GitHubIssueSection.Code("Tail", new string('b', 5_000) + "END") with { KeepEnd = true },
            ],
            Details = new Dictionary<string, string> { ["Key"] = "Value" },
        };

        string body = issue.FormatBody(3_000);

        Assert.True(body.Length <= 3_000);
        Assert.StartsWith("Body", body);
        Assert.Contains("tiny", body);
        Assert.Contains("START", body);
        Assert.Contains("END", body);
        Assert.Contains("| Key | Value |", body);
        Assert.Equal(2, body.Split(GitHubIssueSection.CutNote).Length - 1);
        Assert.InRange(body.Count(c => c == 'a'), 1_000, 1_500);
        Assert.InRange(body.Count(c => c == 'b'), 1_000, 1_500);
    }

    [Fact]
    public void FormatBody_CutsTheBodyWhenSectionsArentEnough()
    {
        GitHubIssue issue = new()
        {
            Title = "T",
            Body = new string('x', 5_000),
            Sections = [GitHubIssueSection.Code("Log", "log")],
            Reporter = new GitHubIssueReporter("Jane"),
        };

        string body = issue.FormatBody(1_000);

        Assert.True(body.Length <= 1_000);
        Assert.Contains("<summary>Log</summary>", body);
        Assert.Contains(GitHubIssueSection.CutNote, body);
        Assert.EndsWith("**Reported by:** Jane", body);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(50)]
    [InlineData(200)]
    public void FormatBody_NeverExceedsTheLimit(int maxLength)
    {
        GitHubIssue issue = GitHubIssue.FromException(new InvalidOperationException(new string('m', 300))) with
        {
            Body = new string('x', 300),
            Reporter = new GitHubIssueReporter("Jane"),
        };

        Assert.True(issue.FormatBody(maxLength).Length <= maxLength);
    }

    [Fact]
    public void FromException_ReportsTheWholeException()
    {
        Exception exception = Thrown(new InvalidOperationException("Outer", new IOException("Inner")));

        GitHubIssue issue = GitHubIssue.FromException(exception, body: "I pressed Save.");

        Assert.Equal("InvalidOperationException: Outer", issue.Title);
        Assert.Equal(["bug"], issue.Labels);
        Assert.True(issue.IncludeSystemInfo);
        GitHubIssueSection section = Assert.Single(issue.Sections);
        Assert.Equal("Exception", section.Title);
        Assert.True(section.IsCode);
        Assert.False(section.IsCollapsed);
        Assert.Contains("IOException: Inner", section.Content);
        Assert.Contains(nameof(Thrown), section.Content);
        Assert.StartsWith("I pressed Save.\n\n### Exception", issue.FormatBody());
    }

    [Fact]
    public void FromException_TitlesAfterTheRealCause()
    {
        AggregateException aggregate = new(new System.Reflection.TargetInvocationException(new IOException("Disk\nfull")));

        Assert.Equal("IOException: Disk", GitHubIssue.FromException(aggregate).Title);
        Assert.Equal("Mine", GitHubIssue.FromException(aggregate, "Mine").Title);
        Assert.Equal(nameof(AggregateException),
            GitHubIssue.FromException(new AggregateException(new IOException(), new IOException())).Title.Split(':')[0]);
    }

    [Fact]
    public void FromException_ShortensLongTitles()
    {
        string title = GitHubIssue.FromException(new IOException(new string('m', 500))).Title;

        Assert.Equal(120, title.Length);
        Assert.EndsWith("…", title);
    }

    [Fact]
    public void FromLogFile_ReadsTheLastLinesWhileTheFileIsOpen()
    {
        string path = Path.Combine(Path.GetTempPath(), $"niddy-{Guid.NewGuid():N}.log");
        try
        {
            using FileStream writer = new(path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\r\n", Enumerable.Range(1, 500).Select(i => $"line {i}")) + "\r\n");
            writer.Write(bytes);
            writer.Flush();

            GitHubIssueSection section = GitHubIssueSection.FromLogFile(path, lines: 3);

            Assert.Equal("line 498\nline 499\nline 500", section.Content);
            Assert.Equal(Path.GetFileName(path), section.Title);
            Assert.True(section.IsCode);
            Assert.True(section.IsCollapsed);
            Assert.True(section.KeepEnd);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromLogFile_ReadsOnlyTheEndOfABigFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"niddy-{Guid.NewGuid():N}.log");
        try
        {
            File.WriteAllLines(path, Enumerable.Range(1, 20_000).Select(i => $"line {i:00000} {new string('x', 20)}"));

            string content = GitHubIssueSection.FromLogFile(path, lines: 100_000).Content;
            string[] lines = content.Split('\n');

            Assert.True(content.Length <= 64 * 1024);
            Assert.StartsWith("line ", lines[0]);
            Assert.StartsWith("line 20000 ", lines[^1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromLogFile_DescribesAMissingFileInsteadOfThrowing()
    {
        GitHubIssueSection section = GitHubIssueSection.FromLogFile("/no/such/dir/app.log", title: "Log");

        Assert.Equal("Log", section.Title);
        Assert.StartsWith("Couldn't read app.log", section.Content);
    }

    [Fact]
    public void FromLatestLogFile_PicksTheNewestFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"niddy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "old.log"), "old");
            File.SetLastWriteTimeUtc(Path.Combine(directory, "old.log"), DateTime.UtcNow.AddDays(-1));
            File.WriteAllText(Path.Combine(directory, "new.log"), "new");
            File.WriteAllText(Path.Combine(directory, "newer.txt"), "not a log");

            GitHubIssueSection section = GitHubIssueSection.FromLatestLogFile(directory);

            Assert.Equal("new.log", section.Title);
            Assert.Equal("new", section.Content);
            Assert.StartsWith("No *.json files", GitHubIssueSection.FromLatestLogFile(directory, searchPattern: "*.json").Content);
            Assert.StartsWith("Couldn't read", GitHubIssueSection.FromLatestLogFile(Path.Combine(directory, "missing")).Content);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Sections_RejectBadArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GitHubIssueSection.FromLogFile("a.log", lines: 0));
        Assert.ThrowsAny<ArgumentException>(() => GitHubIssueSection.FromLatestLogFile(" "));
    }

    [Fact]
    public async Task CreateOrOpenAsync_CreatesThroughTheApiWhenItCan()
    {
        List<string> opened = [];

        Result<GitHubIssueSubmission> result = await GitHubIssues.CreateOrOpenAsync("owner/repo", "secret",
            new GitHubIssue { Title = "T" }, new HttpClient(new FakeHandler(HttpStatusCode.Created, Created)),
            opened.Add, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Value!.OpenedInBrowser);
        Assert.Equal(42, result.Value.Issue!.Number);
        Assert.Null(result.Value.ApiError);
        Assert.Empty(opened);
    }

    [Fact]
    public async Task CreateOrOpenAsync_OpensTheFormWithoutAToken()
    {
        List<string> opened = [];
        FakeHandler handler = new(HttpStatusCode.Created, Created);

        Result<GitHubIssueSubmission> result = await GitHubIssues.CreateOrOpenAsync("owner/repo", null,
            new GitHubIssue { Title = "T" }, new HttpClient(handler), opened.Add,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Value!.OpenedInBrowser);
        Assert.Equal("https://github.com/owner/repo/issues/new?title=T", Assert.Single(opened));
        Assert.Equal(opened[0], result.Value.NewIssueUrl);
        Assert.Null(result.Value.ApiError);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task CreateOrOpenAsync_FallsBackToTheFormWhenTheApiRefuses(HttpStatusCode status)
    {
        List<string> opened = [];

        Result<GitHubIssueSubmission> result = await GitHubIssues.CreateOrOpenAsync("owner/repo", "secret",
            new GitHubIssue { Title = "T" }, new HttpClient(new FakeHandler(status, "{}")), opened.Add,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Value!.OpenedInBrowser);
        Assert.Single(opened);
        Assert.NotNull(result.Value.ApiError);
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, GitHubIssues.ErrorCodes.IssuesDisabled)]
    [InlineData(HttpStatusCode.InternalServerError, GitHubIssues.ErrorCodes.Failed)]
    public async Task CreateOrOpenAsync_ReturnsOtherFailures(HttpStatusCode status, string code)
    {
        List<string> opened = [];

        Result<GitHubIssueSubmission> result = await GitHubIssues.CreateOrOpenAsync("owner/repo", "secret",
            new GitHubIssue { Title = "T" }, new HttpClient(new FakeHandler(status, "{}")), opened.Add,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(code, result.Error!.Code);
        Assert.Empty(opened);
    }

    [Fact]
    public async Task CreateOrOpenAsync_ReportsABrowserThatWontOpen()
    {
        Result<GitHubIssueSubmission> result = await GitHubIssues.CreateOrOpenAsync("owner/repo", "secret",
            new GitHubIssue { Title = "T" }, new HttpClient(new FakeHandler(HttpStatusCode.Unauthorized, "{}")),
            _ => throw new PlatformNotSupportedException("No browser"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GitHubIssues.ErrorCodes.BrowserUnavailable, result.Error!.Code);
        Assert.Contains("No browser", result.Error.Message);
        Assert.Contains("token is missing", result.Error.Message);
    }

    [Fact]
    public void Create_SendsSynchronously()
    {
        FakeHandler handler = new(HttpStatusCode.Created, Created);

        Result<GitHubIssueReference> result = GitHubIssues.Create("owner/repo", "secret", new GitHubIssue { Title = "T" },
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value!.Number);
        Assert.Equal(1, handler.SyncSends);
        Assert.Equal("T", (string?)JsonNode.Parse(handler.Bodies[0])!["title"]);
    }

    [Fact]
    public void Create_FailsLikeTheAsyncVersion()
    {
        Result<GitHubIssueReference> result = GitHubIssues.Create("owner/repo", "secret", new GitHubIssue { Title = "T" },
            new HttpClient(new FakeHandler(HttpStatusCode.Unauthorized, "{}")), TestContext.Current.CancellationToken);

        Assert.Equal(GitHubIssues.ErrorCodes.Unauthorized, result.Error!.Code);
    }

    [Fact]
    public void CreateOrOpen_FallsBackToTheFormSynchronously()
    {
        List<string> opened = [];
        FakeHandler handler = new(HttpStatusCode.Unauthorized, "{}");

        Result<GitHubIssueSubmission> result = GitHubIssues.CreateOrOpen("owner/repo", "secret",
            new GitHubIssue { Title = "T" }, new HttpClient(handler), opened.Add,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Value!.OpenedInBrowser);
        Assert.Single(opened);
        Assert.Equal(1, handler.SyncSends);
    }

    private static Exception Thrown(Exception exception)
    {
        try
        {
            throw exception;
        }
        catch (Exception caught)
        {
            return caught;
        }
    }

    private static Task<Result<GitHubIssueReference>> Create(FakeHandler handler) =>
        GitHubIssues.CreateAsync("owner/repo", "secret", new GitHubIssue { Title = "T" }, new HttpClient(handler),
            TestContext.Current.CancellationToken);

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _content;
        private readonly Exception? _exception;

        public FakeHandler(HttpStatusCode status, string content)
        {
            this._status = status;
            this._content = content;
        }

        public FakeHandler(Exception exception)
        {
            this._content = string.Empty;
            this._exception = exception;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public Dictionary<string, string> Headers { get; } = [];

        public int SyncSends { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return this.Respond(request, body, cancellationToken);
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.SyncSends++;
            string body = string.Empty;
            if (request.Content is not null)
            {
                using StreamReader reader = new(request.Content.ReadAsStream(cancellationToken));
                body = reader.ReadToEnd();
            }

            return this.Respond(request, body, cancellationToken);
        }

        private HttpResponseMessage Respond(HttpRequestMessage request, string body, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            this.Requests.Add(request);
            this.Bodies.Add(body);
            if (this._exception is not null)
            {
                throw this._exception;
            }

            HttpResponseMessage response = new(this._status)
            {
                Content = new StringContent(this._content, Encoding.UTF8, "application/json"),
            };
            foreach ((string name, string value) in this.Headers)
            {
                response.Headers.Add(name, value);
            }

            return response;
        }
    }
}
