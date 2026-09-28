using EnterpriseKnowledgeAgent.Application.Embeddings;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Embeddings;

/// <summary>TASK-EMB-01: batch packing respecting both count and token-budget limits.</summary>
public sealed class EmbeddingBatchPlannerTests
{
    /// <summary>One token per whitespace-separated word - deterministic for test assertions.</summary>
    private sealed class WordCountTokenEstimator : ITokenEstimator
    {
        public int EstimateTokenCount(string text, string? deployment = null) =>
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static KnowledgeChunk NewChunk(int sequenceNumber, string content) =>
        KnowledgeChunk.Create(Guid.NewGuid(), Guid.NewGuid(), sequenceNumber, "H1", content, content.Length, 0, content.Length, $"hash-{sequenceNumber}", DateTimeOffset.UtcNow);

    [Fact]
    public void Plan_returns_no_batches_for_an_empty_chunk_list()
    {
        var batches = EmbeddingBatchPlanner.Plan([], new EmbeddingOptions(), new WordCountTokenEstimator());

        batches.Should().BeEmpty();
    }

    [Fact]
    public void Plan_packs_all_chunks_into_one_batch_when_under_both_limits()
    {
        var chunks = new[] { NewChunk(0, "one two"), NewChunk(1, "three four") };
        var options = new EmbeddingOptions { MaxInputsPerBatch = 16, MaxTokensPerBatch = 8_000 };

        var batches = EmbeddingBatchPlanner.Plan(chunks, options, new WordCountTokenEstimator());

        batches.Should().HaveCount(1);
        batches[0].Should().Equal(chunks);
    }

    [Fact]
    public void Plan_starts_a_new_batch_once_max_inputs_per_batch_is_reached()
    {
        var chunks = new[] { NewChunk(0, "a"), NewChunk(1, "b"), NewChunk(2, "c") };
        var options = new EmbeddingOptions { MaxInputsPerBatch = 2, MaxTokensPerBatch = 8_000 };

        var batches = EmbeddingBatchPlanner.Plan(chunks, options, new WordCountTokenEstimator());

        batches.Should().HaveCount(2);
        batches[0].Should().HaveCount(2);
        batches[1].Should().HaveCount(1);
    }

    [Fact]
    public void Plan_starts_a_new_batch_once_max_tokens_per_batch_would_be_exceeded()
    {
        var chunks = new[] { NewChunk(0, "one two three"), NewChunk(1, "four five six"), NewChunk(2, "seven") };
        var options = new EmbeddingOptions { MaxInputsPerBatch = 16, MaxTokensPerBatch = 5 };

        var batches = EmbeddingBatchPlanner.Plan(chunks, options, new WordCountTokenEstimator());

        // Chunk 0 alone = 3 tokens (fits). Adding chunk 1 (3 more) would total 6 > 5, so chunk 1 starts a new batch.
        // Chunk 1 alone = 3 tokens (fits). Adding chunk 2 (1 more) totals 4 <= 5, so chunk 2 joins chunk 1's batch.
        batches.Should().HaveCount(2);
        batches[0].Should().Equal(chunks[0]);
        batches[1].Should().Equal(chunks[1], chunks[2]);
    }

    [Fact]
    public void Plan_never_produces_an_empty_batch_even_when_a_single_chunk_exceeds_the_token_budget()
    {
        var chunks = new[] { NewChunk(0, "one two three four five") };
        var options = new EmbeddingOptions { MaxInputsPerBatch = 16, MaxTokensPerBatch = 1 };

        var batches = EmbeddingBatchPlanner.Plan(chunks, options, new WordCountTokenEstimator());

        batches.Should().HaveCount(1);
        batches[0].Should().Equal(chunks[0]);
    }
}
