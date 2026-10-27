using ModelContextProtocol.Server;
using System.ComponentModel;

namespace Redmine.MCP.Server
{
    internal sealed class ProjectTools
    {
        private readonly RedmineClient _redmine;

        public ProjectTools(RedmineClient redmine)
        {
            _redmine = redmine;
        }

        [McpServerTool(Name = "list_projects", ReadOnly = true)]
        [Description("Redmine 프로젝트 목록을 조회한다.")]
        public Task<string> ListProjects(
            [Description("페이지 크기. 기본 25, 최대 100")] int? limit = null,
            [Description("건너뛸 개수. 기본 0")] int? offset = null,
            CancellationToken cancellationToken = default)
        {
            return _redmine.GetPagedAsync("projects.json", limit, offset, cancellationToken);
        }

        [McpServerTool(Name = "get_project", ReadOnly = true)]
        [Description("Redmine 프로젝트 하나를 조회한다.")]
        public Task<string> GetProject(
            [Description("프로젝트 ID 또는 식별자")] string id,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            return _redmine.GetAsync($"projects/{Uri.EscapeDataString(id.Trim())}.json", cancellationToken);
        }
    }
}
