namespace Redmine.MCP.Server.Tests;

public sealed class TextGuideToolsTests
{
    [Theory]
    [InlineData("issue")]
    [InlineData("comment")]
    [InlineData("wiki")]
    public void GetTextGuide_IncludesSharedReservedWords(string target)
    {
        string guide = new TextGuideTools().GetTextGuide(target);

        string[] reserved =
        [
            "*굵게*",
            "_기울임_",
            "+밑줄+",
            "-취소선-",
            "@코드@",
            "??인용??",
            "h1.",
            "h3.",
            "bq.",
            "|표|셀|",
            "<pre><code class=\"ruby\">",
            "c, cpp, css, delphi, groovy, html, java, javascript, json, php, python, rhtml, ruby, scheme, sql, xml, yaml",
            "#124",
            "#124-6",
            "#124#note-6",
            "[[페이지]]",
            "[[페이지#앵커|문구]]",
            "[[프로젝트:페이지]]",
            "document#17",
            "document:제목",
            "version#3",
            "version:1.0.0",
            "attachment:file.zip",
            "r758",
            "commit:해시",
            "source:경로@52#L120",
            "export:경로",
            "message#1218",
            "project#3",
            "project:식별자",
            "!#124",
            "{{toc}}",
            "{{>toc}}",
            "{{include(페이지)}}",
            "{{child_pages}}",
            "{{collapse(제목)",
            "{{thumbnail(파일)}}",
            "{{macro_list}}",
            "{{hello_world}}"
        ];
        foreach (string token in reserved)
        {
            Assert.Contains(token, guide, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("{{collapse}}", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTextGuide_IssueTemplateUsesPlainSubject()
    {
        string guide = new TextGuideTools().GetTextGuide("issue");

        Assert.Contains("subject는 일반 텍스트", guide, StringComparison.Ordinal);
        Assert.Contains("h2. 배경", guide, StringComparison.Ordinal);
        Assert.Contains("h2. 재현 절차", guide, StringComparison.Ordinal);
        Assert.Contains("h2. 기대 결과", guide, StringComparison.Ordinal);
        Assert.Contains("h2. 실제 결과", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTextGuide_CommentTemplateRejectsEditingExistingNotes()
    {
        string guide = new TextGuideTools().GetTextGuide("comment");

        Assert.Contains("update_issue의 notes", guide, StringComparison.Ordinal);
        Assert.Contains("기존 댓글은 이 Redmine 2.2에 수정 REST가 없어 고칠 수 없다.", guide, StringComparison.Ordinal);
        Assert.Contains("bq. 인용할 내용", guide, StringComparison.Ordinal);
        Assert.Contains("h3. 확인", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTextGuide_WikiTemplateUsesWikiMacros()
    {
        string guide = new TextGuideTools().GetTextGuide("wiki");

        Assert.Contains("put_wiki_page의 text", guide, StringComparison.Ordinal);
        Assert.Contains("{{include(페이지)}}와 {{child_pages}}는 위키 페이지에서 쓴다.", guide, StringComparison.Ordinal);
        Assert.Contains("{{>toc}}", guide, StringComparison.Ordinal);
        Assert.Contains("h2. 개요", guide, StringComparison.Ordinal);
        Assert.Contains("h2. 관련 일감", guide, StringComparison.Ordinal);
    }

    [Fact]
    public void GetTextGuide_RejectsUnknownTarget()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new TextGuideTools().GetTextGuide("search"));

        Assert.Contains("issue, comment, wiki", exception.Message, StringComparison.Ordinal);
    }
}
