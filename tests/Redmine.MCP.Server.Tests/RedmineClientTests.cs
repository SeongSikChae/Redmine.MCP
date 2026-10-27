using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server.Tests;

public sealed class RedmineClientTests
{
    [Fact]
    public void CreateBaseAddress_AppendsTrailingSlashAndKeepsSubpath()
    {
        Assert.Equal("https://redmine.example/redmine/", RedmineHttpClientSetup.CreateBaseAddress("https://redmine.example/redmine").AbsoluteUri);
        Assert.Equal("https://redmine.example/redmine/", RedmineHttpClientSetup.CreateBaseAddress(" https://redmine.example/redmine/ ").AbsoluteUri);
    }

    [Fact]
    public async Task GetPagedAsync_BuildsUrlAndSendsApiKey()
    {
        using RecordingHandler handler = new();
        RedmineClient client = CreateClient(handler, "https://redmine.example/redmine", "secret-key");

        string body = await client.GetPagedAsync(
            "issues.json",
            limit: 500,
            offset: -3,
            CancellationToken.None,
            ("project_id", "my project"));

        Assert.Equal("{}", body);
        Assert.Equal(HttpMethod.Get, handler.Method);
        Assert.Equal("secret-key", handler.ApiKey);
        Assert.Equal(
            "https://redmine.example/redmine/issues.json?project_id=my%20project&limit=100&offset=0",
            handler.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task PostAsync_WrapsPayloadInNamedObject()
    {
        using RecordingHandler handler = new();
        RedmineClient client = CreateClient(handler, "https://redmine.example/", "secret-key");
        JsonObject issue = new()
        {
            ["project_id"] = 4,
            ["subject"] = "Fix login"
        };

        await client.PostAsync("issues.json", "issue", issue, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.Method);
        Assert.Equal("https://redmine.example/issues.json", handler.RequestUri!.AbsoluteUri);
        Assert.NotNull(handler.Body);
        JsonNode payload = JsonNode.Parse(handler.Body)!;
        Assert.Equal(4, payload["issue"]!["project_id"]!.GetValue<int>());
        Assert.Equal("Fix login", payload["issue"]!["subject"]!.GetValue<string>());
    }

    [Fact]
    public async Task SendAsync_IncludesStatusAndBodyWhenRequestFails()
    {
        using RecordingHandler handler = new()
        {
            StatusCode = HttpStatusCode.UnprocessableEntity,
            ResponseBody = """{"errors":["Subject can't be blank"]}"""
        };
        RedmineClient client = CreateClient(handler, "https://redmine.example/", "secret-key");

        RedmineApiException exception = await Assert.ThrowsAsync<RedmineApiException>(() => client.GetAsync("issues.json", CancellationToken.None));

        Assert.Equal(422, exception.StatusCode);
        Assert.Contains("Subject can't be blank", exception.ResponseBody, StringComparison.Ordinal);
        Assert.Contains("422", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-key", exception.Message, StringComparison.Ordinal);
    }

    private static RedmineClient CreateClient(RecordingHandler handler, string endpoint, string apiKey)
    {
        HttpClient httpClient = new(handler, disposeHandler: false);
        RedmineHttpClientSetup.Configure(httpClient, new Configuration
        {
            ContentRootPath = ".",
            Port = 6049,
            RedmineApiEndpoint = endpoint,
            RedmineApiKey = apiKey
        });
        return new RedmineClient(httpClient);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        public string? Body { get; private set; }

        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        public string ResponseBody { get; init; } = "{}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues(RedmineHttpClientSetup.ApiKeyHeaderName, out IEnumerable<string>? values)
                ? values.Single()
                : null;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
