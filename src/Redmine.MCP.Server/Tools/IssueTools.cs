using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server
{
    internal sealed class IssueTools
    {
        private readonly RedmineClient _redmine;

        public IssueTools(RedmineClient redmine)
        {
            _redmine = redmine;
        }

        [McpServerTool(Name = "list_issues", ReadOnly = true)]
        [Description("Redmine 이슈 목록을 조회한다.")]
        public Task<string> ListIssues(
            [Description("프로젝트 ID 또는 식별자")] string? projectId = null,
            [Description("상태 ID, open, closed, *. 비우면 열린 일감만 반환한다. 전체는 *이다.")] string? statusId = null,
            [Description("담당자 ID 또는 me")] string? assignedToId = null,
            [Description("트래커 ID")] int? trackerId = null,
            [Description("페이지 크기. 기본 25, 최대 100")] int? limit = null,
            [Description("건너뛸 개수. 기본 0")] int? offset = null,
            CancellationToken cancellationToken = default)
        {
            return _redmine.GetPagedAsync(
                "issues.json",
                limit,
                offset,
                cancellationToken,
                ("project_id", projectId),
                ("status_id", statusId),
                ("assigned_to_id", assignedToId),
                ("tracker_id", trackerId?.ToString(CultureInfo.InvariantCulture)));
        }

        [McpServerTool(Name = "get_issue", ReadOnly = true)]
        [Description("Redmine 이슈 하나를 조회한다.")]
        public Task<string> GetIssue(
            [Description("이슈 ID")] int id,
            [Description("함께 받을 연관 데이터. 기본값은 journals이다. children, attachments, relations, changesets를 받을 수 있다. watchers는 2.2 응답에 없다.")] string? include = "journals",
            CancellationToken cancellationToken = default)
        {
            return _redmine.GetAsync(
                $"issues/{id.ToString(CultureInfo.InvariantCulture)}.json",
                cancellationToken,
                ("include", string.IsNullOrWhiteSpace(include) ? "journals" : include));
        }

        [McpServerTool(Name = "create_issue", ReadOnly = false, Destructive = false)]
        [Description("Redmine 이슈를 생성한다. " + RedmineToolDescriptions.Mutating)]
        public Task<string> CreateIssue(
            [Description("프로젝트 ID 또는 식별자")] string projectId,
            [Description("제목")] string subject,
            [Description("설명")] string? description = null,
            [Description("트래커 ID")] int? trackerId = null,
            [Description("상태 ID")] int? statusId = null,
            [Description("우선순위 ID")] int? priorityId = null,
            [Description("담당자 ID")] int? assignedToId = null,
            [Description("부모 이슈 ID")] int? parentIssueId = null,
            [Description("시작일. yyyy-MM-dd")] string? startDate = null,
            [Description("완료 기한. yyyy-MM-dd")] string? dueDate = null,
            [Description("예상 시간")] double? estimatedHours = null,
            [Description("진척도. 0부터 100")] int? doneRatio = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(projectId);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);
            JsonObject issue = BuildIssue(
                projectId,
                subject,
                description,
                trackerId,
                statusId,
                priorityId,
                assignedToId,
                parentIssueId,
                startDate,
                dueDate,
                estimatedHours,
                doneRatio,
                notes: null);
            return _redmine.PostAsync("issues.json", "issue", issue, cancellationToken);
        }

        [McpServerTool(Name = "update_issue", ReadOnly = false, Destructive = true)]
        [Description("Redmine 이슈를 수정하거나 코멘트를 추가한다. 성공 시 본문이 비어 있다. " + RedmineToolDescriptions.Mutating)]
        public Task<string> UpdateIssue(
            [Description("이슈 ID")] int id,
            [Description("제목")] string? subject = null,
            [Description("설명")] string? description = null,
            [Description("트래커 ID")] int? trackerId = null,
            [Description("상태 ID")] int? statusId = null,
            [Description("우선순위 ID")] int? priorityId = null,
            [Description("담당자 ID")] int? assignedToId = null,
            [Description("부모 이슈 ID")] int? parentIssueId = null,
            [Description("시작일. yyyy-MM-dd")] string? startDate = null,
            [Description("완료 기한. yyyy-MM-dd")] string? dueDate = null,
            [Description("예상 시간")] double? estimatedHours = null,
            [Description("진척도. 0부터 100")] int? doneRatio = null,
            [Description("코멘트")] string? notes = null,
            CancellationToken cancellationToken = default)
        {
            JsonObject issue = BuildIssue(
                projectId: null,
                subject,
                description,
                trackerId,
                statusId,
                priorityId,
                assignedToId,
                parentIssueId,
                startDate,
                dueDate,
                estimatedHours,
                doneRatio,
                notes);
            if (issue.Count == 0)
            {
                throw new ArgumentException("수정할 필드를 하나 이상 지정해야 합니다.");
            }

            return _redmine.PutAsync($"issues/{id.ToString(CultureInfo.InvariantCulture)}.json", "issue", issue, cancellationToken);
        }

        private static JsonObject BuildIssue(
            string? projectId,
            string? subject,
            string? description,
            int? trackerId,
            int? statusId,
            int? priorityId,
            int? assignedToId,
            int? parentIssueId,
            string? startDate,
            string? dueDate,
            double? estimatedHours,
            int? doneRatio,
            string? notes)
        {
            JsonObject issue = new();
            RedmineJson.SetId(issue, "project_id", projectId);
            RedmineJson.Set(issue, "subject", subject);
            RedmineJson.Set(issue, "description", description);
            RedmineJson.Set(issue, "tracker_id", trackerId);
            RedmineJson.Set(issue, "status_id", statusId);
            RedmineJson.Set(issue, "priority_id", priorityId);
            RedmineJson.Set(issue, "assigned_to_id", assignedToId);
            RedmineJson.Set(issue, "parent_issue_id", parentIssueId);
            RedmineJson.Set(issue, "start_date", startDate);
            RedmineJson.Set(issue, "due_date", dueDate);
            RedmineJson.Set(issue, "estimated_hours", estimatedHours);
            RedmineJson.Set(issue, "done_ratio", doneRatio);
            RedmineJson.Set(issue, "notes", notes);
            return issue;
        }
    }
}
