using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server.Tests;

[TestClass]
public sealed class AttachmentToolsTests
{
    [TestMethod]
    public async Task DownloadAttachment_ByAttachmentId_ReturnsText()
    {
        using ScriptedHandler handler = new();
        handler.EnqueueJson(
            "https://redmine.example/attachments/9.json",
            """{"attachment":{"id":9,"filename":"note.md","filesize":5,"content_type":"text/plain","content_url":"https://redmine.example/attachments/download/9/note.md"}}""");
        handler.EnqueueBytes(
            "https://redmine.example/attachments/download/9/note.md",
            Encoding.UTF8.GetBytes("hello"));

        AttachmentTools tools = CreateTools(handler);
        string json = await tools.DownloadAttachment(attachmentId: 9, cancellationToken: CancellationToken.None);

        JsonNode node = JsonNode.Parse(json)!;
        Assert.AreEqual(9, node["id"]!.GetValue<int>());
        Assert.AreEqual("note.md", node["filename"]!.GetValue<string>());
        Assert.AreEqual("hello", node["text"]!.GetValue<string>());
        Assert.AreEqual(2, handler.RequestCount);
    }

    [TestMethod]
    public async Task DownloadAttachment_ByIssueIdAndFilename_ResolvesId()
    {
        using ScriptedHandler handler = new();
        handler.EnqueueJson(
            "https://redmine.example/issues/40.json?include=attachments",
            """{"issue":{"id":40,"attachments":[{"id":9,"filename":"note.md"}]}}""");
        handler.EnqueueJson(
            "https://redmine.example/attachments/9.json",
            """{"attachment":{"id":9,"filename":"note.md","filesize":2,"content_type":"text/plain","content_url":"https://redmine.example/attachments/download/9/note.md"}}""");
        handler.EnqueueBytes(
            "https://redmine.example/attachments/download/9/note.md",
            Encoding.UTF8.GetBytes("ok"));

        AttachmentTools tools = CreateTools(handler);
        string json = await tools.DownloadAttachment(
            issueId: 40,
            filename: "note.md",
            cancellationToken: CancellationToken.None);

        Assert.AreEqual("ok", JsonNode.Parse(json)!["text"]!.GetValue<string>());
    }

    [TestMethod]
    public async Task DownloadAttachment_DuplicateFilename_Throws()
    {
        using ScriptedHandler handler = new();
        handler.EnqueueJson(
            "https://redmine.example/issues/40.json?include=attachments",
            """{"issue":{"id":40,"attachments":[{"id":9,"filename":"note.md"},{"id":10,"filename":"note.md"}]}}""");

        AttachmentTools tools = CreateTools(handler);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            tools.DownloadAttachment(issueId: 40, filename: "note.md", cancellationToken: CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAttachment_ContentUrlDifferentOrigin_Throws()
    {
        using ScriptedHandler handler = new();
        handler.EnqueueJson(
            "https://redmine.example/attachments/9.json",
            """{"attachment":{"id":9,"filename":"note.md","filesize":1,"content_type":"text/plain","content_url":"https://redmine.example/attachments/download/9/note.md"}}""");

        AttachmentTools tools = CreateTools(handler);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            tools.DownloadAttachment(
                attachmentId: 9,
                contentUrl: "https://evil.example/attachments/download/9/note.md",
                cancellationToken: CancellationToken.None));
    }

    [TestMethod]
    public async Task DownloadAttachment_SmallFile_SavesAndReturnsText()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "redmine-mcp-attach-" + Guid.NewGuid().ToString("N"));
        string outputPath = Path.Combine(tempDir, "saved.md");
        try
        {
            using ScriptedHandler handler = new();
            handler.EnqueueJson(
                "https://redmine.example/attachments/9.json",
                """{"attachment":{"id":9,"filename":"note.md","filesize":4,"content_type":"text/plain","content_url":"https://redmine.example/attachments/download/9/note.md"}}""");
            handler.EnqueueBytes(
                "https://redmine.example/attachments/download/9/note.md",
                Encoding.UTF8.GetBytes("save"));

            AttachmentTools tools = CreateTools(handler);
            string json = await tools.DownloadAttachment(
                attachmentId: 9,
                outputPath: outputPath,
                cancellationToken: CancellationToken.None);

            JsonNode node = JsonNode.Parse(json)!;
            Assert.AreEqual("save", node["text"]!.GetValue<string>());
            Assert.AreEqual(Path.GetFullPath(outputPath), node["savedPath"]!.GetValue<string>());
            Assert.AreEqual("save", await File.ReadAllTextAsync(outputPath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static AttachmentTools CreateTools(ScriptedHandler handler)
    {
        HttpClient httpClient = new(handler, disposeHandler: false);
        RedmineHttpClientSetup.Configure(httpClient, new Configuration
        {
            ContentRootPath = ".",
            Port = 6049,
            RedmineApiEndpoint = "https://redmine.example/",
            RedmineApiKey = "secret-key"
        });
        return new AttachmentTools(new RedmineClient(httpClient));
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<(string Uri, HttpStatusCode Status, byte[] Body, string? MediaType)> _responses = new();

        public int RequestCount { get; private set; }

        public void EnqueueJson(string uri, string json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _responses.Enqueue((uri, status, Encoding.UTF8.GetBytes(json), "application/json"));
        }

        public void EnqueueBytes(string uri, byte[] body, HttpStatusCode status = HttpStatusCode.OK)
        {
            _responses.Enqueue((uri, status, body, "application/octet-stream"));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Assert.IsTrue(_responses.Count > 0, "Unexpected extra HTTP request: " + request.RequestUri);
            (string expectedUri, HttpStatusCode status, byte[] body, string? mediaType) = _responses.Dequeue();
            Assert.AreEqual(expectedUri, request.RequestUri!.AbsoluteUri);

            ByteArrayContent content = new(body);
            if (mediaType is not null)
            {
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
            }

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = content
            });
        }
    }
}
