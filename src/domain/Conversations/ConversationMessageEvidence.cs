namespace EnterpriseKnowledgeAgent.Domain.Conversations;

/// <summary>
/// A durable snapshot of one <c>SOURCE-NNN</c> evidence entry retrieved for a
/// specific assistant <see cref="ConversationMessage"/> (Module CHAT). Mirrors
/// <c>RunEvidence</c>: the filename, heading path, passage, and source-document
/// offsets are snapshotted at retrieval time so a chat answer's citations stay
/// displayable even after the underlying file or chunk is later deleted.
/// <see cref="KnowledgeChunkId"/>/<see cref="KnowledgeFileId"/> are unenforced
/// references (no FK), not navigable guarantees.
/// </summary>
public sealed class ConversationMessageEvidence
{
    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public Guid ConversationMessageId { get; private set; }

    /// <summary>The internal source identifier shown to the model, e.g. "SOURCE-001".</summary>
    public string SourceId { get; private set; } = string.Empty;

    /// <summary>The chunk this evidence was retrieved from - reference only, may point at a later-deleted row.</summary>
    public Guid KnowledgeChunkId { get; private set; }

    /// <summary>The file this evidence was retrieved from - reference only, may point at a later-deleted row.</summary>
    public Guid KnowledgeFileId { get; private set; }

    public string FileNameSnapshot { get; private set; } = string.Empty;

    public string HeadingPathSnapshot { get; private set; } = string.Empty;

    public string PassageSnapshot { get; private set; } = string.Empty;

    /// <summary>Source-document character offset (inclusive) the passage started at.</summary>
    public int CharacterStartSnapshot { get; private set; }

    /// <summary>Source-document character offset (exclusive) the passage ended at.</summary>
    public int CharacterEndSnapshot { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private ConversationMessageEvidence()
    {
    }
#pragma warning restore CS8618

    public static ConversationMessageEvidence Create(
        Guid conversationId,
        Guid conversationMessageId,
        string sourceId,
        Guid knowledgeChunkId,
        Guid knowledgeFileId,
        string fileNameSnapshot,
        string headingPathSnapshot,
        string passageSnapshot,
        int characterStartSnapshot,
        int characterEndSnapshot,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        return new ConversationMessageEvidence
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            ConversationMessageId = conversationMessageId,
            SourceId = sourceId,
            KnowledgeChunkId = knowledgeChunkId,
            KnowledgeFileId = knowledgeFileId,
            FileNameSnapshot = fileNameSnapshot,
            HeadingPathSnapshot = headingPathSnapshot,
            PassageSnapshot = passageSnapshot,
            CharacterStartSnapshot = characterStartSnapshot,
            CharacterEndSnapshot = characterEndSnapshot,
            CreatedAt = now,
        };
    }
}
