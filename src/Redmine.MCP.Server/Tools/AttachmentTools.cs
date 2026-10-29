using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Redmine.MCP.Server
{
    internal sealed class AttachmentTools
    {
        private readonly RedmineClient _redmine;

        public AttachmentTools(RedmineClient redmine)
        {
            _redmine = redmine;
        }

        [McpServerTool(Name = "download_attachment", ReadOnly = true)]
        [Description("Redmine 첨부파일 본문을 다운로드한다. 응답 JSON에 text 또는 contentBase64를 넣으며, 5MiB를 넘으면 outputPath가 필요하다. outputPath는 MCP 서버 호스트 경로이다.")]
        public async Task<string> DownloadAttachment(
            [Description("첨부 ID. issueId+filename과 둘 중 하나가 필요하다. 둘 다 있으면 attachmentId를 우선한다.")] int? attachmentId = null,
            [Description("일감 ID. attachmentId가 없을 때 filename과 함께 첨부를 찾는다.")] int? issueId = null,
            [Description("첨부 파일명. issueId와 함께 사용한다. 대소문자를 구분한다.")] string? filename = null,
            [Description("본문 다운로드 URL. 있으면 메타 content_url 대신 사용한다. Redmine API endpoint와 같은 origin만 허용한다.")] string? contentUrl = null,
            [Description("MCP 서버 호스트에 저장할 경로. 5MiB 초과 시 필수이다.")] string? outputPath = null,
            CancellationToken cancellationToken = default)
        {
            int id = await ResolveAttachmentIdAsync(attachmentId, issueId, filename, cancellationToken).ConfigureAwait(false);

            string metaJson = await _redmine.GetAsync(
                $"attachments/{id.ToString(CultureInfo.InvariantCulture)}.json",
                cancellationToken).ConfigureAwait(false);
            JsonObject attachment = ParseAttachmentObject(metaJson);
            string resolvedFilename = attachment["filename"]?.GetValue<string>()
                ?? throw new InvalidOperationException("Attachment metadata is missing filename.");
            string? contentType = attachment["content_type"]?.GetValue<string>();
            string downloadUrl = string.IsNullOrWhiteSpace(contentUrl)
                ? attachment["content_url"]?.GetValue<string>()
                    ?? throw new InvalidOperationException("Attachment metadata is missing content_url.")
                : contentUrl.Trim();

            byte[] bytes = await _redmine.GetBytesAsync(downloadUrl, cancellationToken).ConfigureAwait(false);
            bool exceedsInline = AttachmentDownload.ExceedsInlineLimit(bytes.Length);
            if (exceedsInline && string.IsNullOrWhiteSpace(outputPath))
            {
                throw new InvalidOperationException(
                    $"Attachment is larger than {AttachmentDownload.MaxInlineBytes} bytes. Provide outputPath to save on the MCP server host.");
            }

            string? savedPath = null;
            if (!string.IsNullOrWhiteSpace(outputPath))
            {
                savedPath = Path.GetFullPath(outputPath);
                string? directory = Path.GetDirectoryName(savedPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(savedPath, bytes, cancellationToken).ConfigureAwait(false);
            }

            return AttachmentDownload.BuildResponseJson(
                id,
                resolvedFilename,
                contentType,
                bytes,
                includeBody: !exceedsInline,
                savedPath);
        }

        private async Task<int> ResolveAttachmentIdAsync(
            int? attachmentId,
            int? issueId,
            string? filename,
            CancellationToken cancellationToken)
        {
            if (attachmentId is not null)
            {
                if (attachmentId.Value < 1)
                {
                    throw new ArgumentException("attachmentId must be positive.", nameof(attachmentId));
                }

                return attachmentId.Value;
            }

            if (issueId is null || string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentException("Provide attachmentId, or both issueId and filename.");
            }

            if (issueId.Value < 1)
            {
                throw new ArgumentException("issueId must be positive.", nameof(issueId));
            }

            string issueJson = await _redmine.GetAsync(
                $"issues/{issueId.Value.ToString(CultureInfo.InvariantCulture)}.json",
                cancellationToken,
                ("include", "attachments")).ConfigureAwait(false);
            JsonNode root = JsonNode.Parse(issueJson)
                ?? throw new InvalidOperationException("Issue response was empty.");
            JsonArray? attachments = root["issue"]?["attachments"]?.AsArray();
            if (attachments is null || attachments.Count == 0)
            {
                throw new InvalidOperationException($"Issue {issueId.Value} has no attachments matching '{filename}'.");
            }

            List<int> matches = [];
            foreach (JsonNode? node in attachments)
            {
                if (node is null)
                {
                    continue;
                }

                string? name = node["filename"]?.GetValue<string>();
                if (string.Equals(name, filename, StringComparison.Ordinal)
                    && node["id"] is JsonNode idNode)
                {
                    matches.Add(idNode.GetValue<int>());
                }
            }

            if (matches.Count == 0)
            {
                throw new InvalidOperationException($"Issue {issueId.Value} has no attachments matching '{filename}'.");
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Issue {issueId.Value} has multiple attachments named '{filename}'. Use attachmentId.");
            }

            return matches[0];
        }

        private static JsonObject ParseAttachmentObject(string metaJson)
        {
            JsonNode root = JsonNode.Parse(metaJson)
                ?? throw new InvalidOperationException("Attachment response was empty.");
            JsonObject? attachment = root["attachment"]?.AsObject();
            if (attachment is null)
            {
                throw new InvalidOperationException("Attachment response did not contain an attachment object.");
            }

            return attachment;
        }
    }
}
