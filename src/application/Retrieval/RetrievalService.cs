using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Tokenization;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Observability;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using Microsoft.Extensions.Options;
using Pgvector;

namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-01..05: implements the full retrieval pipeline - query embedding
/// + app-scoped semantic search (RET-01), optional hybrid FTS + rank fusion
/// (RET-02), dedup/overlap suppression + neighbor inclusion (RET-03),
/// threshold + evidence-token-budget fill with SOURCE IDs (RET-04), and
/// no-evidence/partial-readiness reporting (RET-05).
/// </summary>
/// <remarks>
/// The existing chunk-search repository methods (TASK-PG-04/06) are
/// app-scoped only, with no per-file filter. To honor
/// <see cref="RetrievalRequest.SelectedFileIds"/> without changing that
/// contract (and its already-tested SQL), this service over-fetches a wider
/// candidate pool from the repository and then applies the file-id filter
/// in memory before fusion/dedup/budgeting. <see cref="CandidatePoolMultiplier"/>
/// and <see cref="MinimumCandidatePoolSize"/> control how wide that pool is.
/// </remarks>
public sealed class RetrievalService(
    IKnowledgeChunkRepository chunkRepository,
    IKnowledgeFileRepository fileRepository,
    IEmbeddingService embeddingService,
    ITokenEstimator tokenEstimator,
    IOptions<AoiOptions> aoiOptions) : IRetrievalService
{
    private const int CandidatePoolMultiplier = 4;
    private const int MinimumCandidatePoolSize = 20;

    public async Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default)
    {
        // TASK-OBS-02: wraps the actual pipeline (RetrieveCoreAsync) so
        // retrieval.duration/retrieval.total/retrieval.empty are recorded
        // for every call regardless of which of the pipeline's several
        // return points produced the result.
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await RetrieveCoreAsync(request, cancellationToken);
        stopwatch.Stop();

        AppMetrics.RetrievalDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds);
        AppMetrics.RetrievalTotal.Add(1);
        if (result.NoEvidence)
        {
            AppMetrics.RetrievalEmpty.Add(1);
        }

        return result;
    }

    private async Task<RetrievalResult> RetrieveCoreAsync(RetrievalRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var aoi = aoiOptions.Value;

        await EnsureEmbeddingConfigMatchesAsync(request.ApplicationId, aoi, cancellationToken);

        var allFiles = await fileRepository.ListAsync(request.ApplicationId, cancellationToken: cancellationToken);
        var filesById = allFiles.ToDictionary(f => f.Id, f => f);

        var requestedFileIds = request.SelectedFileIds ?? [.. allFiles.Select(f => f.Id)];
        var excludedFileIds = requestedFileIds
            .Where(id => !filesById.TryGetValue(id, out var f) || f.EffectiveStatus != UserFacingFileStatus.Ready || !f.IsEnabled)
            .ToList();

        var candidatePoolSize = Math.Max(request.Settings.TopK * CandidatePoolMultiplier, MinimumCandidatePoolSize);

        // RET-01: embed the query with the same deployment as the corpus - never the whole agent prompt (§15.3).
        Vector queryVector;
        using (AppTracing.Source.StartActivity("Create query embedding"))
        {
            var queryEmbeddings = await embeddingService.GenerateEmbeddingsAsync([request.Query], aoi.EmbeddingDeployment, cancellationToken);
            queryVector = new Vector(queryEmbeddings[0]);
        }

        IReadOnlyList<KnowledgeChunkSearchResult> semanticResults;
        using (AppTracing.Source.StartActivity("Semantic search"))
        {
            semanticResults = await chunkRepository.SearchByCosineSimilarityAsync(request.ApplicationId, queryVector, candidatePoolSize, cancellationToken);
        }

        IReadOnlyList<KnowledgeChunkKeywordSearchResult> keywordResults;
        using (AppTracing.Source.StartActivity("Keyword search"))
        {
            keywordResults = request.Settings.Mode == RetrievalMode.Hybrid
                ? await chunkRepository.SearchByFullTextAsync(request.ApplicationId, request.Query, candidatePoolSize, cancellationToken)
                : [];
        }

        if (request.SelectedFileIds is { Count: > 0 } selectedFileIds)
        {
            var selectedSet = new HashSet<Guid>(selectedFileIds);
            semanticResults = [.. semanticResults.Where(r => selectedSet.Contains(r.Chunk.FileId))];
            keywordResults = [.. keywordResults.Where(r => selectedSet.Contains(r.Chunk.FileId))];
        }

        var chunksById = new Dictionary<Guid, KnowledgeChunk>();
        foreach (var result in semanticResults)
        {
            chunksById[result.Chunk.Id] = result.Chunk;
        }

        foreach (var result in keywordResults)
        {
            chunksById[result.Chunk.Id] = result.Chunk;
        }

        if (chunksById.Count == 0)
        {
            return new RetrievalResult(NoEvidence: true, Sources: [], ExcludedFileIds: excludedFileIds);
        }

        // RET-02: cosine distance is ascending (0 = identical) - convert to an ascending-is-worse similarity score before fusing.
        List<(Guid Id, double FusedScore)> fused;
        using (AppTracing.Source.StartActivity("Rank fusion"))
        {
            var semanticScored = semanticResults.Select(r => (r.Chunk.Id, Score: 1.0 - r.Distance)).ToList();
            var keywordScored = keywordResults.Select(r => (r.Chunk.Id, r.Rank)).ToList();
            fused = [.. RankFusion.Fuse(semanticScored, keywordScored, request.Settings.SemanticWeight, request.Settings.KeywordWeight)];
        }

        var candidates = fused
            .Where(f => chunksById.ContainsKey(f.Id))
            .Select(f => ToDedupCandidate(chunksById[f.Id], f.FusedScore))
            .ToList();

        // RET-03: dedup before budgeting to maximize useful evidence.
        var deduped = EvidenceDeduplicator.Deduplicate(candidates);

        if (request.Settings.IncludeNeighbors)
        {
            var chunksByFile = new Dictionary<Guid, IReadOnlyList<EvidenceDeduplicator.FileChunk>>();
            foreach (var fileId in deduped.Select(d => d.FileId).Distinct())
            {
                var fileChunks = await chunkRepository.ListByFileAsync(fileId, activeOnly: true, cancellationToken);
                chunksByFile[fileId] = [.. fileChunks.Select(c => new EvidenceDeduplicator.FileChunk(c.Id, c.SequenceNumber, c.ContentHash, c.CharacterStart, c.CharacterEnd))];

                foreach (var chunk in fileChunks)
                {
                    chunksById.TryAdd(chunk.Id, chunk);
                }
            }

            deduped = EvidenceDeduplicator.IncludeNeighbors(deduped, chunksByFile);
        }

        // RET-04: threshold + greedy token-budget fill + SOURCE-001... ID assignment.
        var budgetCandidates = deduped
            .Where(c => chunksById.ContainsKey(c.ChunkId) && filesById.ContainsKey(c.FileId))
            .Select(c => new EvidenceBudgetPlanner.CandidateForBudget(
                c.FileId,
                c.ChunkId,
                filesById[c.FileId].DisplayName,
                chunksById[c.ChunkId].HeadingPath,
                chunksById[c.ChunkId].Content,
                chunksById[c.ChunkId].CharacterStart,
                chunksById[c.ChunkId].CharacterEnd,
                c.Score))
            .ToList();

        var sources = EvidenceBudgetPlanner.Plan(
            budgetCandidates,
            request.Settings.MinimumSimilarity,
            request.Settings.EvidenceTokenBudget,
            content => tokenEstimator.EstimateTokenCount(content, aoi.ChatDeployment));

        // RET-05: nothing qualified above the threshold/within budget -> controlled no-evidence outcome.
        return sources.Count == 0
            ? new RetrievalResult(NoEvidence: true, Sources: [], ExcludedFileIds: excludedFileIds)
            : new RetrievalResult(NoEvidence: false, Sources: sources, ExcludedFileIds: excludedFileIds);
    }

    private static EvidenceDeduplicator.Candidate ToDedupCandidate(KnowledgeChunk chunk, double score) =>
        new(chunk.Id, chunk.FileId, chunk.SequenceNumber, chunk.ContentHash, chunk.CharacterStart, chunk.CharacterEnd, score);

    /// <summary>TASK-RET-01/D10: rejects retrieval if the corpus embedding config differs from the currently-configured deployment.</summary>
    private async Task EnsureEmbeddingConfigMatchesAsync(Guid applicationId, AoiOptions aoi, CancellationToken cancellationToken)
    {
        var corpusDeployments = await chunkRepository.ListDistinctEmbeddingDeploymentsAsync(applicationId, cancellationToken);

        if (corpusDeployments.Count == 0)
        {
            return;
        }

        if (corpusDeployments.Count > 1 || !corpusDeployments.Contains(aoi.EmbeddingDeployment))
        {
            throw new RetrievalConfigurationException(
                $"Embedding configuration mismatch for application '{applicationId}': corpus chunks were embedded with " +
                $"[{string.Join(", ", corpusDeployments)}], but retrieval is currently configured to use '{aoi.EmbeddingDeployment}'.");
        }
    }
}
