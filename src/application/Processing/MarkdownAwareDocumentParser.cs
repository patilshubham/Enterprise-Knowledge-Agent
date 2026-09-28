using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;

namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>
/// TASK-PRC-03: GFM-capable Markdown parser (Markdig) producing a
/// structure-preserving block sequence with heading paths and source
/// offsets; plain TXT is parsed as blank-line-delimited paragraph blocks.
/// Block text is always sliced verbatim from the normalized source (never
/// re-serialized from the AST) so code fences and tables are reproduced
/// exactly, including fence markers.
/// </summary>
public sealed class MarkdownAwareDocumentParser : IDocumentParser
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

    public IReadOnlyList<ParsedBlock> Parse(string normalizedText, bool isMarkdown)
    {
        ArgumentNullException.ThrowIfNull(normalizedText);

        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            throw new EmptyDocumentException("Document content is empty after normalization.");
        }

        var blocks = isMarkdown ? ParseMarkdown(normalizedText) : ParsePlainText(normalizedText);

        if (blocks.Count == 0)
        {
            throw new EmptyDocumentException("Document produced no content blocks after parsing.");
        }

        return blocks;
    }

    private static List<ParsedBlock> ParseMarkdown(string text)
    {
        var document = Markdown.Parse(text, Pipeline);
        var result = new List<ParsedBlock>();
        var headingStack = new string?[7]; // 1-based, index 0 unused

        WalkBlocks(document, text, headingStack, result);
        return result;
    }

    private static void WalkBlocks(ContainerBlock container, string text, string?[] headingStack, List<ParsedBlock> result)
    {
        foreach (var block in container)
        {
            switch (block)
            {
                case HeadingBlock heading:
                    for (var level = heading.Level; level <= 6; level++)
                    {
                        headingStack[level] = null;
                    }

                    headingStack[heading.Level] = ExtractHeadingText(Slice(text, block.Span));
                    result.Add(new ParsedBlock(
                        ParsedBlockType.Heading, Slice(text, block.Span), BuildPath(headingStack), block.Span.Start, block.Span.End + 1));
                    break;

                case Table table:
                    result.Add(new ParsedBlock(
                        ParsedBlockType.Table, Slice(text, table.Span), BuildPath(headingStack), table.Span.Start, table.Span.End + 1));
                    break;

                case CodeBlock code:
                    result.Add(new ParsedBlock(
                        ParsedBlockType.CodeBlock, Slice(text, code.Span), BuildPath(headingStack), code.Span.Start, code.Span.End + 1));
                    break;

                case QuoteBlock quote:
                    result.Add(new ParsedBlock(
                        ParsedBlockType.BlockQuote, Slice(text, quote.Span), BuildPath(headingStack), quote.Span.Start, quote.Span.End + 1));
                    break;

                case ListBlock list:
                    foreach (var item in list.OfType<ListItemBlock>())
                    {
                        result.Add(new ParsedBlock(
                            ParsedBlockType.ListItem, Slice(text, item.Span), BuildPath(headingStack), item.Span.Start, item.Span.End + 1));
                    }
                    break;

                case ParagraphBlock paragraph:
                    result.Add(new ParsedBlock(
                        ParsedBlockType.Paragraph, Slice(text, paragraph.Span), BuildPath(headingStack), paragraph.Span.Start, paragraph.Span.End + 1));
                    break;

                case ThematicBreakBlock:
                    break; // decorative only - not content

                case ContainerBlock nested:
                    WalkBlocks(nested, text, headingStack, result);
                    break;
            }
        }
    }

    private static List<ParsedBlock> ParsePlainText(string text)
    {
        var result = new List<ParsedBlock>();
        var lines = text.Split('\n');
        var pos = 0;
        var paragraphStart = -1;
        var buffer = new List<string>();

        void Flush(int endPos)
        {
            if (buffer.Count == 0)
            {
                return;
            }

            var content = string.Join('\n', buffer).Trim();
            if (content.Length > 0)
            {
                result.Add(new ParsedBlock(ParsedBlockType.Paragraph, content, string.Empty, paragraphStart, endPos));
            }

            buffer.Clear();
            paragraphStart = -1;
        }

        foreach (var line in lines)
        {
            var lineStart = pos;
            if (string.IsNullOrWhiteSpace(line))
            {
                Flush(lineStart);
            }
            else
            {
                if (paragraphStart < 0)
                {
                    paragraphStart = lineStart;
                }

                buffer.Add(line);
            }

            pos += line.Length + 1;
        }

        Flush(pos);
        return result;
    }

    private static string ExtractHeadingText(string raw)
    {
        var firstLine = raw.Split('\n')[0].Trim();
        return firstLine.TrimStart('#').Trim();
    }

    private static string BuildPath(string?[] headingStack) =>
        string.Join(" > ", headingStack.Skip(1).Where(h => !string.IsNullOrEmpty(h)));

    private static string Slice(string text, SourceSpan span)
    {
        if (span.Start < 0 || span.End < span.Start)
        {
            return string.Empty;
        }

        var start = span.Start;
        var length = Math.Min(span.End + 1, text.Length) - start;
        return length <= 0 ? string.Empty : text.Substring(start, length);
    }
}
