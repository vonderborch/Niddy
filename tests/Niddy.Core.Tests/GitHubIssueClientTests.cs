using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Niddy.Core.GitHub;
using Niddy.Core.Results;

namespace Niddy.Core.Tests;

// Nothing here reaches GitHub: every request goes to a fake handler that answers by address.
public class GitHubIssueClientTests
{
    private const string Created = """{"number": 42, "html_url": "https://github.com/owner/repo/issues/42"}""";

    private const string Commented = """{"html_url": "https://github.com/owner/repo/issues/7#issuecomment-99"}""";

    private static readonly GitHubIssue Fingerprinted = new() { Title = "Crash", Body = "It crashed.", Fingerprint = "save-crash" };

    [Fact]
    public void FromException_FingerprintsTheTypeAndWhereItWasThrown()
    {
        GitHubIssue first = GitHubIssue.FromException(Thrown(() => ThrowFromHere("C:\\Users\\jane\\a.txt")));
        GitHubIssue second = GitHubIssue.FromException(Thrown(() => ThrowFromHere("/home/bob/b.txt")));
        GitHubIssue elsewhere = GitHubIssue.FromException(Thrown(() => throw new IOException("x")));

        Assert.StartsWith("System.IO.IOException at ", first.Fingerprint);
        Assert.Contains(nameof(ThrowFromHere), first.Fingerprint);
        Assert.DoesNotContain("jane", first.Fingerprint);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.NotEqual(first.Fingerprint, elsewhere.Fingerprint);
    }

    [Fact]
    public void FromException_FingerprintsAnUnthrownExceptionByItsMessage()
    {
        GitHubIssue issue = GitHubIssue.FromException(new AggregateException(new IOException("Disk full\nmore")));

        Assert.Equal("System.IO.IOException at Disk full", issue.Fingerprint);
    }

    [Fact]
    public void FormatBody_EndsWithAHiddenMarkerOfTheFingerprintsHash()
    {
        string body = Fingerprinted.FormatBody();

        Assert.Matches(@"^It crashed\.\n\n<!-- fingerprint: nfp[0-9a-f]{16} -->$", body);
        Assert.DoesNotContain("save-crash", body);
        Assert.Equal(body, (Fingerprinted with { Title = "Other", Body = "It crashed." }).FormatBody());
        Assert.NotEqual(body, (Fingerprinted with { Fingerprint = "load-crash" }).FormatBody());
        Assert.Equal("It crashed.", (Fingerprinted with { Fingerprint = "  " }).FormatBody());
    }

    [Fact]
    public void FormatBody_KeepsTheMarkerWhenCut()
    {
        GitHubIssue issue = Fingerprinted with { Body = new string('x', 5_000) };

        string cut = issue.FormatBody(500);

        Assert.True(cut.Length <= 500);
        Assert.EndsWith(" -->", cut);
        Assert.Contains("fingerprint: nfp", cut);
    }

