using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using Niddy.Core.Collections;
using Niddy.Core.Results;
using Niddy.Core.Web;

namespace Niddy.Core.GitHub;

/// <summary>
/// Reads GitHub projects (the current kind, Projects v2) through GitHub's GraphQL API: a user's or organization's
/// projects (<see cref="ListAsync"/>), a repository's (<see cref="ListForRepositoryAsync"/>), one project
/// (<see cref="GetAsync"/>), its items with their field values (<see cref="ListItemsAsync"/>), and its fields
/// (<see cref="ListFieldsAsync"/>). The lists also come a page at a time, for an app that pages them itself
/// (<see cref="ListPageAsync"/>, <see cref="ListForRepositoryPageAsync"/>, <see cref="ListItemsPageAsync"/>).
/// </summary>
/// <remarks>
/// GitHub's GraphQL API needs a token, even for public projects, so every method takes
/// <see cref="GitHubReadOptions"/> with a <see cref="GitHubReadOptions.Token"/>. A fine-grained token needs the
/// Projects (read) permission on the owner; a classic one needs the <c>read:project</c> scope. Classic projects were
/// retired by GitHub and aren't supported.
/// </remarks>
public static class GitHubProjects
{
    /// <summary>The <see cref="Error.Code"/> values the reads fail with. They're the same as <see cref="GitHubIssues.ErrorCodes"/>'.</summary>
    public static class ErrorCodes
    {
        /// <summary>401: the token is invalid or expired.</summary>
        public const string Unauthorized = GitHubIssues.ErrorCodes.Unauthorized;

        /// <summary>The token can't read the project: it lacks the Projects permission or the <c>read:project</c> scope.</summary>
        public const string Forbidden = GitHubIssues.ErrorCodes.Forbidden;

        /// <summary>
        /// GitHub's rate limits, with nothing cached to fall back on. Try again later. GraphQL counts query cost, not
        /// requests: 5,000 points an hour per token.
        /// </summary>
        public const string RateLimited = GitHubIssues.ErrorCodes.RateLimited;

        /// <summary>The owner, repository or project doesn't exist, or the token can't see it.</summary>
        public const string NotFound = GitHubIssues.ErrorCodes.NotFound;

        /// <summary>Any other response or GraphQL error, or a success response that couldn't be read.</summary>
        public const string Failed = GitHubIssues.ErrorCodes.Failed;

        /// <summary>
        /// The request didn't get a response, with nothing cached to fall back on: no connection, DNS failure, or a
        /// timeout.
        /// </summary>
        public const string Network = GitHubIssues.ErrorCodes.Network;
    }

    private const string ProjectsSelection = """
        projectsV2(first: $first, after: $after, orderBy: { field: UPDATED_AT, direction: DESC }) {
          nodes { ...Project }
          pageInfo { hasNextPage endCursor }
          totalCount
        }
        """;

    private static readonly string OwnerProjectsQuery = $$"""
        query($owner: String!, $first: Int!, $after: String) {
          repositoryOwner(login: $owner) { ... on ProjectV2Owner { {{ProjectsSelection}} } }
        }
        {{GitHubProject.Fragment}}
        """;

    private static readonly string RepositoryProjectsQuery = $$"""
        query($owner: String!, $name: String!, $first: Int!, $after: String) {
          repository(owner: $owner, name: $name) { {{ProjectsSelection}} }
        }
        {{GitHubProject.Fragment}}
        """;

    private static readonly string ProjectQuery = $$"""
        query($owner: String!, $number: Int!) {
          repositoryOwner(login: $owner) { ... on ProjectV2Owner { projectV2(number: $number) { ...Project } } }
        }
        {{GitHubProject.Fragment}}
        """;

    private static readonly string ItemsQuery = $$"""
        query($owner: String!, $number: Int!, $first: Int!, $after: String) {
          repositoryOwner(login: $owner) {
            ... on ProjectV2Owner {
              projectV2(number: $number) {
                items(first: $first, after: $after) { nodes { ...Item } pageInfo { hasNextPage endCursor } totalCount }
              }
            }
          }
        }
        {{GitHubProjectItem.Fragment}}
        """;

    private static readonly string FieldsQuery = $$"""
        query($owner: String!, $number: Int!) {
          repositoryOwner(login: $owner) {
            ... on ProjectV2Owner {
              projectV2(number: $number) { fields(first: 100) { nodes { {{GitHubProjectField.Selection}} } } }
            }
          }
        }
        """;

