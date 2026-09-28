using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Application.Tests.Retrieval;

/// <summary>
/// Minimal in-memory fakes for <see cref="RetrievalService"/>'s dependencies.
/// The chunk-search fakes return pre-configured results regardless of the
/// query embedding/text (the real pgvector/FTS SQL is covered by the
/// Infrastructure integration tests) - these fakes exist purely to exercise
/// <see cref="RetrievalService"/>'s own orchestration logic (config-mismatch
/// detection, file scoping, fusion/dedup/neighbor/budget wiring, and
/// no-evidence/excluded-file reporting).
/// </summary>
internal sealed class FakeRetrievalChunkRepository : IKnowledgeChunkRepository
{
    public IReadOnlyList<KnowledgeChunkSearchResult> SemanticResults { get; set; } = [];

    public IReadOnlyList<KnowledgeChunkKeywordSearchResult> KeywordResults { get; set; } = [];

    public IReadOnlySet<string> DistinctDeployments { get; set; } = new HashSet<string>();

    public Dictionary<Guid, IReadOnlyList<KnowledgeChunk>> ChunksByFile { get; } = [];

    public bool FullTextSearchWasCalled { get; private set; }

    public Guid? LastSemanticSearchApplicationId { get; private set; }

    public Guid? LastKeywordSearchApplicationId { get; private set; }

    public Guid? LastDeploymentsApplicationId { get; private set; }

    public Task<IReadOnlyList<KnowledgeChunkSearchResult>> SearchByCosineSimilarityAsync(
        Guid applicationId, Vector queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        LastSemanticSearchApplicationId = applicationId;
        return Task.FromResult(SemanticResults);
    }

    public Task<IReadOnlyList<KnowledgeChunkKeywordSearchResult>> SearchByFullTextAsync(
        Guid applicationId, string query, int topK, CancellationToken cancellationToken = default)
    {
        FullTextSearchWasCalled = true;
        LastKeywordSearchApplicationId = applicationId;
        return Task.FromResult(KeywordResults);
    }

    public Task<IReadOnlyList<KnowledgeChunk>> ListByFileAsync(Guid fileId, bool activeOnly = false, CancellationToken cancellationToken = default) =>
        Task.FromResult(ChunksByFile.GetValueOrDefault(fileId, []));

    public Task<IReadOnlySet<string>> ListDistinctEmbeddingDeploymentsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        LastDeploymentsApplicationId = applicationId;
        return Task.FromResult(DistinctDeployments);
    }

    public Task<IReadOnlySet<string>> ListContentHashesByFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task ActivateAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task DeleteAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task DeleteInactiveForFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task SwapReplacementAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");
}

internal sealed class FakeRetrievalFileRepository : IKnowledgeFileRepository
{
    public List<KnowledgeFile> Files { get; } = [];

    public Task<IReadOnlyList<KnowledgeFile>> ListAsync(
        Guid applicationId, string? search = null, UserFacingFileStatus? status = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeFile>>([.. Files.Where(f => f.ApplicationId == applicationId)]);

    public Task<KnowledgeFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Files.FirstOrDefault(f => f.Id == fileId));

    public Task AddAsync(KnowledgeFile file, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task<bool> ExistsActiveHashAsync(Guid applicationId, string contentHash, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task<IReadOnlyDictionary<UserFacingFileStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Not exercised by retrieval tests.");
}

internal sealed class FakeRetrievalEmbeddingService : IEmbeddingService
{
    public readonly List<(IReadOnlyList<string> Inputs, string Deployment)> Calls = [];

    public float[] VectorToReturn { get; set; } = [1f, 0f, 0f, 0f];

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, string deployment, CancellationToken cancellationToken = default)
    {
        Calls.Add((inputs, deployment));
        return Task.FromResult<IReadOnlyList<float[]>>([.. inputs.Select(_ => VectorToReturn)]);
    }

    public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}

internal sealed class FakeRetrievalTokenEstimator : ITokenEstimator
{
    public int EstimateTokenCount(string text, string? deployment = null) => text.Length;
}

internal static class RetrievalTestFileFactory
{
    /// <summary>Creates a Ready + enabled <see cref="KnowledgeFile"/> for retrieval tests (Queued -&gt; Processing -&gt; Ready).</summary>
    public static KnowledgeFile CreateReadyFile(Guid applicationId, string displayName = "doc.txt")
    {
        var now = DateTimeOffset.UtcNow;
        var file = KnowledgeFile.Create(applicationId, displayName, contentHash: Guid.NewGuid().ToString("N"), fileSize: 100, mimeType: "text/plain", encoding: "utf-8", uploadedBy: "tester", now);
        file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/original");
        file.MarkProcessing(now);
        file.MarkReady(now);
        return file;
    }

    public static KnowledgeChunk CreateChunk(Guid applicationId, Guid fileId, int sequenceNumber, string headingPath, string content, int start, int end, string? contentHash = null) =>
        KnowledgeChunk.Create(applicationId, fileId, sequenceNumber, headingPath, content, content.Length, start, end, contentHash ?? Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
}
