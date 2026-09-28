namespace EnterpriseKnowledgeAgent.Domain.Citations;

/// <summary>
/// TASK-CIT-01: a durable record of one <c>SOURCE-NNN</c> evidence entry
/// supplied to the model for a specific <see cref="AgentRunId"/> (ASDD §17.1).
/// Snapshots the originating chunk/file's filename, heading path, passage,
/// and source-document offsets at retrieval time so citations remain
/// displayable even after the source file or chunk is later deleted
/// (FR-CIT-004) - <see cref="KnowledgeChunkId"/>/<see cref="KnowledgeFileId"/>
/// are unenforced references (no FK), not navigable guarantees.
/// </summary>
public sealed class RunEvidence
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid AgentRunId { get; private set; }

    /// <summary>The internal source identifier shown to the model, e.g. "SOURCE-001" (ASDD §15.6).</summary>
    public string SourceId { get; private set; } = string.Empty;

    /// <summary>The chunk this evidence was retrieved from - reference only, may point at a later-deleted row.</summary>
    public Guid KnowledgeChunkId { get; private set; }

    /// <summary>The file this evidence was retrieved from - reference only, may point at a later-deleted row.</summary>
    public Guid KnowledgeFileId { get; private set; }

    public string FileNameSnapshot { get; private set; } = string.Empty;

    public string HeadingPathSnapshot { get; private set; } = string.Empty;

    public string PassageSnapshot { get; private set; } = string.Empty;

    /// <summary>Source-document character offset (inclusive) the passage started at - supports surrounding-text display (module 22, CIT-04).</summary>
    public int CharacterStartSnapshot { get; private set; }

    /// <summary>Source-document character offset (exclusive) the passage ended at.</summary>
    public int CharacterEndSnapshot { get; private set; }

    /// <summary>Snapshot of the source file's last-processed time, for the citation drawer's "processing time" (ASDD §17.4).</summary>
    public DateTimeOffset? ProcessedAtSnapshot { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private RunEvidence()
    {
    }
#pragma warning restore CS8618

    public static RunEvidence Create(
        Guid applicationId,
        Guid agentRunId,
        string sourceId,
        Guid knowledgeChunkId,
        Guid knowledgeFileId,
        string fileNameSnapshot,
        string headingPathSnapshot,
        string passageSnapshot,
        int characterStartSnapshot,
        int characterEndSnapshot,
        DateTimeOffset? processedAtSnapshot,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            AgentRunId = agentRunId,
            SourceId = sourceId,
            KnowledgeChunkId = knowledgeChunkId,
            KnowledgeFileId = knowledgeFileId,
            FileNameSnapshot = fileNameSnapshot,
            HeadingPathSnapshot = headingPathSnapshot,
            PassageSnapshot = passageSnapshot,
            CharacterStartSnapshot = characterStartSnapshot,
            CharacterEndSnapshot = characterEndSnapshot,
            ProcessedAtSnapshot = processedAtSnapshot,
            CreatedAt = now,
        };
}
