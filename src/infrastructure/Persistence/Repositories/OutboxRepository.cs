using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-JOB-04: EF Core-backed <see cref="IOutboxRepository"/>.</summary>
public sealed class OutboxRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IOutboxRepository
{
    public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
        dbContext.OutboxMessages.AddAsync(message, cancellationToken).AsTask();

    public Task<OutboxMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.OutboxMessages.SingleOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> ListUndeliveredAsync(int batchSize, CancellationToken cancellationToken = default) =>
        await dbContext.OutboxMessages
            .Where(m => !m.Delivered)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
