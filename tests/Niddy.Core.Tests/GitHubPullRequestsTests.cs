using System.Net;
using System.Text.Json.Nodes;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace Niddy.Core.Tests;

// Nothing here reaches GitHub: every request goes to a fake handler.
public sealed class GitHubPullRequestsTests
{
    private const string Api = "https://api.github.com/repos/owner/repo/";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListAsync_SendsTheFilter_AndReadsThePullRequests()
    {
        JsonObject pull = PullRequest(12);
        pull["body"] = "Fixes #3.";
        pull["draft"] = true;
        pull["user"] = new JsonObject { ["login"] = "octocat" };
        pull["labels"] = new JsonArray(new JsonObject { ["name"] = "fix" });
        pull["assignees"] = new JsonArray(new JsonObject { ["login"] = "hubot" });
        pull["requested_reviewers"] = new JsonArray(new JsonObject { ["login"] = "reviewer" });
        pull["milestone"] = new JsonObject { ["title"] = "v2" };
        pull["head"] = new JsonObject
        {
            ["ref"] = "fix-crash",
            ["sha"] = "abc123",
            ["repo"] = new JsonObject { ["full_name"] = "fork/repo" },
        };
        pull["created_at"] = "2026-03-01T00:00:00Z";
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray(pull)));
        GitHubPullRequestFilter filter = new()
        {
            State = GitHubStateFilter.All,
            Head = "fork:fix-crash",
            Base = "main",
            Sort = GitHubPullRequestSort.LongRunning,
            Ascending = true,
        };

        Result<IReadOnlyList<GitHubPullRequest>> result = await GitHubPullRequests.ListAsync("owner/repo", filter, 5,
            new GitHubReadOptions { Token = "secret" }, new HttpClient(handler), Token);

        Assert.Equal(Api + "pulls?state=all&head=fork%3Afix-crash&base=main&sort=long-running&direction=asc&per_page=5&page=1",
            Assert.Single(handler.Urls));
        Assert.Equal("Bearer secret", handler.Requests[0].Headers.Authorization!.ToString());

        GitHubPullRequest read = Assert.Single(result.Value!);
        Assert.Equal(12, read.Number);
        Assert.Equal("Pull 12", read.Title);
        Assert.Equal("Fixes #3.", read.Body);
        Assert.Equal(GitHubItemState.Open, read.State);
        Assert.True(read.IsDraft);
        Assert.Equal(new Uri("https://github.com/owner/repo/pull/12"), read.Url);
        Assert.Equal("owner/repo", read.Repository);
        Assert.Equal("octocat", read.Author);
        Assert.Equal(["fix"], read.Labels);
        Assert.Equal(["hubot"], read.Assignees);
        Assert.Equal(["reviewer"], read.RequestedReviewers);
        Assert.Equal("v2", read.Milestone);
        Assert.Equal("fix-crash", read.HeadBranch);
        Assert.Equal("abc123", read.HeadSha);
        Assert.Equal("fork/repo", read.HeadRepository);
        Assert.Equal("main", read.BaseBranch);
        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), read.CreatedAt);
        Assert.Null(read.MergedAt);
        Assert.Null(read.IsMergeable);
        Assert.Null(read.CommentCount);
        Assert.Null(read.ChangedFiles);
    }

    [Fact]
    public async Task ListAsync_ByDefault_AsksForOpenPullRequestsNewestFirst()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray()));

        Result<IReadOnlyList<GitHubPullRequest>> result = await GitHubPullRequests.ListAsync("owner/repo",
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Empty(result.Value!);
        Assert.Equal(Api + "pulls?state=open&sort=created&direction=desc&per_page=30&page=1", Assert.Single(handler.Urls));
    }

    [Fact]
    public async Task GetAsync_ReadsTheCountsAndMergeState()
    {
        JsonObject pull = PullRequest(4);
        pull["state"] = "closed";
        pull["merged"] = true;
        pull["merged_at"] = "2026-04-05T06:07:08Z";
        pull["closed_at"] = "2026-04-05T06:07:08Z";
        pull["mergeable"] = false;
        pull["comments"] = 2;
        pull["review_comments"] = 5;
        pull["commits"] = 3;
        pull["additions"] = 120;
        pull["deletions"] = 7;
        pull["changed_files"] = 4;
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(pull));

        Result<GitHubPullRequest> result = await GitHubPullRequests.GetAsync("owner/repo", 4,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(Api + "pulls/4", Assert.Single(handler.Urls));
        GitHubPullRequest read = result.Value!;
        Assert.Equal(GitHubItemState.Merged, read.State);
        Assert.Equal(new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero), read.MergedAt);
        Assert.False(read.IsMergeable);
        Assert.Equal(2, read.CommentCount);
        Assert.Equal(5, read.ReviewCommentCount);
        Assert.Equal(3, read.CommitCount);
        Assert.Equal(120, read.Additions);
        Assert.Equal(7, read.Deletions);
        Assert.Equal(4, read.ChangedFiles);
    }

    [Fact]
    public async Task GetAsync_ClosedWithoutMerging_IsClosed()
    {
        JsonObject pull = PullRequest(4);
        pull["state"] = "closed";
        pull["merged_at"] = null;

        Result<GitHubPullRequest> result = await GitHubPullRequests.GetAsync("owner/repo", 4,
            httpClient: new HttpClient(new GitHubFakeHandler(_ => GitHubFakeHandler.Json(pull))), cancellationToken: Token);

        Assert.Equal(GitHubItemState.Closed, result.Value!.State);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, GitHubPullRequests.ErrorCodes.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity, GitHubPullRequests.ErrorCodes.Invalid)]
    [InlineData(HttpStatusCode.Unauthorized, GitHubPullRequests.ErrorCodes.Unauthorized)]
    [InlineData(HttpStatusCode.BadGateway, GitHubPullRequests.ErrorCodes.Failed)]
    public async Task GetAsync_FailedStatus_IsAFailure(HttpStatusCode status, string code)
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject { ["message"] = "Nope" }, status));

        Result<GitHubPullRequest> result = await GitHubPullRequests.GetAsync("owner/repo", 4,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(code, result.Error?.Code);
        Assert.EndsWith("GitHub said: Nope", result.Error?.Message);
    }

    [Fact]
    public void SyncVersions_SendSynchronously()
    {
        GitHubFakeHandler handler = new(request => GitHubFakeHandler.Json(
            request.RequestUri!.AbsolutePath.EndsWith("/pulls", StringComparison.Ordinal)
                ? new JsonArray(PullRequest(1))
                : PullRequest(1)));
        HttpClient client = new(handler);

        Assert.Single(GitHubPullRequests.List("owner/repo", httpClient: client, cancellationToken: Token).Value!);
        Assert.Equal(1, GitHubPullRequests.Get("owner/repo", 1, httpClient: client, cancellationToken: Token).Value!.Number);
        Assert.Equal(2, handler.SyncSends);
    }

    [Fact]
    public async Task BadArguments_Throw()
    {
        HttpClient client = new(new GitHubFakeHandler(_ => throw new InvalidOperationException("No request expected.")));

        await Assert.ThrowsAsync<ArgumentException>(() => GitHubPullRequests.ListAsync("owner/", httpClient: client,
            cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubPullRequests.ListAsync("owner/repo",
            maxCount: 0, httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubPullRequests.GetAsync("owner/repo", -1,
            httpClient: client, cancellationToken: Token));
    }

    private static JsonObject PullRequest(int number) => new()
    {
        ["number"] = number,
        ["title"] = $"Pull {number}",
        ["state"] = "open",
        ["html_url"] = $"https://github.com/owner/repo/pull/{number}",
        ["base"] = new JsonObject
        {
            ["ref"] = "main",
            ["repo"] = new JsonObject { ["full_name"] = "owner/repo" },
        },
    };
}
