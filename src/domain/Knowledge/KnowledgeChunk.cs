using Pgvector;

namespace EnterpriseKnowledgeAgent.Domain.Knowledge;

/// <summary>
/// TASK-CHK-02: a single chunk of parsed content produced from a
/// <see cref="KnowledgeFile"/> (ASDD §9.2, §13.3). Written inactive
/// (<see cref="IsActive"/> = <see langword="false"/>) and only activated
/// once its embedding vector has been generated and validated (module 13,
/// TASK-EMB-03/<see cref="SetEmbedding"/>) - this "safe replacement" pattern
/// means retrieval (module 16) never sees a half-written chunk set. The
/// <c>SearchVector</c> tsvector column (TASK-PG-02) is DB-maintained (a
/// generated column over <see cref="HeadingPath"/>/<see cref="Content"/>)
/// and therefore has no corresponding CLR property here.
/// </summary>
public sealed class KnowledgeChunk
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid FileId { get; private set; }

    /// <summary>0-based position of this chunk within its file.</summary>
    public int SequenceNumber { get; private set; }

    /// <summary>Markdown heading path (e.g. "H1 &gt; H2 &gt; H3") the chunk falls under, empty for TXT/unheaded content.</summary>
    public string HeadingPath { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public int TokenCount { get; private set; }

    /// <summary>Source-document character offset (inclusive) this chunk starts at - used for citation surrounding-text (module 22).</summary>
    public int CharacterStart { get; private set; }

    /// <summary>Source-document character offset (exclusive) this chunk ends at.</summary>
    public int CharacterEnd { get; private set; }

    /// <summary>SHA-256 hash (hex) of <see cref="Content"/> - enables idempotent re-processing (FR-PRC-006): identical chunks are not re-embedded/re-inserted.</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>TASK-EMB-03: the generated embedding vector, null until module 13 populates it.</summary>
    public Vector? Embedding { get; private set; }

    /// <summary>Azure OpenAI embedding deployment name used to generate <see cref="Embedding"/> (D10: must match configured dimensions).</summary>
    public string? EmbeddingDeployment { get; private set; }

    /// <summary>Dimension count of <see cref="Embedding"/> - recorded alongside the vector so a later config change is detectable.</summary>
    public int? EmbeddingDimensions { get; private set; }

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private KnowledgeChunk()
    {
    }
#pragma warning restore CS8618

    public static KnowledgeChunk Create(
        Guid applicationId,
        Guid fileId,
        int sequenceNumber,
        string headingPath,
        string content,
        int tokenCount,
        int characterStart,
        int characterEnd,
        string contentHash,
        DateTimeOffset now) => new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            FileId = fileId,
            SequenceNumber = sequenceNumber,
            HeadingPath = headingPath,
            Content = content,
            TokenCount = tokenCount,
            CharacterStart = characterStart,
            CharacterEnd = characterEnd,
            ContentHash = contentHash,
            IsActive = false,
            CreatedAt = now,
        };

    /// <summary>TASK-CHK-02/module 13: activates the chunk once its embedding vector has been generated and validated.</summary>
    public void Activate() => IsActive = true;

    /// <summary>TASK-EMB-03: records a validated embedding vector + its provenance. Does not activate the chunk (activation is a separate, transactional step, TASK-EMB-03).</summary>
    public void SetEmbedding(Vector embedding, string deployment, int dimensions)
    {
        Embedding = embedding;
        EmbeddingDeployment = deployment;
        EmbeddingDimensions = dimensions;
    }
}
