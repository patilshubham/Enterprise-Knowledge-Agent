using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Shared.Observability;

namespace EnterpriseKnowledgeAgent.Application.Citations;

/// <summary>
/// TASK-CIT-02: persists the evidence package a run actually used as
/// <see cref="RunEvidence"/> rows, snapshotting filename/heading/passage/
/// offsets and the source file's last-processed time so citations remain
/// displayable after the source is later edited or deleted (FR-CIT-004).
/// Idempotent per run (ASDD §12.1 at-least-once delivery): a retried worker
/// attempt for the same <paramref name="agentRunId"/> returns the
/// already-persisted rows instead of duplicating them.
/// </summary>
public sealed class RunEvidenceService(IRunEvidenceRepository runEvidenceRepository, IKnowledgeFileRepository knowledgeFileRepository)
{
    public async Task<IReadOnlyList<RunEvidence>> PersistAsync(
        Guid applicationId,
        Guid agentRunId,
        IReadOnlyList<EvidenceSource> sources,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var existing = await runEvidenceRepository.ListByRunAsync(agentRunId, cancellationToken);
        if (existing.Count > 0)
        {
            AppMetrics.EvidenceCount.Record(existing.Count);
            return existing;
        }

        var evidence = new List<RunEvidence>(sources.Count);
        foreach (var source in sources)
        {
            var file = await knowledgeFileRepository.GetAsync(source.FileId, cancellationToken);
            evidence.Add(RunEvidence.Create(
                applicationId,
                agentRunId,
                source.SourceId,
                source.ChunkId,
                source.FileId,
                file?.DisplayName ?? source.DisplayName,
                source.HeadingPath,
                source.Content,
                source.CharacterStart,
                source.CharacterEnd,
                file?.LastProcessedAt,
                now));
        }

        await runEvidenceRepository.AddRangeAsync(evidence, cancellationToken);
        AppMetrics.EvidenceCount.Record(evidence.Count);
        return evidence;
    }
}
