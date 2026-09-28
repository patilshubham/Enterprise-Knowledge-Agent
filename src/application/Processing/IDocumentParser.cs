namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>TASK-PRC-03: parses normalized text into a structure-preserving sequence of blocks.</summary>
public interface IDocumentParser
{
    /// <summary>
    /// Parses <paramref name="normalizedText"/> (already passed through
    /// <see cref="TextNormalizer.Normalize"/>). <paramref name="isMarkdown"/>
    /// selects GFM-aware Markdown parsing (headings/lists/tables/code
    /// fences preserved) versus plain-paragraph TXT parsing.
    /// </summary>
    IReadOnlyList<ParsedBlock> Parse(string normalizedText, bool isMarkdown);
}
