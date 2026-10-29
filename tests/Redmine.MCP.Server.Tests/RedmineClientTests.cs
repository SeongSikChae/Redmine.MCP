using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server.Tests;

[TestClass]
public sealed class RedmineClientTests
{
    [TestMethod]
    public void CreateBaseAddress_AppendsTrailingSlashAndKeepsSubpath()
    {
        Assert.AreEqual("https://redmine.example/redmine/", RedmineHttpClientSetup.CreateBaseAddress("https://redmine.example/redmine").AbsoluteUri);
        Assert.AreEqual("https://redmine.example/redmine/", RedmineHttpClientSetup.CreateBaseAddress(" https://redmine.example/redmine/ ").AbsoluteUri);
    }

    [TestMethod]
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

        Assert.AreEqual("{}", body);
        Assert.AreEqual(HttpMethod.Get, handler.Method);
        Assert.AreEqual("secret-key", handler.ApiKey);
        Assert.AreEqual(
            "https://redmine.example/redmine/issues.json?project_id=my%20project&limit=100&offset=0",
            handler.RequestUri!.AbsoluteUri);
    }

    [TestMethod]
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

        Assert.AreEqual(HttpMethod.Post, handler.Method);
        Assert.AreEqual("https://redmine.example/issues.json", handler.RequestUri!.AbsoluteUri);
        Assert.IsNotNull(handler.Body);
        JsonNode payload = JsonNode.Parse(handler.Body)!;
        Assert.AreEqual(4, payload["issue"]!["project_id"]!.GetValue<int>());
        Assert.AreEqual("Fix login", payload["issue"]!["subject"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task SendAsync_IncludesStatusAndBodyWhenRequestFails()
    {
        using RecordingHandler handler = new()
        {
            StatusCode = HttpStatusCode.UnprocessableEntity,
            ResponseBody = """{"errors":["Subject can't be blank"]}"""
        };
        RedmineClient client = CreateClient(handler, "https://redmine.example/", "secret-key");

        RedmineApiException exception = await Assert.ThrowsExactlyAsync<RedmineApiException>(() => client.GetAsync("issues.json", CancellationToken.None));

        Assert.AreEqual(422, exception.StatusCode);
        StringAssert.Contains(exception.ResponseBody, "Subject can't be blank", StringComparison.Ordinal);
        StringAssert.Contains(exception.Message, "422", StringComparison.Ordinal);
        Assert.IsFalse(exception.Message.Contains("secret-key", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetBytesAsync_RelativePath_ReturnsBytesAndSendsApiKeyWithWildcardAccept()
    {
        byte[] payload = Encoding.UTF8.GetBytes("hello attachment");
        using RecordingHandler handler = new()
        {
            ResponseBytes = payload
        };
        RedmineClient client = CreateClient(handler, "https://redmine.example/redmine", "secret-key");

        byte[] body = await client.GetBytesAsync("attachments/download/9/file.txt", CancellationToken.None);

        CollectionAssert.AreEqual(payload, body);
        Assert.AreEqual(HttpMethod.Get, handler.Method);
        Assert.AreEqual("secret-key", handler.ApiKey);
        Assert.AreEqual(
            "https://redmine.example/redmine/attachments/download/9/file.txt",
            handler.RequestUri!.AbsoluteUri);
        StringAssert.Contains(handler.Accept!, "*/*", StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task GetBytesAsync_SameOriginAbsoluteUrl_Succeeds()
    {
        byte[] payload = [1, 2, 3, 4];
        using RecordingHandler handler = new()
        {
            ResponseBytes = payload
        };
        RedmineClient client = CreateClient(handler, "https://redmine.example/redmine/", "secret-key");

        byte[] body = await client.GetBytesAsync(
            "https://redmine.example/redmine/attachments/download/9/file.bin",
            CancellationToken.None);

        CollectionAssert.AreEqual(payload, body);
        Assert.AreEqual(
            "https://redmine.example/redmine/attachments/download/9/file.bin",
            handler.RequestUri!.AbsoluteUri);
    }

    [TestMethod]
    public async Task GetBytesAsync_DifferentOrigin_Throws()
    {
        using RecordingHandler handler = new();
        RedmineClient client = CreateClient(handler, "https://redmine.example/", "secret-key");

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            client.GetBytesAsync("https://evil.example/attachments/download/1/x", CancellationToken.None));

        Assert.IsNull(handler.RequestUri);
    }

    [TestMethod]
    public async Task GetBytesAsync_HttpError_ThrowsRedmineApiException()
    {
        using RecordingHandler handler = new()
        {
            StatusCode = HttpStatusCode.NotFound,
            ResponseBytes = Encoding.UTF8.GetBytes("missing")
        };
        RedmineClient client = CreateClient(handler, "https://redmine.example/", "secret-key");

        RedmineApiException exception = await Assert.ThrowsExactlyAsync<RedmineApiException>(() =>
            client.GetBytesAsync("attachments/download/1/x", CancellationToken.None));

        Assert.AreEqual(404, exception.StatusCode);
        StringAssert.Contains(exception.ResponseBody, "missing", StringComparison.Ordinal);
        Assert.IsFalse(exception.Message.Contains("secret-key", StringComparison.Ordinal));
    }

    private static RedmineClient CreateClient(RecordingHandler handler, string endpoint, string apiKey)
    {
        HttpClient httpClient = new(handler, disposeHandler: false);
        RedmineHttpClientSetup.Configure(httpClient, new Configuration
        {
            ContentRootPath = ".",
            Port = 6049,
            Redmine = new RedmineConfiguration
            {
                RedmineApiEndpoint = endpoint,
                RedmineApiKey = apiKey
            }
        });
        return new RedmineClient(httpClient);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpMethod? Method { get; private set; }

        public Uri? RequestUri { get; private set; }

        public string? ApiKey { get; private set; }

        public string? Accept { get; private set; }

        public string? Body { get; private set; }

        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;

        public string ResponseBody { get; init; } = "{}";

        public byte[]? ResponseBytes { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Method = request.Method;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.TryGetValues(RedmineHttpClientSetup.ApiKeyHeaderName, out IEnumerable<string>? values)
                ? values.Single()
                : null;
            Accept = string.Join(", ", request.Headers.Accept.Select(static value => value.ToString()));
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            HttpContent content = ResponseBytes is null
                ? new StringContent(ResponseBody, Encoding.UTF8, "application/json")
                : new ByteArrayContent(ResponseBytes);
            return new HttpResponseMessage(StatusCode)
            {
                Content = content
            };
        }
    }
}
