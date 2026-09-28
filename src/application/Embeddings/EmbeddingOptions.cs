namespace EnterpriseKnowledgeAgent.Application.Embeddings;

/// <summary>TASK-EMB-01: batching/concurrency limits for the embedding pipeline, bound from the "Embedding" config section.</summary>
public sealed class EmbeddingOptions
{
    public const string SectionName = "Embedding";

    /// <summary>Maximum number of chunk contents sent in a single Azure OpenAI embeddings request.</summary>
    public int MaxInputsPerBatch { get; init; } = 16;

    /// <summary>Maximum total estimated tokens packed into a single batch (keeps well under typical embedding-model per-request limits).</summary>
    public int MaxTokensPerBatch { get; init; } = 8_000;

    /// <summary>Bounded degree of parallelism across batches (SemaphoreSlim-limited AOAI calls).</summary>
    public int MaxConcurrentBatches { get; init; } = 4;
}
