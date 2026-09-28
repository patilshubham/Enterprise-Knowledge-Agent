using EnterpriseKnowledgeAgent.Application.Processing;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Processing;

/// <summary>TASK-PRC-03: unit tests for the structure-preserving Markdown/TXT parser.</summary>
public sealed class MarkdownAwareDocumentParserTests
{
    private readonly MarkdownAwareDocumentParser parser = new();

    [Fact]
    public void Parse_plain_text_splits_on_blank_lines_into_paragraphs()
    {
        var text = "First paragraph line one.\nLine two.\n\nSecond paragraph.\n\n\nThird paragraph.";

        var blocks = parser.Parse(text, isMarkdown: false);

        blocks.Should().HaveCount(3);
        blocks[0].Type.Should().Be(ParsedBlockType.Paragraph);
        blocks[0].Text.Should().Be("First paragraph line one.\nLine two.");
        blocks[1].Text.Should().Be("Second paragraph.");
        blocks[2].Text.Should().Be("Third paragraph.");
    }

    [Fact]
    public void Parse_plain_text_tracks_character_offsets()
    {
        var text = "Hello";

        var blocks = parser.Parse(text, isMarkdown: false);

        blocks.Should().ContainSingle();
        blocks[0].CharacterStart.Should().Be(0);
        blocks[0].Text.Should().Be("Hello");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public void Parse_throws_for_empty_or_whitespace_content(string text)
    {
        var act = () => parser.Parse(text, isMarkdown: false);

        act.Should().Throw<EmptyDocumentException>();
    }

    [Fact]
    public void Parse_markdown_extracts_headings_and_builds_heading_path()
    {
        var text = "# Title\n\n## Section\n\nSome body text.";

        var blocks = parser.Parse(text, isMarkdown: true);

        blocks.Should().Contain(b => b.Type == ParsedBlockType.Heading);
        var body = blocks.Single(b => b.Type == ParsedBlockType.Paragraph);
        body.HeadingPath.Should().Be("Title > Section");
    }

    [Fact]
    public void Parse_markdown_resets_deeper_heading_levels_on_new_sibling_heading()
    {
        var text = "# H1\n\n## H2a\n\nBody A\n\n## H2b\n\nBody B";

        var blocks = parser.Parse(text, isMarkdown: true);

        var bodyA = blocks.Single(b => b.Type == ParsedBlockType.Paragraph && b.Text.Contains("Body A"));
        var bodyB = blocks.Single(b => b.Type == ParsedBlockType.Paragraph && b.Text.Contains("Body B"));
        bodyA.HeadingPath.Should().Be("H1 > H2a");
        bodyB.HeadingPath.Should().Be("H1 > H2b");
    }

    [Fact]
    public void Parse_markdown_preserves_code_fence_verbatim_as_one_atomic_block()
    {
        var text = "# Title\n\n```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```\n";

        var blocks = parser.Parse(text, isMarkdown: true);

        var code = blocks.Should().ContainSingle(b => b.Type == ParsedBlockType.CodeBlock).Subject;
        code.Text.Should().Contain("```csharp");
        code.Text.Should().Contain("var x = 1;");
        code.Text.Should().Contain("```");
    }

    [Fact]
    public void Parse_markdown_emits_a_table_as_one_atomic_block()
    {
        var text = "| A | B |\n| - | - |\n| 1 | 2 |\n";

        var blocks = parser.Parse(text, isMarkdown: true);

        blocks.Should().ContainSingle(b => b.Type == ParsedBlockType.Table);
    }

    [Fact]
    public void Parse_markdown_emits_each_list_item_separately()
    {
        var text = "- item one\n- item two\n- item three\n";

        var blocks = parser.Parse(text, isMarkdown: true);

        blocks.Count(b => b.Type == ParsedBlockType.ListItem).Should().Be(3);
    }
}
