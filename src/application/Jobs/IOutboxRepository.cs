using EnterpriseKnowledgeAgent.Domain.Operations;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>TASK-JOB-04: persistence for the transactional outbox.</summary>
public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<OutboxMessage?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Oldest-first batch of undelivered rows for the publisher to attempt sending (TASK-JOB-04).</summary>
    Task<IReadOnlyList<OutboxMessage>> ListUndeliveredAsync(int batchSize, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
