using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace Niddy.Core.Tests;

// Nothing here reaches GitHub: every request goes to a fake handler.
public sealed class GitHubProjectsTests : IDisposable
{
    private const string GraphQL = "https://api.github.com/graphql";

    private static readonly GitHubReadOptions Options = new() { Token = "secret" };

    private readonly string _dir = Directory.CreateTempSubdirectory("niddy-projects-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(this._dir, recursive: true);

    [Fact]
    public async Task ListAsync_PostsTheQuery_AndReadsTheProjects()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProjects(Page([Project(5)]))));

        Result<IReadOnlyList<GitHubProject>> result = await GitHubProjects.ListAsync("acme", Options,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        HttpRequestMessage request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(GraphQL, request.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer secret", request.Headers.Authorization!.ToString());
        JsonNode body = JsonNode.Parse(handler.Bodies[0]!)!;
        Assert.Contains("repositoryOwner(login: $owner)", body["query"]!.GetValue<string>());
        Assert.Equal("acme", body["variables"]!["owner"]!.GetValue<string>());
        Assert.Equal(30, body["variables"]!["first"]!.GetValue<int>());
        Assert.Null(body["variables"]!["after"]);

        GitHubProject project = Assert.Single(result.Value!);
        Assert.Equal("PVT_5", project.Id);
        Assert.Equal(5, project.Number);
        Assert.Equal("Roadmap 5", project.Title);
        Assert.Equal("What's next", project.Description);
        Assert.Equal(new Uri("https://github.com/orgs/acme/projects/5"), project.Url);
        Assert.Equal("acme", project.Owner);
        Assert.False(project.IsClosed);
        Assert.True(project.IsPublic);
        Assert.Equal(12, project.ItemCount);
        Assert.Equal(new DateTimeOffset(2026, 5, 6, 0, 0, 0, TimeSpan.Zero), project.UpdatedAt);
    }

    [Fact]
    public async Task ListAsync_FollowsTheCursor()
    {
        int calls = 0;
        GitHubFakeHandler handler = new(_ => ++calls == 1
            ? Data(OwnerProjects(Page([.. Enumerable.Range(1, 100).Select(Project)], "cursor-1")))
            : Data(OwnerProjects(Page([Project(101), Project(102)]))));

        Result<IReadOnlyList<GitHubProject>> result = await GitHubProjects.ListAsync("acme", Options, 150,
            new HttpClient(handler), Token);

        Assert.Equal(102, result.Value!.Count);
        Assert.Collection(handler.Variables,
            variables =>
            {
                Assert.Equal(100, variables["first"]!.GetValue<int>());
                Assert.Null(variables["after"]);
            },
            variables =>
            {
                Assert.Equal(50, variables["first"]!.GetValue<int>());
                Assert.Equal("cursor-1", variables["after"]!.GetValue<string>());
            });
    }

    [Fact]
    public async Task ListAsync_StopsAtMaxCount()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProjects(Page([Project(1), Project(2), Project(3)], "more"))));

        Result<IReadOnlyList<GitHubProject>> result = await GitHubProjects.ListAsync("acme", Options, 2,
            new HttpClient(handler), Token);

        Assert.Equal(2, result.Value!.Count);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ListAsync_UnknownOwner_IsNotFound()
    {
        GitHubFakeHandler handler = new(_ => Data(new JsonObject { ["repositoryOwner"] = null }));

        Result<IReadOnlyList<GitHubProject>> result = await GitHubProjects.ListAsync("nobody", Options,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(GitHubProjects.ErrorCodes.NotFound, result.Error?.Code);
        Assert.Contains("nobody", result.Error?.Message);
    }

    [Theory]
    [InlineData("NOT_FOUND", GitHubProjects.ErrorCodes.NotFound)]
    [InlineData("FORBIDDEN", GitHubProjects.ErrorCodes.Forbidden)]
    [InlineData("INSUFFICIENT_SCOPES", GitHubProjects.ErrorCodes.Forbidden)]
    [InlineData("RATE_LIMITED", GitHubProjects.ErrorCodes.RateLimited)]
    [InlineData("SOMETHING_ELSE", GitHubProjects.ErrorCodes.Failed)]
    public async Task GraphQLErrors_AreFailures(string type, string code)
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject
        {
            ["data"] = new JsonObject { ["repositoryOwner"] = null },
            ["errors"] = new JsonArray(new JsonObject { ["type"] = type, ["message"] = "Nope" }),
        }));

        Result<GitHubProject> result = await GitHubProjects.GetAsync("acme", 5, Options, new HttpClient(handler), Token);

        Assert.Equal(code, result.Error?.Code);
        Assert.EndsWith("GitHub said: Nope", result.Error?.Message);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitHubProjects.ErrorCodes.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, GitHubProjects.ErrorCodes.Forbidden)]
    [InlineData(HttpStatusCode.BadGateway, GitHubProjects.ErrorCodes.Failed)]
    public async Task FailedStatus_IsAFailure(HttpStatusCode status, string code)
    {
        GitHubFakeHandler handler = new(_ => GitHubFakeHandler.Json(new JsonObject { ["message"] = "Nope" }, status));

        Result<GitHubProject> result = await GitHubProjects.GetAsync("acme", 5, Options, new HttpClient(handler), Token);

        Assert.Equal(code, result.Error?.Code);
    }

    [Fact]
    public async Task ListForRepositoryAsync_AsksTheRepository()
    {
        GitHubFakeHandler handler = new(_ => Data(new JsonObject
        {
            ["repository"] = new JsonObject { ["projectsV2"] = Page([Project(2)]) },
        }));

        Result<IReadOnlyList<GitHubProject>> result = await GitHubProjects.ListForRepositoryAsync("acme/app", Options,
            httpClient: new HttpClient(handler), cancellationToken: Token);

        Assert.Equal(2, Assert.Single(result.Value!).Number);
        JsonNode body = JsonNode.Parse(handler.Bodies[0]!)!;
        Assert.Contains("repository(owner: $owner, name: $name)", body["query"]!.GetValue<string>());
        Assert.Equal("acme", body["variables"]!["owner"]!.GetValue<string>());
        Assert.Equal("app", body["variables"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task GetAsync_ReadsTheProject()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(Project(5))));

        Result<GitHubProject> result = await GitHubProjects.GetAsync(" acme ", 5, Options, new HttpClient(handler), Token);

        Assert.Equal("Roadmap 5", result.Value!.Title);
        JsonNode variables = Assert.Single(handler.Variables);
        Assert.Equal("acme", variables["owner"]!.GetValue<string>());
        Assert.Equal(5, variables["number"]!.GetValue<int>());
    }

    [Fact]
    public async Task GetAsync_MissingProject_IsNotFound()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(null)));

        Result<GitHubProject> result = await GitHubProjects.GetAsync("acme", 9, Options, new HttpClient(handler), Token);

        Assert.Equal(GitHubProjects.ErrorCodes.NotFound, result.Error?.Code);
        Assert.Contains("#9", result.Error?.Message);
    }

    [Fact]
    public async Task ListItemsAsync_ReadsTheItemsAndTheirFields()
    {
        JsonObject issue = Item("I1", "ISSUE", new JsonObject
        {
            ["number"] = 3,
            ["title"] = "Crash on save",
            ["url"] = "https://github.com/acme/app/issues/3",
            ["issueState"] = "OPEN",
            ["repository"] = new JsonObject { ["nameWithOwner"] = "acme/app" },
        });
        issue["fieldValues"] = new JsonObject
        {
            ["nodes"] = new JsonArray(
                FieldValue("Title", "text", "Crash on save"),
                FieldValue("Status", "name", "In Progress"),
                FieldValue("Estimate", "number", 2.5),
                FieldValue("Due", "date", "2026-06-30"),
                FieldValue("Sprint", "title", "Sprint 4"),
                new JsonObject()),
        };
        JsonObject pull = Item("I2", "PULL_REQUEST", new JsonObject
        {
            ["number"] = 4,
            ["title"] = "Fix it",
            ["pullRequestState"] = "MERGED",
        });
        pull["isArchived"] = true;
        JsonObject draft = Item("I3", "DRAFT_ISSUE", new JsonObject { ["title"] = "Idea", ["body"] = "Maybe." });
        JsonObject redacted = Item("I4", "REDACTED", null);
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(new JsonObject
        {
            ["items"] = Page([issue, pull, draft, redacted]),
        })));

        CultureInfo culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        Result<IReadOnlyList<GitHubProjectItem>> result;
        try
        {
            result = await GitHubProjects.ListItemsAsync("acme", 5, Options, httpClient: new HttpClient(handler),
                cancellationToken: Token);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        JsonNode variables = Assert.Single(handler.Variables);
        Assert.Equal(5, variables["number"]!.GetValue<int>());
        Assert.Equal(100, variables["first"]!.GetValue<int>());

        Assert.Collection(result.Value!,
            item =>
            {
                Assert.Equal("I1", item.Id);
                Assert.Equal(GitHubProjectItemType.Issue, item.Type);
                Assert.Equal("Crash on save", item.Title);
                Assert.Equal(3, item.Number);
                Assert.Equal(new Uri("https://github.com/acme/app/issues/3"), item.Url);
                Assert.Equal("acme/app", item.Repository);
                Assert.Equal(GitHubItemState.Open, item.State);
                Assert.False(item.IsArchived);
                Assert.Equal("In Progress", item.Status);
                Assert.Equal("2.5", item.GetField("estimate"));
                Assert.Equal("2026-06-30", item.GetField("Due"));
                Assert.Equal("Sprint 4", item.GetField("Sprint"));
                Assert.Equal("Crash on save", item.GetField("Title"));
                Assert.Null(item.GetField("Priority"));
                Assert.Equal(5, item.Fields.Count);
            },
            item =>
            {
                Assert.Equal(GitHubProjectItemType.PullRequest, item.Type);
                Assert.Equal(GitHubItemState.Merged, item.State);
                Assert.True(item.IsArchived);
                Assert.Null(item.Status);
            },
            item =>
            {
                Assert.Equal(GitHubProjectItemType.DraftIssue, item.Type);
                Assert.Equal("Idea", item.Title);
                Assert.Equal("Maybe.", item.Body);
                Assert.Null(item.Number);
                Assert.Null(item.State);
            },
            item =>
            {
                Assert.Equal(GitHubProjectItemType.Redacted, item.Type);
                Assert.Null(item.Title);
                Assert.Empty(item.Fields);
            });
    }

    [Fact]
    public async Task ListFieldsAsync_ReadsOptionsAndIterations()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(new JsonObject
        {
            ["fields"] = new JsonObject
            {
                ["nodes"] = new JsonArray(
                    new JsonObject { ["id"] = "F1", ["name"] = "Title", ["dataType"] = "TITLE" },
                    new JsonObject
                    {
                        ["id"] = "F2",
                        ["name"] = "Status",
                        ["dataType"] = "SINGLE_SELECT",
                        ["options"] = new JsonArray(new JsonObject { ["name"] = "Todo" }, new JsonObject { ["name"] = "Done" }),
                    },
                    new JsonObject
                    {
                        ["id"] = "F3",
                        ["name"] = "Sprint",
                        ["dataType"] = "ITERATION",
                        ["configuration"] = new JsonObject
                        {
                            ["iterations"] = new JsonArray(new JsonObject { ["title"] = "Sprint 5" }),
                            ["completedIterations"] = new JsonArray(new JsonObject { ["title"] = "Sprint 4" }),
                        },
                    }),
            },
        })));

        Result<IReadOnlyList<GitHubProjectField>> result = await GitHubProjects.ListFieldsAsync("acme", 5, Options,
            new HttpClient(handler), Token);

        Assert.Collection(result.Value!,
            field =>
            {
                Assert.Equal("F1", field.Id);
                Assert.Equal("Title", field.Name);
                Assert.Equal("TITLE", field.DataType);
                Assert.Empty(field.Options);
            },
            field => Assert.Equal(["Todo", "Done"], field.Options),
            field => Assert.Equal(["Sprint 5", "Sprint 4"], field.Options));
    }

    [Fact]
    public async Task ListFieldsAsync_MissingProject_IsNotFound()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(null)));

        Result<IReadOnlyList<GitHubProjectField>> result = await GitHubProjects.ListFieldsAsync("acme", 5, Options,
            new HttpClient(handler), Token);

        Assert.Equal(GitHubProjects.ErrorCodes.NotFound, result.Error?.Code);
    }

    [Theory]
    [InlineData("https://ghe.example.com/api/v3/", "https://ghe.example.com/api/graphql")]
    [InlineData("https://ghe.example.com/api/v3", "https://ghe.example.com/api/graphql")]
    [InlineData("https://proxy.example.com/github/", "https://proxy.example.com/github/graphql")]
    public async Task GraphQLAddress_FollowsTheClientsApiAddress(string api, string graphQL)
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(Project(1))));

        await GitHubProjects.GetAsync("acme", 1, Options, new HttpClient(handler) { BaseAddress = new Uri(api) }, Token);

        Assert.Equal(graphQL, Assert.Single(handler.Urls));
    }

    [Fact]
    public async Task Cache_FreshResponse_IsServedWithoutARequest_ButErrorsAreNot()
    {
        bool fail = true;
        GitHubFakeHandler handler = new(_ => fail
            ? GitHubFakeHandler.Json(new JsonObject
            {
                ["errors"] = new JsonArray(new JsonObject { ["type"] = "SOMETHING", ["message"] = "Try again" }),
            })
            : Data(OwnerProject(Project(1))));
        GitHubReadOptions options = Options with { CacheDirectory = this._dir, CacheDuration = TimeSpan.FromHours(1) };
        HttpClient client = new(handler);

        Result<GitHubProject> failed = await GitHubProjects.GetAsync("acme", 1, options, client, Token);
        fail = false;
        Result<GitHubProject> first = await GitHubProjects.GetAsync("acme", 1, options, client, Token);
        Result<GitHubProject> cached = await GitHubProjects.GetAsync("acme", 1, options, client, Token);
        Result<GitHubProject> other = await GitHubProjects.GetAsync("acme", 2, options, client, Token);

        Assert.False(failed.IsSuccess);
        Assert.True(first.IsSuccess);
        Assert.Equal(1, cached.Value!.Number);
        Assert.True(other.IsSuccess);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Cache_Offline_FallsBackToTheCachedResponse()
    {
        bool online = true;
        GitHubFakeHandler handler = new(_ => online
            ? Data(OwnerProject(Project(1)))
            : throw new HttpRequestException("No such host"));
        GitHubReadOptions options = Options with { CacheDirectory = this._dir };
        HttpClient client = new(handler);

        await GitHubProjects.GetAsync("acme", 1, options, client, Token);
        online = false;
        Result<GitHubProject> offline = await GitHubProjects.GetAsync("acme", 1, options, client, Token);
        Result<GitHubProject> uncached = await GitHubProjects.GetAsync("acme", 2, options, client, Token);

        Assert.Equal(1, offline.Value!.Number);
        Assert.Equal(GitHubProjects.ErrorCodes.Network, uncached.Error?.Code);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task Cache_IsPerToken()
    {
        GitHubFakeHandler handler = new(_ => Data(OwnerProject(Project(1))));
        GitHubReadOptions options = Options with { CacheDirectory = this._dir, CacheDuration = TimeSpan.FromHours(1) };
        HttpClient client = new(handler);

        await GitHubProjects.GetAsync("acme", 1, options, client, Token);
        await GitHubProjects.GetAsync("acme", 1, options with { Token = "other" }, client, Token);

        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public void SyncVersions_SendSynchronously()
    {
        GitHubFakeHandler handler = new(_ =>
        {
            // One answer for every query: the project, its items and its fields.
            JsonObject project = Project(1);
            project["items"] = Page([Item("I1", "DRAFT_ISSUE", new JsonObject { ["title"] = "Idea" })]);
            project["fields"] = new JsonObject
            {
                ["nodes"] = new JsonArray(new JsonObject { ["id"] = "F1", ["name"] = "Title", ["dataType"] = "TITLE" }),
            };
            return Data(new JsonObject
            {
                ["repositoryOwner"] = new JsonObject
                {
                    ["projectsV2"] = Page([Project(1)]),
                    ["projectV2"] = project,
                },
                ["repository"] = new JsonObject { ["projectsV2"] = Page([Project(1)]) },
            });
        });
        HttpClient client = new(handler);

        Assert.Single(GitHubProjects.List("acme", Options, httpClient: client, cancellationToken: Token).Value!);
        Assert.Single(GitHubProjects.ListForRepository("acme/app", Options, httpClient: client, cancellationToken: Token)
            .Value!);
        Assert.Equal(1, GitHubProjects.Get("acme", 1, Options, client, Token).Value!.Number);
        Assert.Equal("Idea",
            Assert.Single(GitHubProjects.ListItems("acme", 1, Options, httpClient: client, cancellationToken: Token).Value!)
                .Title);
        Assert.Equal("Title", Assert.Single(GitHubProjects.ListFields("acme", 1, Options, client, Token).Value!).Name);
        Assert.Equal(5, handler.SyncSends);
    }

    [Fact]
    public async Task BadArguments_Throw_WithoutARequest()
    {
        GitHubFakeHandler handler = new(_ => throw new InvalidOperationException("No request expected."));
        HttpClient client = new(handler);

        await Assert.ThrowsAsync<ArgumentNullException>(() => GitHubProjects.ListAsync("acme", null!, httpClient: client,
            cancellationToken: Token));
        ArgumentException noToken = await Assert.ThrowsAsync<ArgumentException>(() => GitHubProjects.ListAsync("acme",
            new GitHubReadOptions(), httpClient: client, cancellationToken: Token));
        Assert.Contains("token", noToken.Message);
        await Assert.ThrowsAsync<ArgumentException>(() => GitHubProjects.GetAsync(" ", 1, Options, client, Token));
        await Assert.ThrowsAsync<ArgumentException>(() => GitHubProjects.ListForRepositoryAsync("acme", Options,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubProjects.ListItemsAsync("acme", 0, Options,
            httpClient: client, cancellationToken: Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubProjects.ListItemsAsync("acme", 1, Options, 0,
            client, Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => GitHubProjects.ListAsync("acme", Options, 0, client,
            Token));
        Assert.Empty(handler.Requests);
    }

    private static HttpResponseMessage Data(JsonObject data) => GitHubFakeHandler.Json(new JsonObject { ["data"] = data });

    private static JsonObject OwnerProjects(JsonObject page) => new()
    {
        ["repositoryOwner"] = new JsonObject { ["projectsV2"] = page },
    };

    private static JsonObject OwnerProject(JsonObject? project) => new()
    {
        ["repositoryOwner"] = new JsonObject { ["projectV2"] = project },
    };

    private static JsonObject Page(JsonObject[] nodes, string? nextCursor = null) => new()
    {
        ["nodes"] = new JsonArray([.. nodes]),
        ["pageInfo"] = new JsonObject { ["hasNextPage"] = nextCursor is not null, ["endCursor"] = nextCursor },
    };

    private static JsonObject Project(int number) => new()
    {
        ["id"] = $"PVT_{number}",
        ["number"] = number,
        ["title"] = $"Roadmap {number}",
        ["shortDescription"] = "What's next",
        ["url"] = $"https://github.com/orgs/acme/projects/{number}",
        ["closed"] = false,
        ["public"] = true,
        ["createdAt"] = "2026-01-01T00:00:00Z",
        ["updatedAt"] = "2026-05-06T00:00:00Z",
        ["owner"] = new JsonObject { ["login"] = "acme" },
        ["items"] = new JsonObject { ["totalCount"] = 12 },
    };

    private static JsonObject Item(string id, string type, JsonObject? content) => new()
    {
        ["id"] = id,
        ["type"] = type,
        ["isArchived"] = false,
        ["content"] = content,
        ["fieldValues"] = new JsonObject { ["nodes"] = new JsonArray() },
    };

    private static JsonObject FieldValue(string field, string property, JsonNode value) => new()
    {
        [property] = value,
        ["field"] = new JsonObject { ["name"] = field },
    };
}
