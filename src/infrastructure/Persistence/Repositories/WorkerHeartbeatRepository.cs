using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Operations;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-JOB-06: EF Core-backed <see cref="IWorkerHeartbeatRepository"/>.</summary>
public sealed class WorkerHeartbeatRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IWorkerHeartbeatRepository
{
    public async Task UpsertAsync(string workerId, string workerKind, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.WorkerHeartbeats.SingleOrDefaultAsync(h => h.WorkerId == workerId, cancellationToken);
        if (existing is null)
        {
            await dbContext.WorkerHeartbeats.AddAsync(WorkerHeartbeat.Create(workerId, workerKind, now), cancellationToken);
        }
        else
        {
            existing.Touch(now);
        }

        // Each worker process uses a fresh worker id, so restarts/redeploys
        // leave behind heartbeat rows for prior instances that never clear.
        // Prune stale same-kind orphans here (a stale same-kind row is a dead
        // instance) so they don't accumulate unbounded and clutter the
        // operations dashboard. The current instance is excluded by id.
        var staleCutoff = now - WorkerHeartbeat.StaleThreshold;
        var orphans = await dbContext.WorkerHeartbeats
            .Where(h => h.WorkerKind == workerKind && h.WorkerId != workerId && h.LastHeartbeatAt < staleCutoff)
            .ToListAsync(cancellationToken);
        if (orphans.Count > 0)
        {
            dbContext.WorkerHeartbeats.RemoveRange(orphans);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkerHeartbeat>> ListAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.WorkerHeartbeats.OrderBy(h => h.WorkerId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkerHeartbeat>> ListStaleAsync(DateTimeOffset threshold, CancellationToken cancellationToken = default) =>
        await dbContext.WorkerHeartbeats.Where(h => h.LastHeartbeatAt < threshold).ToListAsync(cancellationToken);
}
