namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>TASK-PRC-03: the kind of content a <see cref="ParsedBlock"/> carries.</summary>
public enum ParsedBlockType
{
    Heading,
    Paragraph,
    CodeBlock,
    ListItem,
    Table,
    BlockQuote,
}

/// <summary>
/// TASK-PRC-03: one structural unit of a parsed document - a heading,
/// paragraph, code fence, list item, table, or block quote - carrying its
/// heading-path context and source character offsets (ASDD §13.1, §15.6).
/// <see cref="CharacterStart"/>/<see cref="CharacterEnd"/> index into the
/// normalized text the parser was given (end-exclusive).
/// </summary>
public sealed record ParsedBlock(
    ParsedBlockType Type,
    string Text,
    string HeadingPath,
    int CharacterStart,
    int CharacterEnd);
