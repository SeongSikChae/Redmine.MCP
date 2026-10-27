using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server
{
    internal sealed class WikiTools
    {
        private readonly RedmineClient _redmine;

        public WikiTools(RedmineClient redmine)
        {
            _redmine = redmine;
        }

        [McpServerTool(Name = "list_wiki_pages", ReadOnly = true)]
        [Description("프로젝트의 Redmine 위키 페이지 목록을 조회한다.")]
        public Task<string> ListWikiPages(
            [Description("프로젝트 ID 또는 식별자")] string projectId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
            return _redmine.GetAsync($"projects/{Uri.EscapeDataString(projectId.Trim())}/wiki/index.json", cancellationToken);
        }

        [McpServerTool(Name = "get_wiki_page", ReadOnly = true)]
        [Description("Redmine 위키 페이지 하나를 조회한다.")]
        public Task<string> GetWikiPage(
            [Description("프로젝트 ID 또는 식별자")] string projectId,
            [Description("위키 페이지 제목")] string title,
            CancellationToken cancellationToken = default)
        {
            return _redmine.GetAsync(PagePath(projectId, title), cancellationToken);
        }

        [McpServerTool(Name = "put_wiki_page", ReadOnly = false, Destructive = true)]
        [Description("Redmine 위키 페이지를 생성하거나 수정한다. 기존 페이지를 고치면 성공 본문이 비어 있다. 새 페이지를 만들 때만 페이지 JSON이 온다. " + RedmineToolDescriptions.Mutating)]
        public Task<string> PutWikiPage(
            [Description("프로젝트 ID 또는 식별자")] string projectId,
            [Description("위키 페이지 제목")] string title,
            [Description("본문")] string text,
            [Description("변경 설명")] string? comments = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            JsonObject wikiPage = new();
            RedmineJson.Set(wikiPage, "text", text);
            RedmineJson.Set(wikiPage, "comments", comments);
            return _redmine.PutAsync(PagePath(projectId, title), "wiki_page", wikiPage, cancellationToken);
        }

        private static string PagePath(string projectId, string title)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);
            return $"projects/{Uri.EscapeDataString(projectId.Trim())}/wiki/{Uri.EscapeDataString(title.Trim())}.json";
        }
    }
}
