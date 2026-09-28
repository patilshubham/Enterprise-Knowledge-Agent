using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-JOB-01/02/03: EF Core-backed <see cref="IAgentRunRepository"/> (mirrors <see cref="ProcessingJobRepository"/>'s atomic claim/heartbeat pattern).</summary>
public sealed class AgentRunRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IAgentRunRepository
{
    public Task AddAsync(AgentRun run, CancellationToken cancellationToken = default) =>
        dbContext.AgentRuns.AddAsync(run, cancellationToken).AsTask();

    public Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default) =>
        dbContext.AgentRuns.SingleOrDefaultAsync(r => r.Id == runId, cancellationToken);

    public Task<AgentRun?> GetByIdempotencyKeyAsync(Guid agentId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        dbContext.AgentRuns.SingleOrDefaultAsync(r => r.AgentId == agentId && r.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task<bool> TryClaimAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var leaseExpiresAt = now + leaseDuration;

        var rows = await dbContext.AgentRuns
            .Where(r => r.Id == runId && r.Status == JobStatus.Queued)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Status, JobStatus.Running)
                    .SetProperty(r => r.Stage, RunStage.Preparing)
                    .SetProperty(r => r.WorkerId, workerId)
                    .SetProperty(r => r.StartedAt, now)
                    .SetProperty(r => r.LeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(r => r.AttemptNumber, r => r.AttemptNumber + 1)
                    .SetProperty(r => r.HeartbeatAt, now),
                cancellationToken);

        // ExecuteUpdateAsync writes directly to the database and does not
        // touch this DbContext's change tracker, so any instance of this run
        // already tracked from an earlier query in the same scope keeps
        // stale in-memory property values (Status still Queued, etc). Clear
        // the tracker so subsequent reads in this scope re-fetch the fresh
        // row instead of returning that stale cached instance (mirrors the
        // identical fix in ProcessingJobRepository.TryClaimAsync).
        if (rows == 1)
        {
            dbContext.ChangeTracker.Clear();
        }

        return rows == 1;
    }

    public async Task<bool> HeartbeatAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.AgentRuns
            .Where(r => r.Id == runId && r.WorkerId == workerId && r.Status == JobStatus.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.HeartbeatAt, now)
                    .SetProperty(r => r.LeaseExpiresAt, now + leaseExtension),
                cancellationToken);

        // See the comment in TryClaimAsync - ExecuteUpdateAsync bypasses the change tracker.
        if (rows == 1)
        {
            dbContext.ChangeTracker.Clear();
        }

        return rows == 1;
    }

    public async Task<IReadOnlyList<AgentRun>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await dbContext.AgentRuns
            .Where(r => r.Status == JobStatus.Running && r.LeaseExpiresAt != null && r.LeaseExpiresAt < now)
            .ToListAsync(cancellationToken);

    public Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default) =>
        dbContext.AgentRuns
            .AsNoTracking()
            .Where(r => r.Id == runId)
            .Select(r => r.CancellationRequested)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AgentRun>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default)
    {
        var undeliveredOutboxIds = dbContext.OutboxMessages.Where(o => !o.Delivered).Select(o => o.Id);

        return await dbContext.AgentRuns
            .Where(r => r.Status == JobStatus.Queued
                && r.CreatedAt < createdBefore
                && r.OutboxMessageId != null
                && undeliveredOutboxIds.Contains(r.OutboxMessageId!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AgentRun>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        await dbContext.AgentRuns
            .Where(r => r.ApplicationId == applicationId && r.Status == JobStatus.Failed)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AgentRuns.AsNoTracking().AsQueryable();
        if (applicationId is { } appId)
        {
            query = query.Where(r => r.ApplicationId == appId);
        }

        var counts = await query
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public async Task<IReadOnlyList<AgentRun>> ListAsync(
        Guid applicationId,
        JobStatus? status = null,
        Guid? agentId = null,
        string? createdBy = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AgentRuns.Where(r => r.ApplicationId == applicationId);

        if (status is not null)
        {
            query = query.Where(r => r.Status == status);
        }

        if (agentId is not null)
        {
            query = query.Where(r => r.AgentId == agentId);
        }

        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            query = query.Where(r => r.RequestedBy == createdBy);
        }

        if (from is not null)
        {
            query = query.Where(r => r.CreatedAt >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(r => r.CreatedAt <= to.Value);
        }

        return await query.OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
