using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>
/// TASK-JOB-01/02/03: persistence for <see cref="ProcessingJob"/>. The claim
/// and heartbeat operations are atomic conditional updates issued directly
/// against the table (not entity-load + SaveChanges) - see
/// <see cref="ProcessingJob"/>'s remarks for why this matters under
/// concurrent workers.
/// </summary>
public interface IProcessingJobRepository
{
    Task AddAsync(ProcessingJob job, CancellationToken cancellationToken = default);

    Task<ProcessingJob?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>TASK-KNW-04: the most recently created job for a file (used to surface live stage/progress on the file listing).</summary>
    Task<ProcessingJob?> GetLatestByFileAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>Atomically transitions Queued→Running for <paramref name="jobId"/>. Returns <see langword="false"/> if it was not Queued (already claimed/completed - safe no-op for duplicate delivery).</summary>
    Task<bool> TryClaimAsync(Guid jobId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>Atomically extends the lease for a job still owned/Running under <paramref name="workerId"/>. Returns <see langword="false"/> if the job is no longer owned by that worker (e.g. its lease already expired and was reclaimed).</summary>
    Task<bool> HeartbeatAsync(Guid jobId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default);

    /// <summary>TASK-JOB-06: jobs whose lease has expired while still Running (crashed/stalled worker).</summary>
    Task<IReadOnlyList<ProcessingJob>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>TASK-JOB-05: Queued jobs older than <paramref name="createdBefore"/> whose own outbox row is still undelivered.</summary>
    Task<IReadOnlyList<ProcessingJob>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default);

    /// <summary>TASK-JOB-06: Failed jobs eligible for manual retry.</summary>
    Task<IReadOnlyList<ProcessingJob>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>TASK-PDSH-01/TASK-ADSH-01: single aggregate query returning job counts per <see cref="JobStatus"/>, optionally scoped to one application (<paramref name="applicationId"/> null = platform-wide). Statuses with zero jobs are simply absent from the result.</summary>
    Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK-PRDSH-01: the processing dashboard listing - newest first,
    /// unpaged (mirrors <c>IGeneratedOutputRepository.ListAsync</c>'s
    /// convention of leaving pagination to the frontend's table component).
    /// <paramref name="applicationId"/> null = platform-wide (TASK-PRDSH-01);
    /// non-null = scoped to one application (TASK-PRDSH-02).
    /// </summary>
    Task<IReadOnlyList<ProcessingJob>> ListAsync(
        Guid? applicationId,
        JobStatus? status = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
