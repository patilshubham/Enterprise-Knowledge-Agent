using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Shared.Observability;
using Microsoft.Extensions.Options;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Application.Embeddings;

/// <summary>
/// TASK-EMB-01/02/03: real <see cref="IEmbeddingPipelineStage"/> implementation.
/// Loads a file's not-yet-embedded chunks (<c>Embedding is null</c> -
/// idempotency: a retried run only finishes the remainder), packs them into
/// batches (<see cref="EmbeddingBatchPlanner"/>), calls Azure OpenAI with
/// bounded concurrency across batches, validates each response (vector
/// count/dimension/finite-value, TASK-EMB-02 - a failure here is terminal
/// and non-retryable, <see cref="EmbeddingValidationException"/>), and
/// persists vectors as each batch completes. Persistence (chunk mutation +
/// <c>SaveChangesAsync</c> + progress callback) is serialized behind a
/// dedicated write gate even though the Azure OpenAI calls themselves run
/// concurrently, because the shared EF Core <c>DbContext</c> is not
/// thread-safe. Chunks are activated in one step (TASK-EMB-03) only after
/// every batch has validated and persisted successfully - a mid-way failure
/// leaves the file's chunks inactive/not searchable, and is safe to retry.
/// </summary>
public sealed class AzureOpenAiEmbeddingPipelineStage(
    IKnowledgeChunkRepository chunkRepository,
    IEmbeddingService embeddingService,
    ITokenEstimator tokenEstimator,
    IOptions<AoiOptions> aoiOptions,
    IOptions<EmbeddingOptions> embeddingOptions) : IEmbeddingPipelineStage
{
    public async Task RunAsync(
        Guid applicationId,
        Guid fileId,
        Func<double, CancellationToken, Task>? onProgress = null,
        bool activateOnCompletion = true,
        CancellationToken cancellationToken = default)
    {
        var aoi = aoiOptions.Value;
        var embedding = embeddingOptions.Value;

        var allChunks = await chunkRepository.ListByFileAsync(fileId, activeOnly: false, cancellationToken);
        var pending = allChunks.Where(c => c.Embedding is null).ToList();

        if (pending.Count == 0)
        {
            if (activateOnCompletion)
            {
                await chunkRepository.ActivateAllForFileAsync(fileId, cancellationToken);
            }

            if (onProgress is not null)
            {
                await onProgress(1.0, cancellationToken);
            }

            return;
        }

        var batches = EmbeddingBatchPlanner.Plan(pending, embedding, tokenEstimator, aoi.EmbeddingDeployment);
        AppMetrics.EmbeddingBatches.Add(batches.Count);
        var totalBatches = batches.Count;
        var completedBatches = 0;

        using var concurrencyGate = new SemaphoreSlim(Math.Max(1, embedding.MaxConcurrentBatches));
        using var writeGate = new SemaphoreSlim(1, 1);

        var batchTasks = batches.Select(batch => ProcessBatchAsync(
            batch, aoi, concurrencyGate, writeGate, () => Interlocked.Increment(ref completedBatches), totalBatches, onProgress, cancellationToken));

        await Task.WhenAll(batchTasks);

        // TASK-EMB-03: activate only after every batch has validated + persisted successfully.
        if (activateOnCompletion)
        {
            await chunkRepository.ActivateAllForFileAsync(fileId, cancellationToken);
        }
    }

    private async Task ProcessBatchAsync(
        IReadOnlyList<Domain.Knowledge.KnowledgeChunk> batch,
        AoiOptions aoi,
        SemaphoreSlim concurrencyGate,
        SemaphoreSlim writeGate,
        Func<int> incrementCompleted,
        int totalBatches,
        Func<double, CancellationToken, Task>? onProgress,
        CancellationToken cancellationToken)
    {
        await concurrencyGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var inputs = batch.Select(c => c.Content).ToList();
            var vectors = await embeddingService.GenerateEmbeddingsAsync(inputs, aoi.EmbeddingDeployment, cancellationToken).ConfigureAwait(false);
            Validate(vectors, inputs.Count, aoi.EmbeddingDimensions);

            await writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                for (var i = 0; i < batch.Count; i++)
                {
                    batch[i].SetEmbedding(new Vector(vectors[i]), aoi.EmbeddingDeployment, aoi.EmbeddingDimensions);
                }

                await chunkRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

                var completed = incrementCompleted();
                if (onProgress is not null)
                {
                    await onProgress((double)completed / totalBatches, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                writeGate.Release();
            }
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    private static void Validate(IReadOnlyList<float[]> vectors, int expectedCount, int expectedDimensions)
    {
        if (vectors.Count != expectedCount)
        {
            throw new EmbeddingValidationException(
                $"Azure OpenAI returned {vectors.Count} embedding vector(s) for a batch of {expectedCount} input(s).");
        }

        foreach (var vector in vectors)
        {
            if (vector.Length != expectedDimensions)
            {
                throw new EmbeddingValidationException(
                    $"Azure OpenAI returned a {vector.Length}-dimension embedding vector; expected {expectedDimensions} (configured AzureOpenAI:EmbeddingDimensions).");
            }

            foreach (var value in vector)
            {
                if (!float.IsFinite(value))
                {
                    throw new EmbeddingValidationException("Azure OpenAI returned a non-finite embedding value (NaN/Infinity).");
                }
            }
        }
    }
}
