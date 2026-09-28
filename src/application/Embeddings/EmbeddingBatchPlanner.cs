using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Embeddings;

/// <summary>TASK-EMB-01: packs chunks into batches respecting both a max input count and a max total estimated-token budget per batch.</summary>
public static class EmbeddingBatchPlanner
{
    public static IReadOnlyList<IReadOnlyList<KnowledgeChunk>> Plan(
        IReadOnlyList<KnowledgeChunk> chunks,
        EmbeddingOptions options,
        ITokenEstimator tokenEstimator,
        string? deployment = null)
    {
        var batches = new List<List<KnowledgeChunk>>();
        List<KnowledgeChunk>? current = null;
        var currentTokens = 0;

        foreach (var chunk in chunks)
        {
            var chunkTokens = tokenEstimator.EstimateTokenCount(chunk.Content, deployment);
            var overflowsCount = current is { Count: > 0 } && current.Count >= options.MaxInputsPerBatch;
            var overflowsTokens = current is { Count: > 0 } && currentTokens + chunkTokens > options.MaxTokensPerBatch;

            if (current is null || overflowsCount || overflowsTokens)
            {
                current = [];
                batches.Add(current);
                currentTokens = 0;
            }

            current.Add(chunk);
            currentTokens += chunkTokens;
        }

        return batches;
    }
}
