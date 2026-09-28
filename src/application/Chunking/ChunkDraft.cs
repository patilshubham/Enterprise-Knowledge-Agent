namespace EnterpriseKnowledgeAgent.Application.Chunking;

/// <summary>TASK-CHK-01: an in-memory chunk before persistence (sequence number and content hash are assigned in TASK-CHK-02).</summary>
public sealed record ChunkDraft(string HeadingPath, string Content, int TokenCount, int CharacterStart, int CharacterEnd);
