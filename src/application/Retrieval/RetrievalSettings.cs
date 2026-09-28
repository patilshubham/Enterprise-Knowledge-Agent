using EnterpriseKnowledgeAgent.Shared.Retrieval;

namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-01/02/03/04/05: per-agent retrieval parameters (FR-PAR-002). All
/// values are configured on the agent (module 17+) and passed through
/// unchanged on every run - <see cref="RetrievalService"/> applies no
/// implicit defaults of its own beyond what the caller supplies here.
/// </summary>
/// <param name="TopK">Maximum number of evidence sources the final package may contain (TASK-RET-04's evidence-token-budget fill still applies on top of this).</param>
/// <param name="MinimumSimilarity">TASK-RET-04: candidates whose fused score is below this threshold (0..1, higher = more relevant) are excluded.</param>
/// <param name="SemanticWeight">TASK-RET-02: weight applied to the normalized semantic-search score during rank fusion.</param>
/// <param name="KeywordWeight">TASK-RET-02: weight applied to the normalized keyword (FTS) score during rank fusion. Ignored when <see cref="Mode"/> is <see cref="RetrievalMode.Semantic"/>.</param>
/// <param name="IncludeNeighbors">TASK-RET-03: when true, adjacent chunks (by <c>sequence±1</c> within the same file) are added to the candidate set for extra coherence.</param>
/// <param name="EvidenceTokenBudget">TASK-RET-04: maximum total tokens (estimated via <see cref="EnterpriseKnowledgeAgent.Application.Tokenization.ITokenEstimator"/>) the evidence package's source content may consume.</param>
/// <param name="StrictGrounding">TASK-RET-05: when true, the consuming execution step (module 20/21) must not generate if the run's <see cref="RetrievalResult.NoEvidence"/> is true. Retrieval itself always reports <see cref="RetrievalResult.NoEvidence"/> accurately regardless of this flag; only the downstream behavior differs.</param>
/// <param name="Mode">TASK-RET-02: semantic-only or hybrid (semantic + FTS) retrieval.</param>
public sealed record RetrievalSettings(
    int TopK,
    double MinimumSimilarity,
    double SemanticWeight,
    double KeywordWeight,
    bool IncludeNeighbors,
    int EvidenceTokenBudget,
    bool StrictGrounding,
    RetrievalMode Mode);
