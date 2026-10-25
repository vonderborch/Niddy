using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Niddy.Core.Tests;

/// <summary>A fake GitHub for the read tests: it records each request, with its body, and answers with a function.</summary>
internal sealed class GitHubFakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Each request's body, read before the request is disposed; null for none.</summary>
    public List<string?> Bodies { get; } = [];

    public int SyncSends { get; private set; }

    /// <summary>The URLs requested, in order.</summary>
    public IEnumerable<string> Urls => this.Requests.Select(request => request.RequestUri!.AbsoluteUri);

    /// <summary>Each request body's GraphQL <c>variables</c>.</summary>
    public IEnumerable<JsonNode> Variables => this.Bodies.Select(body => JsonNode.Parse(body!)!["variables"]!);

    public static HttpResponseMessage Json(JsonNode json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(this.Respond(request, cancellationToken));

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.SyncSends++;
        return this.Respond(request, cancellationToken);
    }

    private HttpResponseMessage Respond(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        this.Requests.Add(request);
        if (request.Content is null)
        {
            this.Bodies.Add(null);
        }
        else
        {
            using StreamReader reader = new(request.Content.ReadAsStream(cancellationToken));
            this.Bodies.Add(reader.ReadToEnd());
        }

        HttpResponseMessage response = respond(request);
        response.RequestMessage ??= request;
        return response;
    }
}
