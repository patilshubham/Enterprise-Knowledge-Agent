using System.Text.RegularExpressions;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Shared.Errors;
using Markdig;

namespace EnterpriseKnowledgeAgent.Application.Generation;

/// <summary>TASK-GEN-02: a validated generation result ready for citation validation (TASK-CIT-03).</summary>
/// <param name="OriginalMarkdown">The model's response, byte-for-byte unchanged - this is what module 23 persists immutably as <c>GeneratedOutput.OriginalMarkdown</c>.</param>
/// <param name="CitationMarkers">Every distinct <c>[SOURCE-NNN]</c> marker found, in first-appearance order (module 22 validates each against the run's evidence).</param>
public sealed record ValidatedMarkdownOutput(string OriginalMarkdown, IReadOnlyList<string> CitationMarkers);

/// <summary>
/// TASK-GEN-02: extracts citation markers from a generation response and
/// validates it is well-formed, non-empty Markdown (ASDD §16, §20.3) -
/// classifying <see cref="ErrorCategory.OutputValidationFailure"/> for
/// invalid/empty content. Pure/static and stateless, so directly unit
/// testable. Citation-marker syntax matches the protocol authored in
/// <see cref="EnterpriseKnowledgeAgent.Application.PromptAssembly.PromptAssembler.ProtocolInstructions"/>.
/// </summary>
public static class MarkdownOutputValidator
{
    private static readonly Regex CitationMarkerPattern = new(@"\[SOURCE-\d{3}\]", RegexOptions.Compiled);

    public static ValidatedMarkdownOutput ValidateAndExtract(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AgentRunExecutionException(ErrorCategory.OutputValidationFailure, "The model returned an empty response.");
        }

        var document = Markdown.Parse(content);
        if (document.Count == 0)
        {
            throw new AgentRunExecutionException(ErrorCategory.OutputValidationFailure, "The model's response contained no renderable Markdown content.");
        }

        var citationMarkers = CitationMarkerPattern.Matches(content)
            .Select(match => match.Value)
            .Distinct()
            .ToList();

        return new ValidatedMarkdownOutput(content, citationMarkers);
    }
}
