namespace EnterpriseKnowledgeAgent.Domain.Conversations;

/// <summary>
/// Thrown when a guarded state transition on a <see cref="Conversation"/> or
/// <see cref="ConversationMessage"/> is not valid for its current state
/// (e.g. finalizing a message that is not streaming, or mutating a deleted
/// conversation). Mirrors the domain-conflict convention used by
/// <c>AgentStateConflictException</c>/<c>ApplicationStateConflictException</c>;
/// the API layer maps it to an HTTP 409.
/// </summary>
public sealed class ConversationStateConflictException : InvalidOperationException
{
    public ConversationStateConflictException(string message)
        : base(message)
    {
    }
}
