using System.ComponentModel;
using System.Net;
using System.Reflection;
using System.Text;

namespace Redmine.MCP.Server.Tests;

public sealed class Redmine22ContractTests
{
    [Fact]
    public async Task ListTimeEntries_OmitsUserIdAndSendsSupportedFilters()
    {
        using RecordingHandler handler = new();
        using HttpClient httpClient = new(handler, disposeHandler: false);
        RedmineHttpClientSetup.Configure(httpClient, new Configuration
        {
            ContentRootPath = ".",
            Port = 6049,
            RedmineApiEndpoint = "https://redmine.example/",
            RedmineApiKey = "secret-key"
        });
        TimeEntryTools tools = new(new RedmineClient(httpClient));

        string body = await tools.ListTimeEntries(
            projectId: "demo",
            issueId: 12,
            from: "2026-01-01",
            to: "2026-01-31",
            cancellationToken: CancellationToken.None);

        Assert.Equal("{}", body);
        Assert.NotNull(handler.RequestUri);
        string query = handler.RequestUri.Query;
        Assert.Contains("project_id=demo", query, StringComparison.Ordinal);
        Assert.Contains("issue_id=12", query, StringComparison.Ordinal);
        Assert.Contains("from=2026-01-01", query, StringComparison.Ordinal);
        Assert.Contains("to=2026-01-31", query, StringComparison.Ordinal);
        Assert.DoesNotContain("user_id", query, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(TimeEntryTools).GetMethod(nameof(TimeEntryTools.ListTimeEntries))!.GetParameters(),
            parameter => parameter.Name == "userId");
    }

    [Fact]
    public void ToolDescriptions_StateRedmine22Behavior()
    {
        Assert.Contains("사용자로는 거르지 못한다", Description(typeof(TimeEntryTools), nameof(TimeEntryTools.ListTimeEntries)), StringComparison.Ordinal);
        Assert.Contains("issueId가 있으면 projectId는 적용되지 않는다", Description(typeof(TimeEntryTools), nameof(TimeEntryTools.ListTimeEntries)), StringComparison.Ordinal);
        Assert.Contains("성공 시 본문이 비어 있다", Description(typeof(TimeEntryTools), nameof(TimeEntryTools.UpdateTimeEntry)), StringComparison.Ordinal);

        Assert.Contains("비우면 열린 일감만 반환한다", ParameterDescription(typeof(IssueTools), nameof(IssueTools.ListIssues), "statusId"), StringComparison.Ordinal);
        Assert.Contains("전체는 *이다", ParameterDescription(typeof(IssueTools), nameof(IssueTools.ListIssues), "statusId"), StringComparison.Ordinal);
        string include = ParameterDescription(typeof(IssueTools), nameof(IssueTools.GetIssue), "include");
        Assert.Contains("journals", include, StringComparison.Ordinal);
        Assert.Contains("children", include, StringComparison.Ordinal);
        Assert.Contains("attachments", include, StringComparison.Ordinal);
        Assert.Contains("relations", include, StringComparison.Ordinal);
        Assert.Contains("changesets", include, StringComparison.Ordinal);
        Assert.Contains("watchers는 2.2 응답에 없다", include, StringComparison.Ordinal);
        Assert.Contains("성공 시 본문이 비어 있다", Description(typeof(IssueTools), nameof(IssueTools.UpdateIssue)), StringComparison.Ordinal);

        Assert.Contains("관리자 권한이 필요하다", Description(typeof(UserTools), nameof(UserTools.ListUsers)), StringComparison.Ordinal);
        Assert.Contains("활성 사용자만 반환한다", Description(typeof(UserTools), nameof(UserTools.ListUsers)), StringComparison.Ordinal);
        string getUser = Description(typeof(UserTools), nameof(UserTools.GetUser));
        Assert.Contains("활성 사용자가 아니거나", getUser, StringComparison.Ordinal);
        Assert.Contains("보이는 멤버십과 활동이 없으면 404", getUser, StringComparison.Ordinal);
        Assert.Contains("일반 API 키", Description(typeof(UserTools), nameof(UserTools.GetCurrentUser)), StringComparison.Ordinal);

        string wiki = Description(typeof(WikiTools), nameof(WikiTools.PutWikiPage));
        Assert.Contains("성공 본문이 비어 있다", wiki, StringComparison.Ordinal);
        Assert.Contains("페이지 JSON이 온다", wiki, StringComparison.Ordinal);

        string activities = Description(typeof(MetadataTools), nameof(MetadataTools.ListTimeEntryActivities));
        Assert.Contains("시스템 공통 활동만 반환한다", activities, StringComparison.Ordinal);
        Assert.Contains("프로젝트에서 재정의한 활동 ID는 목록에 없다", activities, StringComparison.Ordinal);
    }

    private static string Description(Type type, string methodName)
    {
        return type.GetMethod(methodName)!.GetCustomAttribute<DescriptionAttribute>()!.Description;
    }

    private static string ParameterDescription(Type type, string methodName, string parameterName)
    {
        ParameterInfo parameter = type.GetMethod(methodName)!.GetParameters().Single(item => item.Name == parameterName);
        return parameter.GetCustomAttribute<DescriptionAttribute>()!.Description;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });
        }
    }
}
