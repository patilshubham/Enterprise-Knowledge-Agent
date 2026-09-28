using EnterpriseKnowledgeAgent.Application.Chunking;
using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Application.Tests.Chunking;

/// <summary>TASK-CHK-01: unit tests for the token-bounded chunker with overlap.</summary>
public sealed class TokenAwareChunkerTests
{
    /// <summary>One token per whitespace-separated word (any whitespace, including the "\n\n" block-join separator) - fully deterministic for test assertions.</summary>
    private sealed class WordCountTokenEstimator : ITokenEstimator
    {
        public int EstimateTokenCount(string text, string? deployment = null) =>
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static TokenAwareChunker CreateChunker(int target = 10, int max = 15, int overlap = 0) =>
        new(new WordCountTokenEstimator(), Options.Create(new ChunkingOptions
        {
            TargetTokens = target,
            MaxTokens = max,
            OverlapTokens = overlap,
        }));

    private static ParsedBlock Paragraph(string text, int start) =>
        new(ParsedBlockType.Paragraph, text, string.Empty, start, start + text.Length);

    [Fact]
    public void Chunk_merges_small_blocks_up_to_target_tokens()
    {
        var chunker = CreateChunker(target: 10, max: 15, overlap: 0);
        var blocks = new[]
        {
            Paragraph("one two three", 0),   // 3 tokens
            Paragraph("four five six", 20),  // 3 tokens
        };

        var chunks = chunker.Chunk(blocks);

        chunks.Should().ContainSingle();
        chunks[0].Content.Should().Be("one two three\n\nfour five six");
        chunks[0].TokenCount.Should().Be(6);
    }

    [Fact]
    public void Chunk_splits_when_target_would_be_exceeded()
    {
        var chunker = CreateChunker(target: 5, max: 15, overlap: 0);
        var blocks = new[]
        {
            Paragraph("one two three", 0),     // 3 tokens
            Paragraph("four five six seven", 20), // 4 tokens -> 3+4=7 > target(5)
        };

        var chunks = chunker.Chunk(blocks);

        chunks.Should().HaveCount(2);
        chunks[0].Content.Should().Be("one two three");
        chunks[1].Content.Should().Be("four five six seven");
    }

    [Fact]
    public void Chunk_never_splits_a_single_oversized_block()
    {
        var chunker = CreateChunker(target: 3, max: 5, overlap: 0);
        var hugeBlock = Paragraph(string.Join(' ', Enumerable.Range(0, 20).Select(i => $"w{i}")), 0); // 20 tokens > max(5)

        var chunks = chunker.Chunk([hugeBlock]);

        chunks.Should().ContainSingle();
        chunks[0].TokenCount.Should().Be(20);
    }

    [Fact]
    public void Chunk_first_chunk_heading_path_is_from_its_first_block()
    {
        var chunker = CreateChunker();
        var block = new ParsedBlock(ParsedBlockType.Paragraph, "some text", "H1 > H2", 0, 9);

        var chunks = chunker.Chunk([block]);

        chunks[0].HeadingPath.Should().Be("H1 > H2");
    }

    [Fact]
    public void Chunk_applies_overlap_by_prefixing_tail_of_previous_chunk()
    {
        var chunker = CreateChunker(target: 3, max: 3, overlap: 3);
        var blocks = new[]
        {
            Paragraph("aaaa bbbb cccc", 0),
            Paragraph("dddd eeee ffff", 20),
        };

        var chunks = chunker.Chunk(blocks);

        chunks.Should().HaveCount(2);
        chunks[0].Content.Should().Be("aaaa bbbb cccc");
        // second chunk should be prefixed with a tail of the first chunk's content.
        chunks[1].Content.Should().EndWith("dddd eeee ffff");
        chunks[1].Content.Should().StartWith("bbbb cccc");
    }

    [Fact]
    public void Chunk_with_zero_overlap_leaves_chunks_unmodified()
    {
        var chunker = CreateChunker(target: 3, max: 3, overlap: 0);
        var blocks = new[]
        {
            Paragraph("aaaa bbbb cccc", 0),
            Paragraph("dddd eeee ffff", 20),
        };

        var chunks = chunker.Chunk(blocks);

        chunks[1].Content.Should().Be("dddd eeee ffff");
    }

    [Fact]
    public void Chunk_empty_block_list_returns_no_chunks()
    {
        var chunker = CreateChunker();

        var chunks = chunker.Chunk([]);

        chunks.Should().BeEmpty();
    }
}
