using EnterpriseKnowledgeAgent.Application.Processing;

namespace EnterpriseKnowledgeAgent.Application.Chunking;

/// <summary>TASK-CHK-01: splits parsed blocks into token-bounded chunks.</summary>
public interface IChunker
{
    IReadOnlyList<ChunkDraft> Chunk(IReadOnlyList<ParsedBlock> blocks);
}