    [Fact]
    public async Task CreateAsync_CommentsOnTheOpenIssueWithTheSameFingerprint()
    {
        RoutedHandler handler = new(request => request.Method == HttpMethod.Get
            ? Respond(HttpStatusCode.OK, SearchResults(Item(7, "Crash", Fingerprinted.FormatBody())))
            : Respond(HttpStatusCode.Created, Commented));

        Result<GitHubIssueReference> result = await GitHubIssues.CreateAsync("owner/repo", "secret", Fingerprinted,
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        GitHubIssueReference issue = result.Value!;
        Assert.Equal(7, issue.Number);
        Assert.Equal("https://github.com/owner/repo/issues/7", issue.Url.AbsoluteUri);
        Assert.True(issue.AddedAsComment);
        Assert.Equal("https://github.com/owner/repo/issues/7#issuecomment-99", issue.CommentUrl!.AbsoluteUri);

        Assert.Equal(2, handler.Requests.Count);
        string search = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.Query);
        Assert.StartsWith("https://api.github.com/search/issues?", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Contains("repo:owner/repo is:issue is:open", search);
        Assert.Contains($"\"{Fingerprinted.FingerprintToken}\"", search);
        Assert.Equal("Bearer secret", handler.Requests[0].Headers.Authorization!.ToString());

        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues/7/comments", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.Equal("It crashed.", (string?)JsonNode.Parse(handler.Bodies[1])!["body"]);
    }

    [Fact]
    public async Task CreateAsync_CommentStartsWithTheTitleWhenItDiffers()
    {
        RoutedHandler handler = new(request => request.Method == HttpMethod.Get
            ? Respond(HttpStatusCode.OK, SearchResults(Item(7, "Crash: first message", Fingerprinted.FormatBody())))
            : Respond(HttpStatusCode.Created, Commented));

        await GitHubIssues.CreateAsync("owner/repo", "secret", Fingerprinted with { Title = "Crash: second message" },
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.Equal("**Crash: second message**\n\nIt crashed.", (string?)JsonNode.Parse(handler.Bodies[1])!["body"]);
    }

    [Fact]
    public async Task CreateAsync_IgnoresSearchResultsWithoutTheMarker()
    {
        string near = SearchResults(
            Item(5, "Crash", "Mentions nfp but not the marker"),
            Item(6, "Crash", Fingerprinted.FormatBody(), state: "closed"),
            Item(8, "Crash", Fingerprinted.FormatBody(), pullRequest: true));
        RoutedHandler handler = new(request => request.Method == HttpMethod.Get
            ? Respond(HttpStatusCode.OK, near)
            : Respond(HttpStatusCode.Created, Created));

        Result<GitHubIssueReference> result = await GitHubIssues.CreateAsync("owner/repo", "secret", Fingerprinted,
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value!.Number);
        Assert.False(result.Value.AddedAsComment);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues", handler.Requests[1].RequestUri!.AbsoluteUri);
        Assert.EndsWith(" -->", (string?)JsonNode.Parse(handler.Bodies[1])!["body"]);
    }

    public static TheoryData<string> Failures => ["search", "search-network", "comment"];

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task CreateAsync_OpensANewIssueWhenTheSearchOrCommentFails(string failure)
    {
        RoutedHandler handler = new(request =>
        {
            bool isSearch = request.Method == HttpMethod.Get;
            bool isComment = request.RequestUri!.AbsolutePath.EndsWith("/comments", StringComparison.Ordinal);
            return (failure, isSearch, isComment) switch
            {
                ("search", true, _) => Respond(HttpStatusCode.Forbidden, """{"message": "API rate limit exceeded"}"""),
                ("search-network", true, _) => throw new HttpRequestException("No route"),
                (_, true, _) => Respond(HttpStatusCode.OK, SearchResults(Item(7, "Crash", Fingerprinted.FormatBody()))),
                ("comment", _, true) => Respond(HttpStatusCode.Forbidden, """{"message": "Issue is locked"}"""),
                _ => Respond(HttpStatusCode.Created, Created),
            };
        });

        Result<GitHubIssueReference> result = await GitHubIssues.CreateAsync("owner/repo", "secret", Fingerprinted,
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value!.Number);
        Assert.False(result.Value.AddedAsComment);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues", handler.Requests[^1].RequestUri!.AbsoluteUri);
    }

    [Fact]
    public void Create_SearchesAndCommentsSynchronously()
    {
        RoutedHandler handler = new(request => request.Method == HttpMethod.Get
            ? Respond(HttpStatusCode.OK, SearchResults(Item(7, "Crash", Fingerprinted.FormatBody())))
            : Respond(HttpStatusCode.Created, Commented));

        Result<GitHubIssueReference> result = GitHubIssues.Create("owner/repo", "secret", Fingerprinted,
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.True(result.Value!.AddedAsComment);
        Assert.Equal(2, handler.SyncSends);
    }

    [Fact]
    public async Task CreateAsync_WithoutAFingerprintDoesntSearch()
    {
        RoutedHandler handler = new(_ => Respond(HttpStatusCode.Created, Created));

        await GitHubIssues.CreateAsync("owner/repo", "secret", Fingerprinted with { Fingerprint = null },
            new HttpClient(handler), TestContext.Current.CancellationToken);

        Assert.Equal(HttpMethod.Post, Assert.Single(handler.Requests).Method);
    }

    [Fact]
    public void Constructor_NormalizesTheRepository()
    {
        GitHubIssueClient client = new(" owner/repo ", "  ");

        Assert.Equal("owner/repo", client.Repository);
        Assert.False(client.HasToken);
        Assert.True(new GitHubIssueClient("owner/repo", "secret").HasToken);
        Assert.Throws<ArgumentException>(() => new GitHubIssueClient("not a repo"));
    }

    [Fact]
    public async Task CreateAsync_UsesTheClientsSettings()
    {
        RoutedHandler handler = new(_ => Respond(HttpStatusCode.Created, Created));
        GitHubIssueClient client = new("owner/repo", "secret", new HttpClient(handler))
        {
            Reporter = new GitHubIssueReporter(Name: "Jane"),
        };

        Result<GitHubIssueReference> result = await client.CreateAsync(new GitHubIssue { Title = "T" },
            TestContext.Current.CancellationToken);
        await client.CreateAsync(new GitHubIssue { Title = "T", Reporter = new GitHubIssueReporter(Name: "Bob") },
            TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value!.Number);
        Assert.Equal("https://api.github.com/repos/owner/repo/issues", handler.Requests[0].RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer secret", handler.Requests[0].Headers.Authorization!.ToString());
        Assert.Equal("**Reported by:** Jane", (string?)JsonNode.Parse(handler.Bodies[0])!["body"]);
        Assert.Equal("**Reported by:** Bob", (string?)JsonNode.Parse(handler.Bodies[1])!["body"]);
    }

    [Fact]
    public void Create_WorksSynchronously()
    {
        RoutedHandler handler = new(_ => Respond(HttpStatusCode.Created, Created));

        Result<GitHubIssueReference> result = new GitHubIssueClient("owner/repo", "secret", new HttpClient(handler))
            .Create(new GitHubIssue { Title = "T" }, TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value!.Number);
        Assert.Equal(1, handler.SyncSends);
    }

    [Fact]
    public async Task CreateAsync_WithoutATokenThrows()
    {
        GitHubIssueClient client = new("owner/repo");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.CreateAsync(new GitHubIssue { Title = "T" }, TestContext.Current.CancellationToken));
        Assert.Throws<InvalidOperationException>(() =>
            client.Create(new GitHubIssue { Title = "T" }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateOrOpenAsync_WithoutATokenOpensTheFormWithOpenUrl()
    {
        List<string> opened = [];
        GitHubIssueClient client = new("owner/repo")
        {
            Reporter = new GitHubIssueReporter(Name: "Jane"),
            BaseAddress = new Uri("https://github.example.com/"),
            OpenUrl = opened.Add,
        };

        Result<GitHubIssueSubmission> result = await client.CreateOrOpenAsync(new GitHubIssue { Title = "T" },
            TestContext.Current.CancellationToken);
        Result<GitHubIssueSubmission> sync = client.CreateOrOpen(new GitHubIssue { Title = "T" },
            TestContext.Current.CancellationToken);

        Assert.True(result.Value!.OpenedInBrowser);
        Assert.True(sync.Value!.OpenedInBrowser);
        Assert.Equal(2, opened.Count);
        Assert.StartsWith("https://github.example.com/owner/repo/issues/new?title=T", opened[0]);
        Assert.Contains(Uri.EscapeDataString("**Reported by:** Jane"), opened[0]);
    }

    [Fact]
    public async Task CreateOrOpenAsync_WithATokenUsesTheApi()
    {
        RoutedHandler handler = new(_ => Respond(HttpStatusCode.Created, Created));
        List<string> opened = [];
        GitHubIssueClient client = new("owner/repo", "secret", new HttpClient(handler)) { OpenUrl = opened.Add };

        Result<GitHubIssueSubmission> result = await client.CreateOrOpenAsync(new GitHubIssue { Title = "T" },
            TestContext.Current.CancellationToken);

        Assert.Equal(42, result.Value!.Issue!.Number);
        Assert.Empty(opened);
    }

    [Fact]
    public void OpenInBrowser_UsesOpenUrlAndTheClientsSettings()
    {
        List<string> opened = [];
        GitHubIssueClient client = new("owner/repo")
        {
            Reporter = new GitHubIssueReporter(Name: "Jane"),
            OpenUrl = opened.Add,
        };

        client.OpenInBrowser(new GitHubIssue { Title = "T" });

        Assert.Equal(client.GetNewIssueUrl(new GitHubIssue { Title = "T" }), Assert.Single(opened));
        Assert.StartsWith("https://github.com/owner/repo/issues/new?title=T", opened[0]);
        Assert.Contains(Uri.EscapeDataString("Jane"), opened[0]);
    }

    private static void ThrowFromHere(string path) => throw new IOException($"Can't write {path}");

    private static Exception Thrown(Action action)
    {
        try
        {
            action();
        }
        catch (Exception caught)
        {
            return caught;
        }

        throw new InvalidOperationException("Nothing was thrown.");
    }

    private static JsonObject Item(int number, string title, string body, string state = "open", bool pullRequest = false)
    {
        JsonObject item = new()
        {
            ["number"] = number,
            ["title"] = title,
            ["state"] = state,
            ["body"] = body,
            ["html_url"] = $"https://github.com/owner/repo/issues/{number}",
        };
        if (pullRequest)
        {
            item["pull_request"] = new JsonObject();
        }

        return item;
    }

    private static string SearchResults(params JsonObject[] items) =>
        new JsonObject { ["total_count"] = items.Length, ["items"] = new JsonArray(items) }.ToJsonString();

    private static HttpResponseMessage Respond(HttpStatusCode status, string content) =>
        new(status) { Content = new StringContent(content, Encoding.UTF8, "application/json") };

    private sealed class RoutedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<string> Bodies { get; } = [];

        public int SyncSends { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return this.Respond(request, body);
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

            return this.Respond(request, body);
        }

        private HttpResponseMessage Respond(HttpRequestMessage request, string body)
        {
            this.Requests.Add(request);
            this.Bodies.Add(body);
            return respond(request);
        }
    }
}