    /// <summary>
    /// A user's or organization's projects, most recently updated first, closed ones included (see
    /// <see cref="GitHubProject.IsClosed"/>).
    /// </summary>
    /// <param name="owner">The user's or organization's login, e.g. <c>octocat</c>.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="maxCount">The most to return. It reads up to 100 per request, as many pages as it needs.</param>
    /// <param name="httpClient">
    /// The client to send with, or null for a shared one. The REST API address is <paramref name="options"/>'
    /// <see cref="GitHubReadOptions.ApiAddress"/>, else the client's <see cref="HttpClient.BaseAddress"/>, else
    /// <c>https://api.github.com/</c>, and GraphQL's is worked out from it: <c>https://github.example.com/api/v3/</c>
    /// for GitHub Enterprise Server sends to <c>https://github.example.com/api/graphql</c>, and
    /// <c>https://api.github.com/</c> to <c>https://api.github.com/graphql</c>.
    /// </param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The projects, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: an owner that
    /// doesn't exist is <see cref="ErrorCodes.NotFound"/>. GraphQL errors' messages include GitHub's.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="options"/>' token is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubProject>>> ListAsync(string owner, GitHubReadOptions options,
        int maxCount = 30, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListCoreAsync(owner, options, maxCount, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// A user's or organization's projects, blocking until they're read. Otherwise the same as <see cref="ListAsync"/>.
    /// Like <see cref="WebContent"/>'s synchronous methods it doesn't work in the browser, and a client you pass needs
    /// a handler that supports synchronous sends.
    /// </summary>
    /// <inheritdoc cref="ListAsync" path="/param"/>
    /// <inheritdoc cref="ListAsync" path="/returns"/>
    /// <inheritdoc cref="ListAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubProject>> List(string owner, GitHubReadOptions options, int maxCount = 30,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListCoreAsync(owner, options, maxCount, httpClient, true, cancellationToken));

    /// <summary>
    /// The projects linked to a repository, most recently updated first, closed ones included. Projects belong to a
    /// user or organization; this is the ones shown on the repository's Projects tab.
    /// </summary>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="maxCount">The most to return. It reads up to 100 per request, as many pages as it needs.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The projects, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a repository that
    /// doesn't exist is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, or <paramref name="options"/>' token is blank.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubProject>>> ListForRepositoryAsync(string repository,
        GitHubReadOptions options, int maxCount = 30, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListForRepositoryCoreAsync(repository, options, maxCount, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// The projects linked to a repository, blocking until they're read. Otherwise the same as
    /// <see cref="ListForRepositoryAsync"/>; see <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListForRepositoryAsync" path="/param"/>
    /// <inheritdoc cref="ListForRepositoryAsync" path="/returns"/>
    /// <inheritdoc cref="ListForRepositoryAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubProject>> ListForRepository(string repository, GitHubReadOptions options,
        int maxCount = 30, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListForRepositoryCoreAsync(repository, options, maxCount, httpClient, true,
            cancellationToken));

    /// <summary>One project by its owner and number.</summary>
    /// <param name="owner">The login of the user or organization that owns it.</param>
    /// <param name="number">The project number: the <c>5</c> in <c>github.com/orgs/acme/projects/5</c>.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The project, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a missing owner
    /// or project is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="options"/>' token is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<GitHubProject>> GetAsync(string owner, int number, GitHubReadOptions options,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        GetCoreAsync(owner, number, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One project, blocking until it's read. Otherwise the same as <see cref="GetAsync"/>; see <see cref="List"/> for
    /// where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="GetAsync" path="/param"/>
    /// <inheritdoc cref="GetAsync" path="/returns"/>
    /// <inheritdoc cref="GetAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<GitHubProject> Get(string owner, int number, GitHubReadOptions options,
        HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(GetCoreAsync(owner, number, options, httpClient, true, cancellationToken));

    /// <summary>
    /// A project's items, in the project's own order (not a view's), with up to 50 field values each. Archived items
    /// may be included; check <see cref="GitHubProjectItem.IsArchived"/>.
    /// </summary>
    /// <param name="owner">The login of the user or organization that owns the project.</param>
    /// <param name="number">The project number.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="maxCount">The most to return. It reads up to 100 per request, as many pages as it needs.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <returns>
    /// The items, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a missing owner or
    /// project is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="options"/>' token is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> or <paramref name="maxCount"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubProjectItem>>> ListItemsAsync(string owner, int number,
        GitHubReadOptions options, int maxCount = 100, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListItemsCoreAsync(owner, number, options, maxCount, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// A project's items, blocking until they're read. Otherwise the same as <see cref="ListItemsAsync"/>; see
    /// <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListItemsAsync" path="/param"/>
    /// <inheritdoc cref="ListItemsAsync" path="/returns"/>
    /// <inheritdoc cref="ListItemsAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubProjectItem>> ListItems(string owner, int number,
        GitHubReadOptions options, int maxCount = 100, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListItemsCoreAsync(owner, number, options, maxCount, httpClient, true, cancellationToken));

    /// <summary>
    /// A project's fields, built-in ones (Title, Assignees, Status…) included, with single-select options and
    /// iterations. Use it to show a field's possible values, e.g. the columns of a board grouped by Status.
    /// </summary>
    /// <param name="owner">The login of the user or organization that owns the project.</param>
    /// <param name="number">The project number.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>
    /// The fields, or a failure whose <see cref="Error.Code"/> is one of <see cref="ErrorCodes"/>: a missing owner or
    /// project is <see cref="ErrorCodes.NotFound"/>.
    /// </returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="options"/>' token is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is less than 1.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<IReadOnlyList<GitHubProjectField>>> ListFieldsAsync(string owner, int number,
        GitHubReadOptions options, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        ListFieldsCoreAsync(owner, number, options, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// A project's fields, blocking until they're read. Otherwise the same as <see cref="ListFieldsAsync"/>; see
    /// <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListFieldsAsync" path="/param"/>
    /// <inheritdoc cref="ListFieldsAsync" path="/returns"/>
    /// <inheritdoc cref="ListFieldsAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<IReadOnlyList<GitHubProjectField>> ListFields(string owner, int number,
        GitHubReadOptions options, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListFieldsCoreAsync(owner, number, options, httpClient, true, cancellationToken));

    /// <summary>
    /// One page of a user's or organization's projects, for an app that pages them itself, e.g. in a table. Otherwise
    /// the same as <see cref="ListAsync"/>.
    /// </summary>
    /// <remarks>
    /// GraphQL pages by cursor, not number: pass the previous page's <see cref="ItemPage{T}.NextCursor"/> to read the
    /// next one. Pages can't be jumped to, but <see cref="ItemPage{T}.TotalCount"/> and
    /// <see cref="ItemPage{T}.PageCount"/> say how many there are.
    /// </remarks>
    /// <param name="owner">The user's or organization's login, e.g. <c>octocat</c>.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="cursor">The previous page's <see cref="ItemPage{T}.NextCursor"/>, or null for the first page.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, or a failure as for <see cref="ListAsync"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="options"/>' token is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> isn't 1 to 100.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<ItemPage<GitHubProject>>> ListPageAsync(string owner, GitHubReadOptions options,
        int pageSize = 30, string? cursor = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListPageCoreAsync(owner, options, pageSize, cursor, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One page of a user's or organization's projects, blocking until it's read. Otherwise the same as
    /// <see cref="ListPageAsync"/>; see <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListPageAsync" path="/remarks"/>
    /// <inheritdoc cref="ListPageAsync" path="/param"/>
    /// <inheritdoc cref="ListPageAsync" path="/returns"/>
    /// <inheritdoc cref="ListPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubProject>> ListPage(string owner, GitHubReadOptions options, int pageSize = 30,
        string? cursor = null, HttpClient? httpClient = null, CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListPageCoreAsync(owner, options, pageSize, cursor, httpClient, true, cancellationToken));

    /// <summary>
    /// One page of the projects linked to a repository, for an app that pages them itself. Otherwise the same as
    /// <see cref="ListForRepositoryAsync"/>.
    /// </summary>
    /// <inheritdoc cref="ListPageAsync" path="/remarks"/>
    /// <param name="repository">The repository, as <c>owner/name</c>.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="cursor">The previous page's <see cref="ItemPage{T}.NextCursor"/>, or null for the first page.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, or a failure as for <see cref="ListForRepositoryAsync"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="repository"/> isn't <c>owner/name</c>, or <paramref name="options"/>' token is blank.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageSize"/> isn't 1 to 100.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<ItemPage<GitHubProject>>> ListForRepositoryPageAsync(string repository,
        GitHubReadOptions options, int pageSize = 30, string? cursor = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListForRepositoryPageCoreAsync(repository, options, pageSize, cursor, httpClient, false, cancellationToken)
            .AsTask();

    /// <summary>
    /// One page of the projects linked to a repository, blocking until it's read. Otherwise the same as
    /// <see cref="ListForRepositoryPageAsync"/>; see <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListForRepositoryPageAsync" path="/remarks"/>
    /// <inheritdoc cref="ListForRepositoryPageAsync" path="/param"/>
    /// <inheritdoc cref="ListForRepositoryPageAsync" path="/returns"/>
    /// <inheritdoc cref="ListForRepositoryPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubProject>> ListForRepositoryPage(string repository, GitHubReadOptions options,
        int pageSize = 30, string? cursor = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListForRepositoryPageCoreAsync(repository, options, pageSize, cursor, httpClient, true,
            cancellationToken));

