using EnterpriseKnowledgeAgent.Shared.Retrieval;

namespace EnterpriseKnowledgeAgent.Application.Chat;

/// <summary>
/// Module CHAT: the config-free defaults that drive every conversation unless
/// the user explicitly overrides them via advanced configuration. Chosen to
/// favor grounded, concise, low-cost answers over a real knowledge base.
/// </summary>
public static class ChatDefaults
{
    /// <summary>Low temperature keeps answers factual and grounded in the retrieved evidence.</summary>
    public const double Temperature = 0.2;

    /// <summary>Default number of evidence passages retrieved per question.</summary>
    public const int RetrievalCount = 8;

    /// <summary>Minimum fused similarity a passage must reach to be considered relevant.</summary>
    public const double MinimumSimilarity = 0.30;

    public const bool IncludeNeighbors = true;

    /// <summary>Chat always asks the model to cite; the pipeline surfaces citations but does not hard-fail a turn that lacks them.</summary>
    public const bool CitationsRequired = true;

    /// <summary>Token budget for the evidence package supplied to the model per turn.</summary>
    public const int MaximumEvidenceTokens = 6_000;

    /// <summary>Default cap on the generated answer length (keeps latency and cost bounded).</summary>
    public const int MaxOutputTokens = 800;

    /// <summary>How many prior messages are replayed to the model as short-term conversational memory.</summary>
    public const int HistoryWindowMessages = 8;

    /// <summary>Rank-fusion weights used when the conversation runs in hybrid mode (mirrors the agent-run pipeline).</summary>
    public const double SemanticWeight = 0.6;
    public const double KeywordWeight = 0.4;

    public const RetrievalMode DefaultRetrievalMode = RetrievalMode.Semantic;

    /// <summary>The platform's default assistant persona, used when the conversation has no custom system prompt.</summary>
    public const string DefaultSystemPrompt =
        """
        You are a helpful enterprise knowledge assistant. Answer the user's questions using only the evidence retrieved from this application's knowledge base. Be clear, concise, and accurate, and cite the supporting source for each claim. If the retrieved evidence does not contain the answer, say that you don't have that information rather than guessing.
        """;

    /// <summary>Friendly message shown as the assistant's answer when retrieval finds no usable evidence.</summary>
    public const string NoEvidenceMessage =
        "I couldn't find anything in this application's knowledge base to answer that. Try rephrasing your question, or ask about a topic covered by the uploaded documents.";
}
