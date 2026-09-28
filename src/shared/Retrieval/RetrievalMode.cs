namespace EnterpriseKnowledgeAgent.Shared.Retrieval;

/// <summary>
/// TASK-RET-02: whether retrieval runs semantic-only or fuses semantic +
/// keyword (FTS) candidates. Lives in Shared (rather than
/// <c>Application.Retrieval</c>, where it originated) so the Domain-layer
/// <c>Agent</c> aggregate (module 17, TASK-AGT-01) can reference the same
/// enum without Domain depending on Application (layering: Application ->
/// Domain + Shared, never the reverse).
/// </summary>
public enum RetrievalMode
{
    /// <summary>Only the pgvector cosine-similarity search runs; FTS is skipped entirely.</summary>
    Semantic,

    /// <summary>Both the semantic search and PostgreSQL full-text search run; candidates are combined via rank fusion.</summary>
    Hybrid,
}
