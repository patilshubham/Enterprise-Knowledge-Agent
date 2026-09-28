using EnterpriseKnowledgeAgent.Domain.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-RET-02/TASK-PG-04: one ranked hit from a full-text-search query - the chunk plus its Postgres <c>ts_rank</c> score (higher = better keyword match).</summary>
public sealed record KnowledgeChunkKeywordSearchResult(KnowledgeChunk Chunk, double Rank);
