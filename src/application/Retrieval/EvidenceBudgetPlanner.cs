namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-04: applies the similarity threshold and greedily fills the
/// evidence-token budget, assigning stable <c>SOURCE-001</c>-style IDs in
/// final order. Pure/static so it is directly unit testable without a
/// repository, tokenizer, or database.
/// </summary>
public static class EvidenceBudgetPlanner
{
    /// <summary>A candidate carrying just the fields needed to build an <see cref="EvidenceSource"/>.</summary>
    public sealed record CandidateForBudget(Guid FileId, Guid ChunkId, string DisplayName, string HeadingPath, string Content, int CharacterStart, int CharacterEnd, double Score);

    /// <summary>
    /// Filters out candidates below <paramref name="minimumSimilarity"/>, then
    /// greedily walks <paramref name="orderedCandidates"/> (assumed already
    /// ordered best-first) adding each one that still fits within
    /// <paramref name="tokenBudget"/> - a candidate that doesn't fit is
    /// skipped (not a hard stop) so a later, smaller candidate can still be
    /// included, maximizing evidence used within the budget. Assigns
    /// <c>SOURCE-001</c>, <c>SOURCE-002</c>, ... in the order sources are
    /// actually included.
    /// </summary>
    public static IReadOnlyList<EvidenceSource> Plan(
        IReadOnlyList<CandidateForBudget> orderedCandidates,
        double minimumSimilarity,
        int tokenBudget,
        Func<string, int> estimateTokens)
    {
        ArgumentNullException.ThrowIfNull(orderedCandidates);
        ArgumentNullException.ThrowIfNull(estimateTokens);

        var sources = new List<EvidenceSource>();
        var usedTokens = 0;

        foreach (var candidate in orderedCandidates)
        {
            if (candidate.Score < minimumSimilarity)
            {
                continue;
            }

            var tokenCount = estimateTokens(candidate.Content);
            if (usedTokens + tokenCount > tokenBudget)
            {
                continue;
            }

            sources.Add(new EvidenceSource(
                $"SOURCE-{sources.Count + 1:000}",
                candidate.FileId,
                candidate.ChunkId,
                candidate.DisplayName,
                candidate.HeadingPath,
                candidate.Content,
                candidate.CharacterStart,
                candidate.CharacterEnd,
                tokenCount,
                candidate.Score));

            usedTokens += tokenCount;
        }

        return sources;
    }
}
