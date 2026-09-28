using EnterpriseKnowledgeAgent.Domain.Knowledge;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-CHK-02: persistence for <see cref="KnowledgeChunk"/>.</summary>
public interface IKnowledgeChunkRepository
{
    Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeChunk>> ListByFileAsync(Guid fileId, bool activeOnly = false, CancellationToken cancellationToken = default);

    /// <summary>TASK-PRC-01 idempotency (FR-PRC-006): content hashes of a file's not-yet-active chunks. Scoped to inactive rows only (not the live/active generation) so replacement processing (module 15) never treats unchanged content in the CURRENT active version as "already present" and skips recreating it for the new version - that would silently orphan it once the old active generation is removed on swap (TASK-REP-02).</summary>
    Task<IReadOnlySet<string>> ListContentHashesByFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>Module 13: flips every inactive chunk for a file to active in one statement, once embeddings are validated.</summary>
    Task ActivateAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>TASK-KNW-05/reprocess: removes all chunks for a file (they will be regenerated).</summary>
    Task DeleteAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>TASK-REP-02: removes only the not-yet-activated chunks for a file - used to clean up a failed replacement attempt's temporary chunks while leaving the current active generation untouched.</summary>
    Task DeleteInactiveForFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK-REP-02: the atomic replacement swap, in one DB transaction -
    /// deletes the file's currently-active ("old generation") chunks, then
    /// activates its currently-inactive ("new generation", already embedded
    /// by this point) chunks. Only ever called once every new-generation
    /// chunk has a validated embedding (module 13 ran with
    /// <c>activateOnCompletion: false</c>), so old content is never removed
    /// before new content is fully ready to take its place.
    /// </summary>
    Task SwapReplacementAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>TASK-RET-01/D10: distinct <see cref="Domain.Knowledge.KnowledgeChunk.EmbeddingDeployment"/> values among an application's active, embedded chunks - used to detect an embedding-config mismatch between the corpus and the currently configured deployment.</summary>
    Task<IReadOnlySet<string>> ListDistinctEmbeddingDeploymentsAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK-PG-06: exact cosine-similarity search, always scoped by
    /// <paramref name="applicationId"/> in the query itself (FR-RET-001 -
    /// never global-then-filter), restricted to active chunks belonging to
    /// enabled, <see cref="EnterpriseKnowledgeAgent.Shared.Knowledge.UserFacingFileStatus.Ready"/>
    /// files, ordered by ascending cosine distance (closest match first).
    /// </summary>
    Task<IReadOnlyList<KnowledgeChunkSearchResult>> SearchByCosineSimilarityAsync(
        Guid applicationId, Vector queryEmbedding, int topK, CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK-RET-02/TASK-PG-04: Postgres full-text-search retrieval over the
    /// <c>search_vector</c> generated column, scoped by
    /// <paramref name="applicationId"/> in the query itself (never global-
    /// then-filter), restricted to active chunks belonging to enabled, Ready
    /// files, ordered by descending <c>ts_rank</c> (best keyword match first).
    /// </summary>
    Task<IReadOnlyList<KnowledgeChunkKeywordSearchResult>> SearchByFullTextAsync(
        Guid applicationId, string query, int topK, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
