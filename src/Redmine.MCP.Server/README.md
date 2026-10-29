# Redmine MCP Server

Redmine REST API를 HTTP MCP 도구로 노출하는 ASP.NET Core 서버입니다. 전송은 stateless HTTP이고, API 주소와 API 키는 YAML 설정에서 읽습니다.

## 설정

샘플: `Configuration.sample.yml`

```yaml
ContentRootPath: .
Port: 6049
Secure:
  UseSsl: false
AzureKms:
  ClientCertificateThumbprint: replace-with-client-certificate-thumbprint
  AzureTenantId: 00000000-0000-0000-0000-000000000000
  AzureClientId: 00000000-0000-0000-0000-000000000000
  AzureKeyVaultUrl: https://YOUR-VAULT.vault.azure.net/
  SecretsDekFilePath: secrets.dek
Redmine:
  RedmineApiEndpoint: https://redmine.example.com
  RedmineApiKey: replace-with-redmine-api-key
```

- `Redmine.RedmineApiEndpoint` 끝의 슬래시는 없어도 됩니다. 하위 경로에 설치된 Redmine이면 그 경로까지 포함합니다.
- `Secure.UseSsl: true`이면 `ServerCertificate`와 `ServerCertificatePassword`가 필요합니다.
- `ServerCertificatePassword`와 `RedmineApiKey`는 기동 시 Azure Key Vault KEK + 로컬 DEK로 암호화·복호화됩니다. 평문으로 넣으면 첫 기동에서 설정 파일이 암호문으로 갱신됩니다.
- 기존 flat YAML은 수동으로 `Secure` / `AzureKms` / `Redmine` 중첩으로 맞춰야 합니다.
- 기동에는 `--config`와 `--log`가 필요합니다.

## Redmine 2.2 제약

접속 대상은 Redmine 2.2.x입니다.

- 전문 검색 REST와 기존 댓글 수정 REST는 없습니다.
- 일감 제목은 일반 텍스트입니다. 일감 설명, 새 댓글, 위키 본문은 Textile입니다.
- `watchers`는 `get_issue` 응답에 없습니다.

## 공통

- 목록 도구의 `limit` 기본값은 25, 최대값은 100입니다. `offset` 기본값은 0입니다.
- 변경 도구(`create_issue`, `update_issue`, `create_time_entry`, `update_time_entry`, `put_wiki_page`)는 호출 전에 변경 내용을 사용자에게 보여주고 동의를 받은 뒤에만 호출하라는 지시가 있습니다. 서버는 도구가 호출되면 Redmine에 바로 반영합니다.
- 새 댓글은 `update_issue`의 `notes`입니다.
- Textile 본문을 쓰기 전에 `get_text_guide`를 호출합니다. `target`은 `issue`, `comment`, `wiki`입니다. Redmine에는 요청하지 않습니다.

## 도구

### 이슈

- `list_issues`, `get_issue`, `create_issue`, `update_issue`
- `list_issues`의 `statusId`를 비우면 열린 일감만 나옵니다. 전체는 `*`입니다.
- `get_issue`의 `include` 기본값은 `journals`입니다. `children`, `attachments`, `relations`, `changesets`를 받을 수 있습니다.
- `update_issue`는 성공 시 본문이 비어 있습니다.

### 첨부

- `download_attachment`
- `attachmentId` 또는 `issueId`+`filename`으로 첨부를 지정합니다. 선택으로 `contentUrl`, `outputPath`를 받습니다.
- 응답 JSON에 `text` 또는 `contentBase64`를 넣습니다. **5MiB**를 넘으면 인라인 본문 대신 `outputPath`가 필요합니다.
- `outputPath`는 **MCP 서버 호스트** 경로입니다.

### 프로젝트

- `list_projects`, `get_project`

### 사용자

- `get_current_user`, `list_users`, `get_user`
- `list_users`는 관리자 권한으로 활성 사용자만 반환합니다.
- `get_user`는 관리자가 아니면 활성 사용자가 아니거나, 본인이 아니면서 보이는 멤버십과 활동이 없으면 404입니다.
- `get_current_user`는 일반 API 키로 현재 사용자를 조회합니다.

### 시간 기록

- `list_time_entries`, `get_time_entry`, `create_time_entry`, `update_time_entry`
- 시간 기록 목록은 사용자로 거르지 못합니다. `issueId`가 있으면 `projectId`는 적용되지 않습니다.
- `update_time_entry`는 성공 시 본문이 비어 있습니다.

### 위키

- `list_wiki_pages`, `get_wiki_page`, `put_wiki_page`
- 기존 위키의 `put_wiki_page`는 성공 시 본문이 비어 있습니다. 새 페이지를 만들 때만 페이지 JSON이 옵니다.

### 메타데이터

- `list_issue_statuses`, `list_trackers`, `list_issue_priorities`, `list_time_entry_activities`
- `list_time_entry_activities`는 시스템 공통 활동만 반환합니다. 프로젝트에서 재정의한 활동 ID는 목록에 없습니다.

### 본문 가이드

- `get_text_guide`

## 로컬 개발

로컬에서 이 서버를 시험할 때 IDE가 localhost로 접속하도록 설정합니다.

```json
{
  "servers": {
    "Redmine.MCP.Server": {
      "type": "http",
      "url": "http://localhost:6049"
    }
  }
}
```

개발 프로필은 `Properties/launchSettings.json`의 인수를 사용합니다.

- [Use MCP servers in VS Code](https://code.visualstudio.com/docs/copilot/chat/mcp-servers)
- [Use MCP servers in Visual Studio](https://learn.microsoft.com/visualstudio/ide/mcp-servers)

## 알려진 이슈

1. VS Code에서 `https://localhost:5121` 접속이 실패합니다.
   - 시스템에 신뢰된 self-signed 개발 인증서여도 발생할 수 있습니다.
   - `http://localhost:6049`로는 접속됩니다.
   - 참고: [Cannot connect to MCP server via SSE using trusted developer certificate (microsoft/vscode#248170)](https://github.com/microsoft/vscode/issues/248170)

## 참고

ASP.NET Core MCP 서버는 [ModelContextProtocol.AspNetCore](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) 패키지를 사용합니다.

- [Official Documentation](https://modelcontextprotocol.io/)
- [Protocol Specification](https://spec.modelcontextprotocol.io/)
- [GitHub Organization](https://github.com/modelcontextprotocol)
- [MCP C# SDK](https://csharp.sdk.modelcontextprotocol.io/)
