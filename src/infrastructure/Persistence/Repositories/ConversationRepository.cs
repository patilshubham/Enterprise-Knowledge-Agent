using EnterpriseKnowledgeAgent.Application.Chat;
using EnterpriseKnowledgeAgent.Domain.Conversations;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>Module CHAT: EF Core-backed <see cref="IConversationRepository"/>.</summary>
public sealed class ConversationRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IConversationRepository
{
    public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) =>
        dbContext.Conversations.AddAsync(conversation, cancellationToken).AsTask();

    public Task<Conversation?> GetAsync(Guid applicationId, Guid conversationId, CancellationToken cancellationToken = default) =>
        dbContext.Conversations
            .Include(c => c.Messages)
            .SingleOrDefaultAsync(
                c => c.Id == conversationId && c.ApplicationId == applicationId && !c.IsDeleted,
                cancellationToken);

    public async Task<IReadOnlyList<ConversationListItem>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        await dbContext.Conversations
            .AsNoTracking()
            .Where(c => c.ApplicationId == applicationId && !c.IsDeleted)
            .OrderByDescending(c => c.LastMessageAt)
            .Select(c => new ConversationListItem(
                c.Id,
                c.Title,
                c.CreatedBy,
                c.CreatedAt,
                c.LastMessageAt,
                c.Messages.Count))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ConversationMessageEvidence>> GetEvidenceAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        await dbContext.ConversationMessageEvidences
            .AsNoTracking()
            .Where(e => e.ConversationId == conversationId)
            .ToListAsync(cancellationToken);

    public Task AddEvidenceAsync(IReadOnlyList<ConversationMessageEvidence> evidence, CancellationToken cancellationToken = default) =>
        dbContext.ConversationMessageEvidences.AddRangeAsync(evidence, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
