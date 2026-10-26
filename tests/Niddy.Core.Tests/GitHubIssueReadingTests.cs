using System.Net;
using System.Text.Json.Nodes;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace Niddy.Core.Tests;

// Nothing here reaches GitHub: every request goes to a fake handler.
public sealed class GitHubIssueReadingTests : IDisposable
{
    private const string Api = "https://api.github.com/repos/owner/repo/";

    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-issue-reads-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(this._dir, recursive: true);

    [Fact]
    public async Task ListAsync_SendsTheFilter_AndReadsTheIssues()
    {
        JsonObject issue = Issue(42);
        issue["body"] = "It crashed.";
        issue["state"] = "closed";
        issue["state_reason"] = "completed";
        issue["user"] = new JsonObject { ["login"] = "octocat" };
        issue["labels"] = new JsonArray(new JsonObject { ["name"] = "bug" }, "crash");
        issue["assignees"] = new JsonArray(new JsonObject { ["login"] = "hubot" });
        issue["milestone"] = new JsonObject { ["title"] = "v2" };
        issue["comments"] = 3;
        issue["locked"] = true;
        issue["created_at"] = "2026-01-02T03:04:05Z";
        issue["closed_at"] = "2026-02-02T00:00:00Z";
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray(issue)));
        GitHubIssueFilter filter = new()
        {
            State = GitHubStateFilter.All,
            Labels = ["bug", " ui "],
            Assignee = "hubot",
            Creator = "octocat",
            Mentioned = "someone",
            Milestone = "3",
            Since = new DateTimeOffset(2026, 1, 1, 2, 0, 0, TimeSpan.FromHours(2)),
            Sort = GitHubIssueSort.Updated,
            Ascending = true,
        };

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo", filter, 10,
            new GitHubReadOptions { Token = "secret" }, new HttpClient(handler), Token);

        Assert.Equal(
            Api + "issues?state=all&labels=bug%2Cui&assignee=hubot&creator=octocat&mentioned=someone&milestone=3"
                + "&since=2026-01-01T00%3A00%3A00Z&sort=updated&direction=asc&per_page=10&page=1",
            Assert.Single(handler.Urls));
        Assert.Equal("Bearer secret", handler.Requests[0].Headers.Authorization!.ToString());

        GitHubIssueInfo read = Assert.Single(result.Value!);
        Assert.Equal(42, read.Number);
        Assert.Equal("Issue 42", read.Title);
        Assert.Equal("It crashed.", read.Body);
        Assert.Equal(GitHubItemState.Closed, read.State);
        Assert.Equal("completed", read.StateReason);
        Assert.False(read.IsPullRequest);
        Assert.Equal(new Uri("https://github.com/owner/repo/issues/42"), read.Url);
        Assert.Equal("owner/repo", read.Repository);
        Assert.Equal("octocat", read.Author);
        Assert.Equal(["bug", "crash"], read.Labels);
        Assert.Equal(["hubot"], read.Assignees);
        Assert.Equal("v2", read.Milestone);
        Assert.Equal(3, read.CommentCount);
        Assert.True(read.IsLocked);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), read.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero), read.ClosedAt);
        Assert.Null(read.UpdatedAt);
    }

    [Fact]
    public async Task ListAsync_ByDefault_AsksForOpenIssuesNewestFirst()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray()));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Empty(result.Value!);
        Assert.Equal(Api + "issues?state=open&sort=created&direction=desc&per_page=30&page=1", Assert.Single(handler.Urls));
        Assert.Null(handler.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task ListAsync_SkipsPullRequests_UnlessAskedFor()
    {
        JsonObject pullRequest = Issue(2);
        pullRequest["pull_request"] = new JsonObject { ["merged_at"] = "2026-01-01T00:00:00Z" };
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray(Issue(1), pullRequest.DeepClone())));

        Result<IReadOnlyList<GitHubIssueInfo>> issues = await GitHubIssues.ListAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);
        Result<IReadOnlyList<GitHubIssueInfo>> both = await GitHubIssues.ListAsync("owner/repo",
            new GitHubIssueFilter { IncludePullRequests = true }, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        Assert.Equal([1], issues.Value!.Select(issue => issue.Number));
        Assert.Equal([1, 2], both.Value!.Select(issue => issue.Number));
        Assert.True(both.Value![1].IsPullRequest);
        Assert.Equal(GitHubItemState.Merged, both.Value[1].State);
    }

    [Fact]
    public async Task ListAsync_ReadsPages_UntilAShortOne()
    {
        GitHubFakeHandler handler = new(request => GitHubFakeHandler.Json(
            request.RequestUri!.Query.EndsWith("page=1", StringComparison.Ordinal) ? Issues(1, 100) : Issues(101, 30)));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo", maxCount: 500,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(130, result.Value!.Count);
        Assert.Equal(130, result.Value[^1].Number);
        Assert.Collection(handler.Urls,
            url => Assert.EndsWith("&per_page=100&page=1", url),
            url => Assert.EndsWith("&per_page=100&page=2", url));
    }

    [Fact]
    public async Task ListAsync_StopsAtMaxCount()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(Issues(1, 100)));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo", maxCount: 150,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(150, result.Value!.Count);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task ListAsync_FullPageOfSkippedPullRequests_StillReadsTheNextPage()
    {
        JsonArray pullRequests = Issues(1, 2);
        foreach (JsonNode? pullRequest in pullRequests)
        {
            pullRequest!["pull_request"] = new JsonObject();
        }

        GitHubFakeHandler handler = new(request => GitHubFakeHandler.Json(
            request.RequestUri!.Query.EndsWith("page=1", StringComparison.Ordinal)
                ? pullRequests.DeepClone()
                : new JsonArray(Issue(3))));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo", maxCount: 2,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal([3], result.Value!.Select(issue => issue.Number));
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task GetAsync_ReadsOneIssue()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(Issue(7)));

        Result<GitHubIssueInfo> result = await GitHubIssues.GetAsync("owner/repo", 7,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(Api + "issues/7", Assert.Single(handler.Urls));
        Assert.Equal(7, result.Value!.Number);
        Assert.Equal(GitHubItemState.Open, result.Value.State);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("https://other.example.com/api/v3/")]
    public async Task ApiAddress_TakesPrecedenceOverTheClients(string? clientAddress)
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(Issue(7)));
        HttpClient client = new(handler) { BaseAddress = clientAddress is null ? null : new Uri(clientAddress) };
        GitHubReadOptions options = new() { ApiAddress = new Uri("https://github.example.com/api/v3") };

        await GitHubIssues.GetAsync("owner/repo", 7, options, client, Token);

        Assert.Equal("https://github.example.com/api/v3/repos/owner/repo/issues/7", Assert.Single(handler.Urls));
    }

    [Fact]
    public async Task ApiAddress_KeepsSeparateCachesPerServer()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(Issue(7)));
        HttpClient client = new(handler);
        GitHubReadOptions cloud = new() { CacheDirectory = this._dir, CacheDuration = TimeSpan.FromHours(1) };
        GitHubReadOptions enterprise = cloud with { ApiAddress = new Uri("https://github.example.com/api/v3/") };

        await GitHubIssues.GetAsync("owner/repo", 7, cloud, client, Token);
        await GitHubIssues.GetAsync("owner/repo", 7, enterprise, client, Token);
        await GitHubIssues.GetAsync("owner/repo", 7, enterprise, client, Token);

        Assert.Equal([Api + "issues/7", "https://github.example.com/api/v3/repos/owner/repo/issues/7"], handler.Urls);
    }

    [Theory]
    [InlineData("api/v3/")]
    [InlineData("ftp://github.example.com/api/v3/")]
    public void ApiAddress_ThatIsntAbsoluteHttp_Throws(string address)
    {
        Assert.Throws<ArgumentException>(() => new GitHubReadOptions { ApiAddress = new Uri(address, UriKind.RelativeOrAbsolute) });
        Assert.Throws<ArgumentException>(() => new GitHubReleaseOptions { ApiAddress = new Uri(address, UriKind.RelativeOrAbsolute) });
    }

    [Fact]
    public async Task GetAsync_MissingIssue_IsNotFound()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject { ["message"] = "Not Found" },
            HttpStatusCode.NotFound));

        Result<GitHubIssueInfo> result = await GitHubIssues.GetAsync("owner/repo", 7,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubIssues.ErrorCodes.NotFound, result.Error?.Code);
        Assert.Contains("#7", result.Error?.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubIssues.ErrorCodes.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubIssues.ErrorCodes.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests, GitHubIssues.ErrorCodes.RateLimited)]
    [InlineData(HttpStatusCode.NotFound, GitHubIssues.ErrorCodes.NotFound)]
    [InlineData(HttpStatusCode.Gone, GitHubIssues.ErrorCodes.IssuesDisabled)]
    [InlineData(HttpStatusCode.UnprocessableEntity, GitHubIssues.ErrorCodes.Invalid)]
    [InlineData(HttpStatusCode.InternalServerError, GitHubIssues.ErrorCodes.Failed)]
    public async Task ListAsync_FailedStatus_IsAFailure(HttpStatusCode status, string code)
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject { ["message"] = "Nope" }, status));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(code, result.Error?.Code);
        Assert.EndsWith("GitHub said: Nope", result.Error?.Message);
        Assert.Equal(status, Assert.IsType<HttpRequestException>(result.Error?.Exception).StatusCode);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"not": "a list"}""")]
    [InlineData("""[{"number": 1}]""")]
    public async Task ListAsync_UnreadableResponse_Fails(string content)
    {
        GitHubFakeHandler handler = new(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(content) });

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.ListAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubIssues.ErrorCodes.Failed, result.Error?.Code);
        Assert.StartsWith("GitHub's response couldn't be read", result.Error?.Message);
    }

    [Fact]
    public async Task ListAsync_Offline_FallsBackToTheCache()
    {
        bool online = true;
        GitHubFakeHandler handler = new(_ => online
            ? GitHubFakeHandler.Json(new JsonArray(Issue(1)))
            : throw new HttpRequestException("No such host"));
        GitHubReadOptions options = new() { CacheDirectory = this._dir };

        Result<IReadOnlyList<GitHubIssueInfo>> first = await GitHubIssues.ListAsync("owner/repo", options: options,
            httpClient: new HttpClient(handler), cancellationToken: Token);
        online = false;
        Result<IReadOnlyList<GitHubIssueInfo>> second = await GitHubIssues.ListAsync("owner/repo", options: options,
            httpClient: new HttpClient(handler), cancellationToken: Token);
        Result<IReadOnlyList<GitHubIssueInfo>> uncached = await GitHubIssues.ListAsync("owner/other",
            options: options, httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(1, Assert.Single(first.Value!).Number);
        Assert.Equal(1, Assert.Single(second.Value!).Number);
        Assert.Equal(GitHubIssues.ErrorCodes.Network, uncached.Error?.Code);
    }

    [Fact]
    public async Task SearchAsync_SendsTheQuery_AndReadsItsItems()
    {
        JsonObject found = Issue(5);
        found["repository_url"] = "https://api.github.com/repos/other/project";
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject
        {
            ["total_count"] = 1,
            ["items"] = new JsonArray(found),
        }));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.SearchAsync(" is:open label:\"good first\" ",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal("https://api.github.com/search/issues?q=is%3Aopen%20label%3A%22good%20first%22&per_page=30&page=1",
            Assert.Single(handler.Urls));
        GitHubIssueInfo issue = Assert.Single(result.Value!);
        Assert.Equal("other/project", issue.Repository);
    }

    [Fact]
    public async Task SearchAsync_StopsAtGitHubsThousandResults()
    {
        int page = 0;
        GitHubFakeHandler handler = new(_ =>
        {
            page++;
            return GitHubFakeHandler.Json(new JsonObject { ["items"] = Issues((page - 1) * 100 + 1, 100) });
        });

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.SearchAsync("bug", 5000,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(1000, result.Value!.Count);
        Assert.Equal(10, handler.Requests.Count);
    }

    [Fact]
    public async Task SearchAsync_RejectedQuery_IsInvalid()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject { ["message"] = "Validation Failed" },
            HttpStatusCode.UnprocessableEntity));

        Result<IReadOnlyList<GitHubIssueInfo>> result = await GitHubIssues.SearchAsync("repo:nobody/nothing",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubIssues.ErrorCodes.Invalid, result.Error?.Code);
    }

    [Fact]
    public async Task ListCommentsAsync_ReadsTheComments()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray(new JsonObject
        {
            ["id"] = 9_000_000_001L,
            ["body"] = "Me too.",
            ["user"] = new JsonObject { ["login"] = "octocat" },
            ["html_url"] = "https://github.com/owner/repo/issues/3#issuecomment-1",
            ["created_at"] = "2026-01-01T00:00:00Z",
            ["updated_at"] = "2026-01-02T00:00:00Z",
        })));

        Result<IReadOnlyList<GitHubComment>> result = await GitHubIssues.ListCommentsAsync("owner/repo", 3,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(Api + "issues/3/comments?per_page=100&page=1", Assert.Single(handler.Urls));
        GitHubComment comment = Assert.Single(result.Value!);
        Assert.Equal(9_000_000_001L, comment.Id);
        Assert.Equal("Me too.", comment.Body);
        Assert.Equal("octocat", comment.Author);
        Assert.Equal(new Uri("https://github.com/owner/repo/issues/3#issuecomment-1"), comment.Url);
        Assert.Equal(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), comment.UpdatedAt);
    }

    [Fact]
    public void SyncVersions_SendSynchronously()
    {
        GitHubFakeHandler handler = new(request => GitHubFakeHandler.Json(request.RequestUri!.AbsolutePath switch
        {
            "/search/issues" => new JsonObject { ["items"] = new JsonArray(Issue(1)) },
            "/repos/owner/repo/issues/1" => Issue(1),
            _ => new JsonArray(),
        }));
        HttpClient client = new(handler);

        Assert.True(GitHubIssues.List("owner/repo", httpClient: client, cancellationToken: Token).IsSuccess);
        Assert.Equal(1, GitHubIssues.Get("owner/repo", 1, httpClient: client, cancellationToken: Token).Value!.Number);
        Assert.Single(GitHubIssues.Search("bug", httpClient: client, cancellationToken: Token).Value!);
        Assert.True(GitHubIssues.ListComments("owner/repo", 1, httpClient: client, cancellationToken: Token).IsSuccess);
        Assert.Equal(4, handler.SyncSends);
    }

    [Fact]
    public async Task BadArguments_Throw()
    {
        HttpClient client = new(new GitHubFakeHandler(_ => throw new InvalidOperationException("No request expected.")));

        await Assert.ThrowsAsync<ArgumentException>(() => GitHubIssues.ListAsync("not-a-repo", httpClient: client,
            cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.ListAsync("owner/repo", maxCount: 0,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.GetAsync("owner/repo", 0,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => GitHubIssues.SearchAsync(" ", httpClient: client,
            cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.ListCommentsAsync("owner/repo", 1, 0,
            httpClient: client, cancellationToken: Token));
    }

    private static JsonObject Issue(int number) => new()
    {
        ["number"] = number,
        ["title"] = $"Issue {number}",
        ["state"] = "open",
        ["html_url"] = $"https://github.com/owner/repo/issues/{number}",
        ["repository_url"] = "https://api.github.com/repos/owner/repo",
    };

    private static JsonArray Issues(int first, int count) =>
        [.. Enumerable.Range(first, count).Select(number => (JsonNode)Issue(number))];
}
