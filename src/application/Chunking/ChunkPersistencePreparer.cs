using System.Security.Cryptography;
using System.Text;
using EnterpriseKnowledgeAgent.Domain.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Chunking;

/// <summary>
/// TASK-CHK-02: assigns sequence numbers and content hashes to chunk drafts
/// and materializes them as inactive <see cref="KnowledgeChunk"/> entities
/// ready for persistence (ASDD §13.3 - activated later once embeddings are
/// validated, module 13).
/// </summary>
public static class ChunkPersistencePreparer
{
    public static IReadOnlyList<KnowledgeChunk> Prepare(
        Guid applicationId, Guid fileId, IReadOnlyList<ChunkDraft> drafts, DateTimeOffset now)
    {
        var result = new List<KnowledgeChunk>(drafts.Count);
        for (var sequence = 0; sequence < drafts.Count; sequence++)
        {
            var draft = drafts[sequence];
            result.Add(KnowledgeChunk.Create(
                applicationId,
                fileId,
                sequence,
                draft.HeadingPath,
                draft.Content,
                draft.TokenCount,
                draft.CharacterStart,
                draft.CharacterEnd,
                ComputeContentHash(draft.Content),
                now));
        }

        return result;
    }

    public static string ComputeContentHash(string content) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
}