    /// <summary>
    /// One page of a project's items, for an app that pages them itself, e.g. in a table. Otherwise the same as
    /// <see cref="ListItemsAsync"/>.
    /// </summary>
    /// <inheritdoc cref="ListPageAsync" path="/remarks"/>
    /// <param name="owner">The login of the user or organization that owns the project.</param>
    /// <param name="number">The project number.</param>
    /// <param name="options">The token, which is required, the API address and a cache.</param>
    /// <param name="pageSize">How many per page, 1 to 100.</param>
    /// <param name="cursor">The previous page's <see cref="ItemPage{T}.NextCursor"/>, or null for the first page.</param>
    /// <param name="httpClient">The client to send with, as for <see cref="ListAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The page, or a failure as for <see cref="ListItemsAsync"/>.</returns>
    /// <exception cref="ArgumentException"><paramref name="owner"/> or <paramref name="options"/>' token is blank.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="number"/> is less than 1, or <paramref name="pageSize"/> isn't 1 to 100.
    /// </exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
    public static Task<Result<ItemPage<GitHubProjectItem>>> ListItemsPageAsync(string owner, int number,
        GitHubReadOptions options, int pageSize = 30, string? cursor = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        ListItemsPageCoreAsync(owner, number, options, pageSize, cursor, httpClient, false, cancellationToken).AsTask();

    /// <summary>
    /// One page of a project's items, blocking until it's read. Otherwise the same as <see cref="ListItemsPageAsync"/>;
    /// see <see cref="List"/> for where the synchronous versions work.
    /// </summary>
    /// <inheritdoc cref="ListItemsPageAsync" path="/remarks"/>
    /// <inheritdoc cref="ListItemsPageAsync" path="/param"/>
    /// <inheritdoc cref="ListItemsPageAsync" path="/returns"/>
    /// <inheritdoc cref="ListItemsPageAsync" path="/exception"/>
    /// <exception cref="NotSupportedException"><paramref name="httpClient"/>'s handler doesn't support synchronous sends.</exception>
    [UnsupportedOSPlatform("browser")]
    public static Result<ItemPage<GitHubProjectItem>> ListItemsPage(string owner, int number,
        GitHubReadOptions options, int pageSize = 30, string? cursor = null, HttpClient? httpClient = null,
        CancellationToken cancellationToken = default) =>
        SharedHttp.RunSync(ListItemsPageCoreAsync(owner, number, options, pageSize, cursor, httpClient, true,
            cancellationToken));

    private static ValueTask<Result<IReadOnlyList<GitHubProject>>> ListCoreAsync(string owner,
        GitHubReadOptions options, int maxCount, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        owner = CheckOwner(owner, options);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return GitHubReader.ListGraphQLAsync(OwnerProjectsQuery, new JsonObject { ["owner"] = owner }, maxCount,
            data => data["repositoryOwner"]?["projectsV2"], GitHubProject.FromJson, options, httpClient,
            $"There's no user or organization {owner}.", sync, cancellationToken);
    }

    private static ValueTask<Result<IReadOnlyList<GitHubProject>>> ListForRepositoryCoreAsync(string repository,
        GitHubReadOptions options, int maxCount, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        CheckOwner(owner, options);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return GitHubReader.ListGraphQLAsync(RepositoryProjectsQuery,
            new JsonObject { ["owner"] = owner, ["name"] = name }, maxCount, data => data["repository"]?["projectsV2"],
            GitHubProject.FromJson, options, httpClient,
            $"The repository {owner}/{name} doesn't exist, or the token can't see it.", sync, cancellationToken);
    }

    private static async ValueTask<Result<GitHubProject>> GetCoreAsync(string owner, int number,
        GitHubReadOptions options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        owner = CheckOwner(owner, options);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        string notFound = NotFound(owner, number);
        Result<JsonNode> data = await GitHubReader.GraphQLAsync(ProjectQuery,
            new JsonObject { ["owner"] = owner, ["number"] = number }, options, httpClient, notFound, sync,
            cancellationToken).ConfigureAwait(false);
        if (!data.TryGetValue(out JsonNode node, out Error? error))
        {
            return error!;
        }

        try
        {
            return node["repositoryOwner"]?["projectV2"] is { } project
                ? GitHubProject.FromJson(project)
                : new Error(notFound, ErrorCodes.NotFound);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return GitHubReader.Unreadable(ex);
        }
    }

    private static ValueTask<Result<IReadOnlyList<GitHubProjectItem>>> ListItemsCoreAsync(string owner, int number,
        GitHubReadOptions options, int maxCount, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        owner = CheckOwner(owner, options);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxCount, 1);
        return GitHubReader.ListGraphQLAsync(ItemsQuery, new JsonObject { ["owner"] = owner, ["number"] = number },
            maxCount, data => data["repositoryOwner"]?["projectV2"]?["items"], GitHubProjectItem.FromJson, options,
            httpClient, NotFound(owner, number), sync, cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubProject>>> ListPageCoreAsync(string owner,
        GitHubReadOptions options, int pageSize, string? cursor, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        owner = CheckOwner(owner, options);
        GitHubReader.CheckPageSize(pageSize);
        return GitHubReader.PageGraphQLAsync(OwnerProjectsQuery, new JsonObject { ["owner"] = owner }, pageSize, cursor,
            data => data["repositoryOwner"]?["projectsV2"], GitHubProject.FromJson, options, httpClient,
            $"There's no user or organization {owner}.", sync, cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubProject>>> ListForRepositoryPageCoreAsync(string repository,
        GitHubReadOptions options, int pageSize, string? cursor, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        (string owner, string name) = GitHubApi.ParseRepository(repository);
        CheckOwner(owner, options);
        GitHubReader.CheckPageSize(pageSize);
        return GitHubReader.PageGraphQLAsync(RepositoryProjectsQuery,
            new JsonObject { ["owner"] = owner, ["name"] = name }, pageSize, cursor,
            data => data["repository"]?["projectsV2"], GitHubProject.FromJson, options, httpClient,
            $"The repository {owner}/{name} doesn't exist, or the token can't see it.", sync, cancellationToken);
    }

    private static ValueTask<Result<ItemPage<GitHubProjectItem>>> ListItemsPageCoreAsync(string owner, int number,
        GitHubReadOptions options, int pageSize, string? cursor, HttpClient? httpClient, bool sync,
        CancellationToken cancellationToken)
    {
        owner = CheckOwner(owner, options);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        GitHubReader.CheckPageSize(pageSize);
        return GitHubReader.PageGraphQLAsync(ItemsQuery, new JsonObject { ["owner"] = owner, ["number"] = number },
            pageSize, cursor, data => data["repositoryOwner"]?["projectV2"]?["items"], GitHubProjectItem.FromJson,
            options, httpClient, NotFound(owner, number), sync, cancellationToken);
    }

    private static async ValueTask<Result<IReadOnlyList<GitHubProjectField>>> ListFieldsCoreAsync(string owner,
        int number, GitHubReadOptions options, HttpClient? httpClient, bool sync, CancellationToken cancellationToken)
    {
        owner = CheckOwner(owner, options);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        string notFound = NotFound(owner, number);
        Result<JsonNode> data = await GitHubReader.GraphQLAsync(FieldsQuery,
            new JsonObject { ["owner"] = owner, ["number"] = number }, options, httpClient, notFound, sync,
            cancellationToken).ConfigureAwait(false);
        if (!data.TryGetValue(out JsonNode node, out Error? error))
        {
            return error!;
        }

        try
        {
            if (node["repositoryOwner"]?["projectV2"]?["fields"]?["nodes"] is not JsonArray fields)
            {
                return new Error(notFound, ErrorCodes.NotFound);
            }

            return Result<IReadOnlyList<GitHubProjectField>>.Success(
                [.. fields.OfType<JsonNode>().Select(GitHubProjectField.FromJson)]);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return GitHubReader.Unreadable(ex);
        }
    }

    /// <summary>Checks the owner and that there's a token, and returns the owner trimmed.</summary>
    private static string CheckOwner(string owner, GitHubReadOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Token))
        {
            throw new ArgumentException("GitHub's GraphQL API, which projects are read through, needs a token.",
                nameof(options));
        }

        return owner.Trim();
    }

    private static string NotFound(string owner, int number) =>
        $"{owner} has no project #{number}, or the token can't see it.";
}
