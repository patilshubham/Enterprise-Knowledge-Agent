using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Domain.Jobs;

/// <summary>
/// TASK-JOB-01: a durable record of a document-processing job (ASDD §9,
/// §12.2). <see cref="FileId"/> references a future <c>KnowledgeFile</c>
/// (module 6, not yet built) by id only - no navigation/FK, matching the
/// existing convention of adding schema incrementally per owning module.
/// </summary>
/// <remarks>
/// The atomic Queued→Running claim transition (TASK-JOB-03) is performed by
/// <c>IProcessingJobRepository.TryClaimAsync</c> as a single conditional
/// <c>UPDATE ... WHERE Status = 'Queued'</c> issued directly against the
/// table (EF Core <c>ExecuteUpdateAsync</c>) rather than through
/// <see cref="Claim"/> + <c>SaveChanges</c> - two workers can race to claim
/// the same row from independent <c>DbContext</c> instances, and only a
/// single atomic statement (not an entity load + save) guarantees exactly
/// one of them wins. <see cref="Claim"/> exists for unit tests and any
/// single-writer code path that already holds the row.
/// </remarks>
public sealed class ProcessingJob
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid FileId { get; private set; }

    /// <summary>TASK-JOB-04/05: the outbox row announcing this job's queue message - lets the reconciler check delivery without parsing message payloads.</summary>
    public Guid? OutboxMessageId { get; private set; }

    public JobStatus Status { get; private set; }

    public ProcessingStage Stage { get; private set; }

    public int Progress { get; private set; }

    public string? WorkerId { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    /// <summary>1-based count of claim attempts for this job (FR-PRC-008).</summary>
    public int AttemptNumber { get; private set; }

    public DateTimeOffset? HeartbeatAt { get; private set; }

    public ErrorCategory? FailureCategory { get; private set; }

    public string? FailureMessage { get; private set; }

    public CorrelationId CorrelationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>TASK-SIG-02: monotonic per-entity version bumped by every state-mutating method below, in the same transaction as the mutation - lets SignalR clients (module 25) ignore stale/out-of-order events. Never reset; starts at 0.</summary>
    public long EventSequence { get; private set; }

    private ProcessingJob()
    {
    }

    public static ProcessingJob Create(Guid applicationId, Guid fileId, CorrelationId correlationId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ApplicationId = applicationId,
        FileId = fileId,
        Status = JobStatus.Queued,
        Stage = ProcessingStage.Queued,
        Progress = 0,
        AttemptNumber = 0,
        CorrelationId = correlationId,
        CreatedAt = now,
    };

    /// <summary>See the type-level remarks: the concurrency-safe claim path is the repository's atomic UPDATE, not this method + SaveChanges.</summary>
    public void Claim(string workerId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (Status != JobStatus.Queued)
        {
            throw new JobStateConflictException($"Cannot claim job '{Id}' in status '{Status}' (expected Queued).");
        }

        Status = JobStatus.Running;
        Stage = ProcessingStage.Downloading;
        WorkerId = workerId;
        StartedAt = now;
        LeaseExpiresAt = now + leaseDuration;
        AttemptNumber++;
        HeartbeatAt = now;
        EventSequence++;
    }

    public void Heartbeat(DateTimeOffset now, TimeSpan leaseExtension)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot heartbeat job '{Id}' in status '{Status}' (expected Running).");
        }

        HeartbeatAt = now;
        LeaseExpiresAt = now + leaseExtension;
    }

    public void UpdateProgress(ProcessingStage stage, int progress, DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot update progress for job '{Id}' in status '{Status}' (expected Running).");
        }

        Stage = stage;
        Progress = Math.Clamp(progress, 0, 100);
        HeartbeatAt = now;
        EventSequence++;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot complete job '{Id}' in status '{Status}' (expected Running).");
        }

        Status = JobStatus.Completed;
        Stage = ProcessingStage.Completed;
        Progress = 100;
        CompletedAt = now;
        EventSequence++;
    }

    /// <summary>Terminal (per ASDD §12.4 retry classification) or exhausted-retry failure.</summary>
    public void Fail(ErrorCategory category, string message, DateTimeOffset now)
    {
        if (Status is JobStatus.Completed or JobStatus.Cancelled)
        {
            throw new JobStateConflictException($"Cannot fail job '{Id}' already in terminal status '{Status}'.");
        }

        Status = JobStatus.Failed;
        FailureCategory = category;
        FailureMessage = message;
        CompletedAt = now;
        EventSequence++;
    }

    /// <summary>TASK-JOB-02: records which outbox row announces this job (set once, immediately after <see cref="Create"/> in the same transaction).</summary>
    public void AttachOutboxMessage(Guid outboxMessageId) => OutboxMessageId = outboxMessageId;

    /// <summary>TASK-JOB-03: called by the lease-expiry reaper (TASK-JOB-06) to make an abandoned job re-claimable.</summary>
    public void ReleaseExpiredLease(DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot release lease for job '{Id}' in status '{Status}' (expected Running).");
        }

        Status = JobStatus.Queued;
        Stage = ProcessingStage.Queued;
        WorkerId = null;
        StartedAt = null;
        LeaseExpiresAt = null;
        HeartbeatAt = null;
    }

    /// <summary>TASK-JOB-06: manual (or automatic) retry re-queues a Failed job for another attempt, pointing at a freshly-created outbox row.</summary>
    public void RequeueForRetry(Guid newOutboxMessageId, DateTimeOffset now)
    {
        if (Status != JobStatus.Failed)
        {
            throw new JobStateConflictException($"Cannot retry job '{Id}' in status '{Status}' (only Failed jobs may be retried).");
        }

        Status = JobStatus.Queued;
        Stage = ProcessingStage.Queued;
        Progress = 0;
        WorkerId = null;
        StartedAt = null;
        LeaseExpiresAt = null;
        HeartbeatAt = null;
        FailureCategory = null;
        FailureMessage = null;
        CompletedAt = null;
        OutboxMessageId = newOutboxMessageId;
    }
}
