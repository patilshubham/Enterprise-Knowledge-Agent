namespace EnterpriseKnowledgeAgent.Application.Tokenization;

/// <summary>
/// Estimates model token counts for text (used by chunking - module 11 -
/// and context budgeting - module 20). TASK-AOI-04 extended this with an
/// optional <paramref name="deployment"/> hint so a future tokenizer-accurate
/// implementation can select the right encoding per model; no real tokenizer
/// library is available in this codebase's package set, so the registered
/// implementation remains the conservative character-based heuristic even
/// after this change - only the interface shape is deployment-aware.
/// </summary>
public interface ITokenEstimator
{
    int EstimateTokenCount(string text, string? deployment = null);
}
