using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Generation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Generation;

/// <summary>TASK-GEN-02: unit tests for <see cref="MarkdownOutputValidator"/>'s extraction + validation.</summary>
public sealed class MarkdownOutputValidatorTests
{
    [Fact]
    public void ValidateAndExtract_for_valid_markdown_returns_the_content_unchanged()
    {
        const string markdown = "# Heading\n\nSome body text with a claim [SOURCE-001].";

        var result = MarkdownOutputValidator.ValidateAndExtract(markdown);

        result.OriginalMarkdown.Should().Be(markdown);
    }

    [Fact]
    public void ValidateAndExtract_extracts_distinct_citation_markers_in_first_appearance_order()
    {
        const string markdown = "Claim one [SOURCE-002]. Claim two [SOURCE-001]. Repeated [SOURCE-002].";

        var result = MarkdownOutputValidator.ValidateAndExtract(markdown);

        result.CitationMarkers.Should().Equal("[SOURCE-002]", "[SOURCE-001]");
    }

    [Fact]
    public void ValidateAndExtract_with_no_citation_markers_returns_an_empty_list()
    {
        var result = MarkdownOutputValidator.ValidateAndExtract("Just plain Markdown, no citations.");

        result.CitationMarkers.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t ")]
    public void ValidateAndExtract_for_empty_or_blank_content_throws_output_validation_failure(string? content)
    {
        var act = () => MarkdownOutputValidator.ValidateAndExtract(content);

        act.Should().Throw<AgentRunExecutionException>()
            .Which.Category.Should().Be(ErrorCategory.OutputValidationFailure);
    }
}
