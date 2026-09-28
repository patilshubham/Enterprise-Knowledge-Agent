namespace EnterpriseKnowledgeAgent.Shared.Chat;

/// <summary>
/// Lifecycle status of a single assistant <c>ConversationMessage</c> (Module
/// CHAT). User messages are always <see cref="Completed"/>; assistant messages
/// move from <see cref="Streaming"/> to one of the terminal states as the
/// runtime retrieval-and-generation pipeline resolves.
/// </summary>
public enum MessageStatus
{
    /// <summary>The assistant answer is being generated and streamed to the client.</summary>
    Streaming,

    /// <summary>The assistant answer finished successfully and is persisted in full.</summary>
    Completed,

    /// <summary>Retrieval returned no usable evidence from the knowledge base, so no answer was generated.</summary>
    NoResults,

    /// <summary>Generation failed (e.g. the model dependency was unavailable). See the failure category/message.</summary>
    Failed,

    /// <summary>The client disconnected (or cancelled) before generation completed; any partial answer is retained.</summary>
    Cancelled,
}
