using System.Text.Json.Nodes;
using Niddy.Core.Collections;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace Niddy.Core.Tests;

// The page-at-a-time reads. Nothing here reaches GitHub: every request goes to a fake handler.
public sealed class GitHubPagingTests : IDisposable
{
    private const string Api = "https://api.github.com/repos/owner/repo/";

    private static readonly GitHubReadOptions Options = new() { Token = "secret" };

    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-github-pages-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(this._dir, recursive: true);

    [Fact]
    public async Task IssuesListPageAsync_AsksForThePage_AndReadsTheLinkHeader()
    {
        JsonObject pull = Issue(5);
        pull["pull_request"] = new JsonObject();
        GitHubFakeHandler handler = new(_ => WithLink(GitHubFakeHandler.Json(new JsonArray(Issue(4), pull, Issue(6))),
            $"<{Api}issues?per_page=10&page=4>; rel=\"next\", <{Api}issues?per_page=10&page=7>; rel=\"last\""));

        Result<ItemPage<GitHubIssueInfo>> result = await GitHubIssues.ListPageAsync("owner/repo",
            new GitHubIssueFilter { State = GitHubStateFilter.All }, 3, 10, httpClient: new HttpClient(handler),
            cancellationToken: Token);

        string url = Assert.Single(handler.Urls);
        Assert.StartsWith(Api + "issues?state=all&", url);
        Assert.EndsWith("&per_page=10&page=3", url);
        ItemPage<GitHubIssueInfo> page = result.Value!;
        Assert.Equal([4, 6], page.Items.Select(issue => issue.Number));
        Assert.True(page.HasMore);
        Assert.Equal(7, page.PageCount);
        Assert.Null(page.TotalCount);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task ListPageAsync_OnTheLastPage_HasNoMore()
    {
        GitHubFakeHandler handler = new(_ => WithLink(GitHubFakeHandler.Json(new JsonArray(PullRequest(1))),
            $"<{Api}pulls?per_page=30&page=2>; rel=\"prev\", <{Api}pulls?per_page=30&page=1>; rel=\"first\""));

        Result<ItemPage<GitHubPullRequest>> result = await GitHubPullRequests.ListPageAsync("owner/repo", page: 3,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.EndsWith("&per_page=30&page=3", Assert.Single(handler.Urls));
        Assert.False(result.Value!.HasMore);
        Assert.Equal(3, result.Value.PageCount);
    }

    [Fact]
    public async Task ListPageAsync_WithoutALinkHeader_GoesByWhetherThePageIsFull()
    {
        GitHubFakeHandler full = new(_ => GitHubFakeHandler.Json(new JsonArray(PullRequest(1), PullRequest(2))));
        GitHubFakeHandler partial = new(_ => GitHubFakeHandler.Json(new JsonArray(PullRequest(1))));

        ItemPage<GitHubPullRequest> fullPage = (await GitHubPullRequests.ListPageAsync("owner/repo", pageSize: 2,
            httpClient: new HttpClient(full), cancellationToken: Token)).Value!;
        ItemPage<GitHubPullRequest> lastPage = (await GitHubPullRequests.ListPageAsync("owner/repo", page: 2,
            pageSize: 2, httpClient: new HttpClient(partial), cancellationToken: Token)).Value!;

        Assert.True(fullPage.HasMore);
        Assert.Null(fullPage.PageCount);
        Assert.False(lastPage.HasMore);
        Assert.Equal(2, lastPage.PageCount);
    }

    [Fact]
    public async Task SearchPageAsync_ReadsTheTotal_AndCountsOnlyTheReachablePages()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject
        {
            ["total_count"] = 2_500,
            ["items"] = new JsonArray(Issue(1), Issue(2)),
        }));
        HttpClient client = new(handler);

        Result<ItemPage<GitHubIssueInfo>> first = await GitHubIssues.SearchPageAsync("is:issue crash", 1, 100,
            httpClient: client, cancellationToken: Token);
        Result<ItemPage<GitHubIssueInfo>> last = await GitHubIssues.SearchPageAsync("is:issue crash", 10, 100,
            httpClient: client, cancellationToken: Token);

