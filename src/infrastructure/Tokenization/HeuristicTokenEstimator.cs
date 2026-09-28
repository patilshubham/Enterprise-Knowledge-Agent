namespace EnterpriseKnowledgeAgent.Infrastructure.Tokenization;

/// <summary>
/// Conservative character-based token-count heuristic (~4 characters per
/// token, the commonly cited average for English text under BPE tokenizers
/// such as cl100k_base). Registered as the default <c>ITokenEstimator</c>;
/// TASK-AOI-04 added the optional <c>deployment</c> parameter to the
/// interface, but no real tokenizer library is available in this codebase's
/// package set, so this heuristic ignores it and always estimates the same
/// way regardless of which deployment is asked about.
/// </summary>
public sealed class HeuristicTokenEstimator : EnterpriseKnowledgeAgent.Application.Tokenization.ITokenEstimator
{
    public int EstimateTokenCount(string text, string? deployment = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4.0));
    }
}
