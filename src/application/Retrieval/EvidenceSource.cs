namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-04: one item of the evidence package assembled for a run. Bound
/// to the run by <see cref="SourceId"/> (module 22 validates that any source
/// ID the model cites in a generated answer actually appears in the run's
/// evidence package).
/// </summary>
/// <param name="SourceId">Stable, run-scoped identifier in the form <c>SOURCE-001</c>, <c>SOURCE-002</c>, ... assigned in final evidence order.</param>
/// <param name="FileId">The knowledge file this passage came from.</param>
/// <param name="ChunkId">The knowledge chunk this passage came from (TASK-CIT-02 snapshots this onto <c>RunEvidence</c>).</param>
/// <param name="DisplayName">The file's display name, for citation UI.</param>
/// <param name="HeadingPath">The chunk's heading path (e.g. <c>"Chapter 2 &gt; Section 2.1"</c>), for citation UI.</param>
/// <param name="Content">The passage text included in the model prompt.</param>
/// <param name="CharacterStart">Source-document character offset (inclusive) the chunk started at (TASK-CIT-02 snapshot for surrounding-text display).</param>
/// <param name="CharacterEnd">Source-document character offset (exclusive) the chunk ended at.</param>
/// <param name="TokenCount">Estimated token count of <see cref="Content"/> (as counted against <see cref="RetrievalSettings.EvidenceTokenBudget"/>).</param>
/// <param name="Score">The candidate's final fused/similarity score (0..1, higher = more relevant) at the time it was selected.</param>
public sealed record EvidenceSource(
    string SourceId,
    Guid FileId,
    Guid ChunkId,
    string DisplayName,
    string HeadingPath,
    string Content,
    int CharacterStart,
    int CharacterEnd,
    int TokenCount,
    double Score);