        Assert.Equal("https://api.github.com/search/issues?q=is%3Aissue%20crash&per_page=100&page=1",
            handler.Urls.First());
        Assert.Equal(2_500, first.Value!.TotalCount);
        Assert.Equal(10, first.Value.PageCount);
        Assert.True(first.Value.HasMore);
        Assert.Equal(2, first.Value.Items.Count);
        Assert.False(last.Value!.HasMore);
    }

    [Fact]
    public async Task SearchPageAsync_PastTheFirstThousandResults_Throws()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject
        {
            ["total_count"] = 0,
            ["items"] = new JsonArray(),
        }));
        HttpClient client = new(handler);

        Result<ItemPage<GitHubIssueInfo>> lastReachable = await GitHubIssues.SearchPageAsync("crash", 34, 30,
            httpClient: client, cancellationToken: Token);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.SearchPageAsync("crash", 35, 30,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.SearchPageAsync("crash", 11, 100,
            httpClient: client, cancellationToken: Token));

        Assert.True(lastReachable.IsSuccess);
        Assert.Equal(1, lastReachable.Value!.PageCount);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ListCommentsPageAsync_AsksForThePage()
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonArray(new JsonObject
        {
            ["id"] = 1,
            ["body"] = "Same here.",
        })));

        Result<ItemPage<GitHubComment>> result = await GitHubIssues.ListCommentsPageAsync("owner/repo", 42, 2, 5,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(Api + "issues/42/comments?per_page=5&page=2", Assert.Single(handler.Urls));
        Assert.Equal("Same here.", Assert.Single(result.Value!.Items).Body);
        Assert.False(result.Value.HasMore);
    }

    [Fact]
    public async Task ListPageAsync_FromTheCache_KeepsTheLinkHeader()
    {
        GitHubFakeHandler handler = new(_ => WithLink(GitHubFakeHandler.Json(new JsonArray(PullRequest(1))),
            $"<{Api}pulls?per_page=1&page=2>; rel=\"next\", <{Api}pulls?per_page=1&page=5>; rel=\"last\""));
        HttpClient client = new(handler);
        GitHubReadOptions cached = new() { CacheDirectory = this._dir, CacheDuration = TimeSpan.FromHours(1) };

        ItemPage<GitHubPullRequest> fetched = (await GitHubPullRequests.ListPageAsync("owner/repo", pageSize: 1,
            options: cached, httpClient: client, cancellationToken: Token)).Value!;
        ItemPage<GitHubPullRequest> again = (await GitHubPullRequests.ListPageAsync("owner/repo", pageSize: 1,
            options: cached, httpClient: client, cancellationToken: Token)).Value!;

        Assert.Single(handler.Requests);
        Assert.Equal(5, fetched.PageCount);
        Assert.Equal(5, again.PageCount);
        Assert.True(again.HasMore);
    }

    [Fact]
    public void SynchronousPages_SendSynchronously()
    {
        GitHubFakeHandler handler = new(request => request.Method == HttpMethod.Post
            ? Data(Owner("projectsV2", Connection([Project(1)], null, 1)))
            : GitHubFakeHandler.Json(new JsonArray()));
        HttpClient client = new(handler);

        Assert.True(GitHubIssues.ListPage("owner/repo", httpClient: client, cancellationToken: Token).IsSuccess);
        Assert.True(GitHubIssues.ListCommentsPage("owner/repo", 1, httpClient: client, cancellationToken: Token).IsSuccess);
        Assert.True(GitHubPullRequests.ListPage("owner/repo", httpClient: client, cancellationToken: Token).IsSuccess);
        Assert.True(GitHubProjects.ListPage("acme", Options, httpClient: client, cancellationToken: Token).IsSuccess);

        Assert.Equal(4, handler.SyncSends);
    }

    [Fact]
    public async Task ProjectsListPageAsync_SendsTheCursor_AndReadsTheTotal()
    {
        GitHubFakeHandler handler = new(_ => Data(Owner("projectsV2", Connection([Project(1), Project(2)], "c2", 60))));
        HttpClient client = new(handler);

        Result<ItemPage<GitHubProject>> first = await GitHubProjects.ListPageAsync("acme", Options, 25,
            httpClient: client, cancellationToken: Token);
        Result<ItemPage<GitHubProject>> second = await GitHubProjects.ListPageAsync("acme", Options, 25, "c2", client,
            Token);

        Assert.Collection(handler.Variables,
            variables =>
            {
                Assert.Equal(25, variables["first"]!.GetValue<int>());
                Assert.Null(variables["after"]);
            },
            variables => Assert.Equal("c2", variables["after"]!.GetValue<string>()));
        Assert.Contains("totalCount", handler.Bodies[0]);
        ItemPage<GitHubProject> page = first.Value!;
        Assert.Equal([1, 2], page.Items.Select(project => project.Number));
        Assert.True(page.HasMore);
        Assert.Equal("c2", page.NextCursor);
        Assert.Equal(60, page.TotalCount);
        Assert.Equal(3, page.PageCount);
        Assert.True(second.IsSuccess);
    }

    [Fact]
    public async Task ProjectsListItemsPageAsync_OnTheLastPage_HasNoCursor()
    {
        GitHubFakeHandler handler = new(_ => Data(new JsonObject
        {
            ["repositoryOwner"] = new JsonObject
            {
                ["projectV2"] = new JsonObject { ["items"] = Connection([Item("PVTI_1")], null, 1) },
            },
        }));

        Result<ItemPage<GitHubProjectItem>> result = await GitHubProjects.ListItemsPageAsync("acme", 5, Options,
            cursor: "c9", httpClient: new HttpClient(handler), cancellationToken: Token);

        JsonNode variables = Assert.Single(handler.Variables);
        Assert.Equal(5, variables["number"]!.GetValue<int>());
        Assert.Equal(30, variables["first"]!.GetValue<int>());
        Assert.Equal("c9", variables["after"]!.GetValue<string>());
        ItemPage<GitHubProjectItem> page = result.Value!;
        Assert.Equal("PVTI_1", Assert.Single(page.Items).Id);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
        Assert.Equal(1, page.PageCount);
    }

    [Fact]
    public async Task ProjectsListForRepositoryPageAsync_ReadsTheRepositorysProjects()
    {
        GitHubFakeHandler handler = new(_ => Data(new JsonObject
        {
            ["repository"] = new JsonObject { ["projectsV2"] = Connection([], null, 0) },
        }));

        Result<ItemPage<GitHubProject>> result = await GitHubProjects.ListForRepositoryPageAsync("acme/app", Options,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Empty(result.Value!.Items);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(1, result.Value.PageCount);
        Assert.Equal("app", Assert.Single(handler.Variables)["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task PageArguments_AreChecked_BeforeSending()
    {
        GitHubFakeHandler handler = new(_ => throw new InvalidOperationException("Nothing should be sent."));
        HttpClient client = new(handler);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.ListPageAsync("owner/repo", page: 0,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.ListPageAsync("owner/repo",
            pageSize: 101, httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubPullRequests.ListPageAsync("owner/repo",
            pageSize: 0, httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubIssues.ListCommentsPageAsync("owner/repo", 0,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => GitHubIssues.SearchPageAsync(" ", httpClient: client,
            cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubProjects.ListPageAsync("acme", Options, 101,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentException>(() => GitHubProjects.ListItemsPageAsync("acme", 1,
            new GitHubReadOptions(), httpClient: client, cancellationToken: Token));
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage WithLink(HttpResponseMessage response, string link)
    {
        response.Headers.TryAddWithoutValidation("Link", link);
        return response;
    }

    private static HttpResponseMessage Data(JsonObject data) => GitHubFakeHandler.Json(new JsonObject { ["data"] = data });

    private static JsonObject Owner(string property, JsonObject connection) => new()
    {
        ["repositoryOwner"] = new JsonObject { [property] = connection },
    };

    private static JsonObject Connection(JsonObject[] nodes, string? nextCursor, int totalCount) => new()
    {
        ["nodes"] = new JsonArray([.. nodes]),
        ["pageInfo"] = new JsonObject { ["hasNextPage"] = nextCursor is not null, ["endCursor"] = nextCursor },
        ["totalCount"] = totalCount,
    };

    private static JsonObject Project(int number) => new()
    {
        ["id"] = $"PVT_{number}",
        ["number"] = number,
        ["title"] = $"Roadmap {number}",
    };

    private static JsonObject Item(string id) => new()
    {
        ["id"] = id,
        ["type"] = "DRAFT_ISSUE",
        ["isArchived"] = false,
        ["content"] = new JsonObject { ["title"] = "Draft" },
        ["fieldValues"] = new JsonObject { ["nodes"] = new JsonArray() },
    };

    private static JsonObject Issue(int number) => new()
    {
        ["number"] = number,
        ["title"] = $"Issue {number}",
        ["state"] = "open",
        ["html_url"] = $"https://github.com/owner/repo/issues/{number}",
    };

    private static JsonObject PullRequest(int number) => new()
    {
        ["number"] = number,
        ["title"] = $"Pull {number}",
        ["state"] = "open",
    };
}
