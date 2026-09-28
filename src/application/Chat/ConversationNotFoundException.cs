namespace EnterpriseKnowledgeAgent.Application.Chat;

/// <summary>Module CHAT: thrown when a conversation does not exist (or is soft-deleted) in the target application. The API maps it to HTTP 404.</summary>
public sealed class ConversationNotFoundException : Exception
{
    public ConversationNotFoundException(Guid conversationId)
        : base($"Conversation '{conversationId}' was not found.")
    {
    }
}
