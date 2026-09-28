using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;

/// <summary>TASK-AOI-03/TASK-EMB-01: Azure OpenAI-backed <see cref="IEmbeddingService"/>.</summary>
public sealed class AzureOpenAiEmbeddingService(AzureOpenAiClientFactory clientFactory, IOptions<AoiOptions> options) : IEmbeddingService
{
    public async Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, string deployment, CancellationToken cancellationToken = default)
    {
        var embeddingClient = clientFactory.Client.GetEmbeddingClient(deployment);

        // `text-embedding-3-*` models natively emit more dimensions than the
        // `vector(1536)` pgvector column stores (e.g. text-embedding-3-large
        // defaults to 3072). Requesting the configured dimension count here
        // uses OpenAI's native (matryoshka) truncation so the output still
        // fits the existing schema without a migration, while remaining far
        // more accurate than the previous ada-002 embeddings at the same
        // stored width. (ada-002 does not support this parameter, but it is
        // no longer the configured embedding model.)
        var embeddingOptions = new EmbeddingGenerationOptions { Dimensions = options.Value.EmbeddingDimensions };

        var collection = await AzureOpenAiRetryPolicy.ExecuteAsync(
            ct => embeddingClient.GenerateEmbeddingsAsync(inputs, embeddingOptions, ct),
            options.Value,
            "embeddings",
            cancellationToken).ConfigureAwait(false);

        return collection.Value
            .OrderBy(e => e.Index)
            .Select(e => e.ToFloats().ToArray())
            .ToList();
    }

    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var embeddingClient = clientFactory.Client.GetEmbeddingClient(options.Value.EmbeddingDeployment);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds));
            await embeddingClient.GenerateEmbeddingsAsync(["ping"], cancellationToken: timeoutCts.Token).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
