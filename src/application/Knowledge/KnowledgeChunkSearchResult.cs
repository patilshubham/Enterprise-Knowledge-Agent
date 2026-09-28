using EnterpriseKnowledgeAgent.Domain.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-PG-06: one ranked hit from a cosine-similarity search - the chunk plus its cosine distance to the query embedding (0 = identical, 2 = opposite).</summary>
public sealed record KnowledgeChunkSearchResult(KnowledgeChunk Chunk, double Distance);
