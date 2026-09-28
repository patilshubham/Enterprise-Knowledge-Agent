using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-JOB-01/02/03: EF Core-backed <see cref="IProcessingJobRepository"/>.</summary>
public sealed class ProcessingJobRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IProcessingJobRepository
{
    public Task AddAsync(ProcessingJob job, CancellationToken cancellationToken = default) =>
        dbContext.ProcessingJobs.AddAsync(job, cancellationToken).AsTask();

    public Task<ProcessingJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        dbContext.ProcessingJobs.SingleOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    public Task<ProcessingJob?> GetLatestByFileAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.ProcessingJobs
            .Where(j => j.FileId == fileId)
            .OrderByDescending(j => j.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> TryClaimAsync(Guid jobId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var leaseExpiresAt = now + leaseDuration;

        var rows = await dbContext.ProcessingJobs
            .Where(j => j.Id == jobId && j.Status == JobStatus.Queued)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(j => j.Status, JobStatus.Running)
                    .SetProperty(j => j.Stage, ProcessingStage.Downloading)
                    .SetProperty(j => j.WorkerId, workerId)
                    .SetProperty(j => j.StartedAt, now)
                    .SetProperty(j => j.LeaseExpiresAt, leaseExpiresAt)
                    .SetProperty(j => j.AttemptNumber, j => j.AttemptNumber + 1)
                    .SetProperty(j => j.HeartbeatAt, now),
                cancellationToken);

        // ExecuteUpdateAsync writes directly to the database and does not
        // touch this DbContext's change tracker, so any instance of this job
        // already tracked from an earlier query in the same scope (e.g. the
        // GetLatestByFileAsync lookup callers use to resolve the job id
        // before claiming) keeps stale in-memory property values (Status
        // still Queued, etc). Clear the tracker so subsequent reads in this
        // scope re-fetch the fresh row instead of returning that stale
        // cached instance.
        if (rows == 1)
        {
            dbContext.ChangeTracker.Clear();
        }

        return rows == 1;
    }

    public async Task<bool> HeartbeatAsync(Guid jobId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.ProcessingJobs
            .Where(j => j.Id == jobId && j.WorkerId == workerId && j.Status == JobStatus.Running)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(j => j.HeartbeatAt, now)
                    .SetProperty(j => j.LeaseExpiresAt, now + leaseExtension),
                cancellationToken);

        // See the comment in TryClaimAsync - ExecuteUpdateAsync bypasses the change tracker.
        if (rows == 1)
        {
            dbContext.ChangeTracker.Clear();
        }

        return rows == 1;
    }

    public async Task<IReadOnlyList<ProcessingJob>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        await dbContext.ProcessingJobs
            .Where(j => j.Status == JobStatus.Running && j.LeaseExpiresAt != null && j.LeaseExpiresAt < now)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProcessingJob>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default)
    {
        var undeliveredOutboxIds = dbContext.OutboxMessages.Where(o => !o.Delivered).Select(o => o.Id);

        return await dbContext.ProcessingJobs
            .Where(j => j.Status == JobStatus.Queued
                && j.CreatedAt < createdBefore
                && j.OutboxMessageId != null
                && undeliveredOutboxIds.Contains(j.OutboxMessageId!.Value))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessingJob>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        await dbContext.ProcessingJobs
            .Where(j => j.ApplicationId == applicationId && j.Status == JobStatus.Failed)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProcessingJobs.AsNoTracking().AsQueryable();
        if (applicationId is { } appId)
        {
            query = query.Where(j => j.ApplicationId == appId);
        }

        var counts = await query
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public async Task<IReadOnlyList<ProcessingJob>> ListAsync(
        Guid? applicationId,
        JobStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProcessingJobs.AsQueryable();

        if (applicationId is { } appId)
        {
            query = query.Where(j => j.ApplicationId == appId);
        }

        if (status is not null)
        {
            query = query.Where(j => j.Status == status);
        }

        if (from is not null)
        {
            query = query.Where(j => j.CreatedAt >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(j => j.CreatedAt <= to.Value);
        }

        return await query.OrderByDescending(j => j.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
