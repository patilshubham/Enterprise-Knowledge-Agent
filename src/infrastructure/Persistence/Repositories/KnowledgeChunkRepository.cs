using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-CHK-02: EF Core-backed <see cref="IKnowledgeChunkRepository"/>.</summary>
public sealed class KnowledgeChunkRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IKnowledgeChunkRepository
{
    public Task AddRangeAsync(IEnumerable<KnowledgeChunk> chunks, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeChunks.AddRangeAsync(chunks, cancellationToken);

    public async Task<IReadOnlyList<KnowledgeChunk>> ListByFileAsync(Guid fileId, bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeChunks.Where(c => c.FileId == fileId);
        if (activeOnly)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query.OrderBy(c => c.SequenceNumber).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> ListContentHashesByFileAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var hashes = await dbContext.KnowledgeChunks
            .Where(c => c.FileId == fileId && !c.IsActive)
            .Select(c => c.ContentHash)
            .ToListAsync(cancellationToken);

        return hashes.ToHashSet();
    }

    public Task ActivateAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeChunks
            .Where(c => c.FileId == fileId && !c.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsActive, true), cancellationToken);

    public Task DeleteAllForFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeChunks
            .Where(c => c.FileId == fileId)
            .ExecuteDeleteAsync(cancellationToken);

    public Task DeleteInactiveForFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeChunks
            .Where(c => c.FileId == fileId && !c.IsActive)
            .ExecuteDeleteAsync(cancellationToken);

    public async Task SwapReplacementAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Old generation: currently-active chunks for this file - safe to remove now that
        // every new-generation chunk below already has a validated embedding.
        await dbContext.KnowledgeChunks
            .Where(c => c.FileId == fileId && c.IsActive)
            .ExecuteDeleteAsync(cancellationToken);

        // New generation: the just-embedded, still-inactive chunks - promote them to active.
        await dbContext.KnowledgeChunks
            .Where(c => c.FileId == fileId && !c.IsActive)
            .ExecuteUpdateAsync(setters => setters.SetProperty(c => c.IsActive, true), cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlySet<string>> ListDistinctEmbeddingDeploymentsAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var deployments = await dbContext.KnowledgeChunks
            .Where(c => c.ApplicationId == applicationId && c.IsActive && c.EmbeddingDeployment != null)
            .Select(c => c.EmbeddingDeployment!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return deployments.ToHashSet();
    }

    public async Task<IReadOnlyList<KnowledgeChunkSearchResult>> SearchByCosineSimilarityAsync(
        Guid applicationId, Vector queryEmbedding, int topK, CancellationToken cancellationToken = default)
    {
        var readyFileIds = dbContext.KnowledgeFiles
            .Where(f => f.ApplicationId == applicationId && f.IsEnabled && f.Status == UserFacingFileStatus.Ready)
            .Select(f => f.Id);

        var ranked = await dbContext.KnowledgeChunks
            .Where(c => c.ApplicationId == applicationId && c.IsActive && c.Embedding != null && readyFileIds.Contains(c.FileId))
            .Select(c => new { Chunk = c, Distance = c.Embedding!.CosineDistance(queryEmbedding) })
            .OrderBy(x => x.Distance)
            .Take(topK)
            .ToListAsync(cancellationToken);

        return ranked.Select(x => new KnowledgeChunkSearchResult(x.Chunk, x.Distance)).ToList();
    }

    public async Task<IReadOnlyList<KnowledgeChunkKeywordSearchResult>> SearchByFullTextAsync(
        Guid applicationId, string query, int topK, CancellationToken cancellationToken = default)
    {
        // TASK-PG-04/RET-02: ts_rank isn't projectable through the shadow
        // "SearchVector" property via LINQ, so this ranks + scopes via a raw
        // query returning (id, rank) pairs, then batch-loads the full
        // entities by id and re-attaches each rank in ranked order.
        var readyStatus = UserFacingFileStatus.Ready.ToString();
        var ranked = await dbContext.Database.SqlQuery<RankedChunkId>(
            $"""
             SELECT c."Id" AS "Id", ts_rank(c.search_vector, plainto_tsquery('english', {query})) AS "Rank"
             FROM knowledge.knowledge_chunks c
             INNER JOIN knowledge.knowledge_files f ON f."Id" = c."FileId"
             WHERE c."ApplicationId" = {applicationId}
               AND c."IsActive" = TRUE
               AND f."IsEnabled" = TRUE
               AND f."Status" = {readyStatus}
               AND c.search_vector @@ plainto_tsquery('english', {query})
             ORDER BY ts_rank(c.search_vector, plainto_tsquery('english', {query})) DESC
             LIMIT {topK}
             """).ToListAsync(cancellationToken);

        if (ranked.Count == 0)
        {
            return [];
        }

        var ids = ranked.Select(r => r.Id).ToList();
        var chunksById = await dbContext.KnowledgeChunks
            .Where(c => ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, cancellationToken);

        return ranked
            .Where(r => chunksById.ContainsKey(r.Id))
            .Select(r => new KnowledgeChunkKeywordSearchResult(chunksById[r.Id], r.Rank))
            .ToList();
    }

    private sealed record RankedChunkId(Guid Id, double Rank);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
