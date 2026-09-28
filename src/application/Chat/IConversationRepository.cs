using EnterpriseKnowledgeAgent.Domain.Conversations;

namespace EnterpriseKnowledgeAgent.Application.Chat;

/// <summary>
/// Module CHAT: persistence gateway for the <see cref="Conversation"/>
/// aggregate and its evidence snapshots. Conversations are shared across the
/// application, so listings are not filtered by creator.
/// </summary>
public interface IConversationRepository
{
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    /// <summary>Loads a non-deleted conversation with its full message history, change-tracked for the streaming pipeline.</summary>
    Task<Conversation?> GetAsync(Guid applicationId, Guid conversationId, CancellationToken cancellationToken = default);

    /// <summary>Lists non-deleted conversations for the application, most-recently-active first.</summary>
    Task<IReadOnlyList<ConversationListItem>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>Loads all evidence snapshots for a conversation's messages (for the detail view).</summary>
    Task<IReadOnlyList<ConversationMessageEvidence>> GetEvidenceAsync(Guid conversationId, CancellationToken cancellationToken = default);

    Task AddEvidenceAsync(IReadOnlyList<ConversationMessageEvidence> evidence, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>Module CHAT: a projected conversation-list row (avoids loading full message bodies for the sidebar).</summary>
public sealed record ConversationListItem(
    Guid Id,
    string Title,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastMessageAt,
    int MessageCount);
