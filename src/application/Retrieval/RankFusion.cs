namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-02: combines two independently-scored candidate lists (semantic
/// cosine-similarity and PostgreSQL FTS rank) into a single ordered list via
/// min-max normalization + weighted sum. Pure/static so it is directly unit
/// testable without any repository or database dependency.
/// </summary>
public static class RankFusion
{
    /// <summary>
    /// Fuses two candidate score lists. Each list's scores are normalized
    /// independently to the 0..1 range (min-max) before weighting, so the two
    /// scoring scales (cosine similarity vs. <c>ts_rank</c>) never need to be
    /// directly comparable. An id present in only one list is treated as 0
    /// for the other list's contribution. Both input lists must use
    /// "higher is better" scores.
    /// </summary>
    /// <param name="semantic">Semantic-search candidates as (chunk id, similarity score).</param>
    /// <param name="keyword">Keyword/FTS candidates as (chunk id, rank score). May be empty (semantic-only mode).</param>
    /// <param name="semanticWeight">Weight applied to the normalized semantic score.</param>
    /// <param name="keywordWeight">Weight applied to the normalized keyword score.</param>
    /// <returns>All distinct ids from both lists, ordered by fused score descending.</returns>
    public static IReadOnlyList<(Guid Id, double FusedScore)> Fuse(
        IReadOnlyList<(Guid Id, double Score)> semantic,
        IReadOnlyList<(Guid Id, double Score)> keyword,
        double semanticWeight,
        double keywordWeight)
    {
        ArgumentNullException.ThrowIfNull(semantic);
        ArgumentNullException.ThrowIfNull(keyword);

        var normalizedSemantic = Normalize(semantic);
        var normalizedKeyword = Normalize(keyword);

        var ids = new HashSet<Guid>(normalizedSemantic.Keys);
        ids.UnionWith(normalizedKeyword.Keys);

        return ids
            .Select(id => (
                Id: id,
                FusedScore: (semanticWeight * normalizedSemantic.GetValueOrDefault(id, 0.0))
                    + (keywordWeight * normalizedKeyword.GetValueOrDefault(id, 0.0))))
            .OrderByDescending(x => x.FusedScore)
            .ToList();
    }

    private static Dictionary<Guid, double> Normalize(IReadOnlyList<(Guid Id, double Score)> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var min = items.Min(i => i.Score);
        var max = items.Max(i => i.Score);

        // All candidates scored identically (or there's only a single
        // candidate) - normalizing by range would divide by zero. Rather
        // than collapsing every candidate to an arbitrary constant (which
        // would destroy the score's absolute meaning and defeat
        // TASK-RET-04's MinimumSimilarity threshold whenever a small corpus
        // yields only one or two candidates), fall back to the raw score
        // clamped to 0..1 so a genuinely weak single candidate still reads
        // as weak.
        if (max - min < 1e-9)
        {
            return items.ToDictionary(i => i.Id, i => Math.Clamp(i.Score, 0.0, 1.0));
        }

        return items.ToDictionary(i => i.Id, i => (i.Score - min) / (max - min));
    }
}
