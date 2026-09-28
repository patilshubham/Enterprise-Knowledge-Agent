using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Chunking;

/// <summary>
/// TASK-CHK-01: groups parsed blocks into token-bounded chunks, preferring
/// heading/paragraph boundaries at the target size and enforcing a hard
/// max, then TASK-CHK-01's overlap step carries a tail of the previous
/// chunk's text into the next chunk for retrieval continuity. Every
/// <see cref="ParsedBlock"/> is atomic - a code fence or table block is
/// never split mid-block even if it alone exceeds <see cref="ChunkingOptions.MaxTokens"/>.
/// </summary>
public sealed class TokenAwareChunker(ITokenEstimator tokenEstimator, IOptions<ChunkingOptions> options) : IChunker
{
    public IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<ParsedBlock> blocks)
    {
        var opts = options.Value;
        var drafts = new List<ChunkDraft>();
        var current = new List<ParsedBlock>();
        var currentTokens = 0;

        void FlushCurrent()
        {
            if (current.Count == 0)
            {
                return;
            }

            var content = string.Join("\n\n", current.Select(b => b.Text));
            var heading = current[0].HeadingPath;
            var start = current[0].CharacterStart;
            var end = current[^1].CharacterEnd;
            drafts.Add(new ChunkDraft(heading, content, tokenEstimator.EstimateTokenCount(content), start, end));

            current.Clear();
            currentTokens = 0;
        }

        foreach (var block in blocks)
        {
            var blockTokens = tokenEstimator.EstimateTokenCount(block.Text);

            // Oversized single block (e.g. a huge code fence): never split it - emit alone, whole, even over max.
            if (blockTokens > opts.MaxTokens)
            {
                FlushCurrent();
                drafts.Add(new ChunkDraft(block.HeadingPath, block.Text, blockTokens, block.CharacterStart, block.CharacterEnd));
                continue;
            }

            var wouldExceedMax = currentTokens + blockTokens > opts.MaxTokens;
            var wouldExceedTargetWithContent = currentTokens > 0 && currentTokens + blockTokens > opts.TargetTokens;

            if (wouldExceedMax || wouldExceedTargetWithContent)
            {
                FlushCurrent();
            }

            current.Add(block);
            currentTokens += blockTokens;
        }

        FlushCurrent();

        return ApplyOverlap(drafts, opts.OverlapTokens);
    }

    private List<ChunkDraft> ApplyOverlap(List<ChunkDraft> drafts, int overlapTokens)
    {
        if (overlapTokens <= 0 || drafts.Count < 2)
        {
            return drafts;
        }

        var result = new List<ChunkDraft>(drafts.Count) { drafts[0] };
        for (var i = 1; i < drafts.Count; i++)
        {
            var previous = drafts[i - 1];
            var current = drafts[i];

            var overlapText = TakeLastApproxTokens(previous.Content, overlapTokens);
            if (overlapText.Length == 0)
            {
                result.Add(current);
                continue;
            }

            var mergedContent = overlapText + "\n\n" + current.Content;
            result.Add(current with { Content = mergedContent, TokenCount = tokenEstimator.EstimateTokenCount(mergedContent) });
        }

        return result;
    }

    private static string TakeLastApproxTokens(string content, int overlapTokens)
    {
        const int approxCharsPerToken = 4;
        var approxChars = Math.Min(content.Length, overlapTokens * approxCharsPerToken);
        if (approxChars <= 0)
        {
            return string.Empty;
        }

        var slice = content[^approxChars..];

        // Avoid cutting mid-word: start from the first word boundary inside the slice.
        var spaceIndex = slice.IndexOf(' ', StringComparison.Ordinal);
        if (spaceIndex > 0 && spaceIndex < slice.Length - 1)
        {
            slice = slice[(spaceIndex + 1)..];
        }

        return slice.Trim();
    }
}
