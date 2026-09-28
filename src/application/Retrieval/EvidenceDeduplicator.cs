namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-03: suppresses duplicate/heavily-overlapping candidate chunks and
/// optionally adds neighboring chunks for context. Pure/static so both
/// operations are directly unit testable without a repository or database.
/// </summary>
public static class EvidenceDeduplicator
{
    /// <summary>A candidate chunk carrying just the fields dedup/neighbor logic needs.</summary>
    public sealed record Candidate(
        Guid ChunkId,
        Guid FileId,
        int SequenceNumber,
        string ContentHash,
        int CharacterStart,
        int CharacterEnd,
        double Score);

    /// <summary>
    /// Removes redundant candidates in two passes: (1) exact-duplicate content
    /// (same <see cref="Candidate.ContentHash"/>, anywhere in the application)
    /// keeps only the highest-scored occurrence; (2) within the same file,
    /// a lower-scored candidate whose character range overlaps a
    /// higher-scored, already-kept candidate is suppressed.
    /// </summary>
    /// <returns>The surviving candidates, ordered by score descending.</returns>
    public static IReadOnlyList<Candidate> Deduplicate(IReadOnlyList<Candidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var byHash = candidates
            .GroupBy(c => c.ContentHash)
            .Select(g => g.OrderByDescending(c => c.Score).First());

        var kept = new List<Candidate>();
        foreach (var group in byHash.GroupBy(c => c.FileId))
        {
            var orderedInFile = group.OrderByDescending(c => c.Score).ToList();
            var keptInFile = new List<Candidate>();

            foreach (var candidate in orderedInFile)
            {
                if (!keptInFile.Any(k => Overlaps(k, candidate)))
                {
                    keptInFile.Add(candidate);
                }
            }

            kept.AddRange(keptInFile);
        }

        return kept.OrderByDescending(c => c.Score).ToList();
    }

    private static bool Overlaps(Candidate a, Candidate b) =>
        a.CharacterStart < b.CharacterEnd && b.CharacterStart < a.CharacterEnd;

    /// <summary>Everything <see cref="IncludeNeighbors"/> needs to know about a chunk that could become a neighbor.</summary>
    public sealed record FileChunk(Guid ChunkId, int SequenceNumber, string ContentHash, int CharacterStart, int CharacterEnd);

    /// <summary>
    /// TASK-RET-03: for each selected candidate, adds its immediate neighbors
    /// (<c>sequence - 1</c> and <c>sequence + 1</c>, same file) from
    /// <paramref name="chunksByFile"/> when present and not already selected.
    /// Neighbors inherit their parent candidate's score so they still
    /// participate correctly in threshold/budget decisions downstream
    /// (TASK-RET-04) - they are never scored independently since they were
    /// added for context, not because they matched the query themselves.
    /// Neighbors are only ever drawn from the same file, so app/file
    /// boundaries are never crossed.
    /// </summary>
    public static IReadOnlyList<Candidate> IncludeNeighbors(
        IReadOnlyList<Candidate> selected,
        IReadOnlyDictionary<Guid, IReadOnlyList<FileChunk>> chunksByFile)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(chunksByFile);

        var existingKeys = new HashSet<(Guid FileId, int SequenceNumber)>(
            selected.Select(c => (c.FileId, c.SequenceNumber)));

        var additions = new List<Candidate>();

        foreach (var candidate in selected)
        {
            if (!chunksByFile.TryGetValue(candidate.FileId, out var fileChunks))
            {
                continue;
            }

            foreach (var neighborSequence in new[] { candidate.SequenceNumber - 1, candidate.SequenceNumber + 1 })
            {
                var key = (candidate.FileId, neighborSequence);
                if (!existingKeys.Add(key))
                {
                    continue;
                }

                var neighbor = fileChunks.FirstOrDefault(c => c.SequenceNumber == neighborSequence);
                if (neighbor is null)
                {
                    continue;
                }

                additions.Add(new Candidate(
                    neighbor.ChunkId,
                    candidate.FileId,
                    neighbor.SequenceNumber,
                    neighbor.ContentHash,
                    neighbor.CharacterStart,
                    neighbor.CharacterEnd,
                    candidate.Score));
            }
        }

        return selected.Concat(additions).OrderByDescending(c => c.Score).ToList();
    }
}
