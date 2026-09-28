using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Domain.Execution;

/// <summary>
/// TASK-JOB-01: a durable record of an agent run (ASDD §9.4, §12.2).
/// <see cref="AgentId"/> references a future <c>Agent</c> (module 17, not
/// yet built) by id only. The snapshot/parameter fields are populated by the
/// caller when creating the run (module 19's job, out of scope here) - this
/// type only owns persistence + the same claim/lease/heartbeat lifecycle as
/// <see cref="ProcessingJob"/> (TASK-JOB-03).
/// </summary>
/// <remarks>See <see cref="ProcessingJob"/>'s remarks: the concurrency-safe
/// claim path is the repository's atomic UPDATE, not <see cref="Claim"/> + SaveChanges.</remarks>
public sealed class AgentRun
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid AgentId { get; private set; }

    /// <summary>TASK-JOB-04/05: the outbox row announcing this run's queue message - lets the reconciler check delivery without parsing message payloads.</summary>
    public Guid? OutboxMessageId { get; private set; }

    public string RequestedBy { get; private set; } = string.Empty;

    public string UserRequest { get; private set; } = string.Empty;

    /// <summary>TASK-RUN-01: caller-supplied idempotency key (unique per <see cref="AgentId"/>) - repeated submissions with the same key return this same run instead of creating a duplicate.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>TASK-RUN-01: optional user-supplied title for the eventual <c>GeneratedOutput</c> (module 23, not yet built) - carried on the run so it isn't lost before that module exists.</summary>
    public string? OutputTitle { get; private set; }

    public JobStatus Status { get; private set; }

    public RunStage Stage { get; private set; }

    public int Progress { get; private set; }

    public string AgentNameSnapshot { get; private set; } = string.Empty;

    public string AgentPromptSnapshot { get; private set; } = string.Empty;

    public string? KnowledgeScopeSnapshotJson { get; private set; }

    public string? DeploymentSnapshot { get; private set; }

    public string? RequestedParametersJson { get; private set; }

    public string? EffectiveParametersJson { get; private set; }

    public string? RetrievalSettingsJson { get; private set; }

    public string? WorkerId { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public int AttemptNumber { get; private set; }

    public DateTimeOffset? HeartbeatAt { get; private set; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public ErrorCategory? FailureCategory { get; private set; }

    public string? FailureMessage { get; private set; }

    public CorrelationId CorrelationId { get; private set; }

    /// <summary>TASK-RUN-05: set by <see cref="RequestCancellation"/> when the run is already claimed (Running, pre-generation) - the worker checks this before the Generating stage and, if set, calls <see cref="Cancel"/> itself instead of the request racing the worker's own concurrent writes.</summary>
    public bool CancellationRequested { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>TASK-SIG-02: monotonic per-entity version bumped by every state-mutating method below, in the same transaction as the mutation - lets SignalR clients (module 25) ignore stale/out-of-order events. Never reset; starts at 0.</summary>
    public long EventSequence { get; private set; }

    private AgentRun()
    {
    }

    public static AgentRun Create(
        Guid applicationId,
        Guid agentId,
        string requestedBy,
        string userRequest,
        string idempotencyKey,
        string? outputTitle,
        string agentNameSnapshot,
        string agentPromptSnapshot,
        string? knowledgeScopeSnapshotJson,
        string? deploymentSnapshot,
        string? requestedParametersJson,
        string? effectiveParametersJson,
        string? retrievalSettingsJson,
        CorrelationId correlationId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        return new()
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            AgentId = agentId,
            RequestedBy = requestedBy,
            UserRequest = userRequest,
            IdempotencyKey = idempotencyKey,
            OutputTitle = outputTitle,
            Status = JobStatus.Queued,
            Stage = RunStage.Queued,
            Progress = 0,
            AgentNameSnapshot = agentNameSnapshot,
            AgentPromptSnapshot = agentPromptSnapshot,
            KnowledgeScopeSnapshotJson = knowledgeScopeSnapshotJson,
            DeploymentSnapshot = deploymentSnapshot,
            RequestedParametersJson = requestedParametersJson,
            EffectiveParametersJson = effectiveParametersJson,
            RetrievalSettingsJson = retrievalSettingsJson,
            AttemptNumber = 0,
            CorrelationId = correlationId,
            CreatedAt = now,
        };
    }

    /// <summary>TASK-JOB-02: records which outbox row announces this run (set once, immediately after <see cref="Create"/> in the same transaction).</summary>
    public void AttachOutboxMessage(Guid outboxMessageId) => OutboxMessageId = outboxMessageId;

    public void Claim(string workerId, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (Status != JobStatus.Queued)
        {
            throw new JobStateConflictException($"Cannot claim run '{Id}' in status '{Status}' (expected Queued).");
        }

        Status = JobStatus.Running;
        Stage = RunStage.Preparing;
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
            throw new JobStateConflictException($"Cannot heartbeat run '{Id}' in status '{Status}' (expected Running).");
        }

        HeartbeatAt = now;
        LeaseExpiresAt = now + leaseExtension;
    }

    public void UpdateProgress(RunStage stage, int progress, DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot update progress for run '{Id}' in status '{Status}' (expected Running).");
        }

        Stage = stage;
        Progress = Math.Clamp(progress, 0, 100);
        HeartbeatAt = now;
        EventSequence++;
    }

    /// <summary>TASK-GEN-01: overwrites the effective-parameters snapshot after a generation-time capability adjustment (e.g. TopP dropped because the deployment doesn't support it). Requested and effective start identical at <see cref="Create"/>; this is the only way effective ever diverges.</summary>
    public void RecordEffectiveParameters(string effectiveParametersJson)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot record effective parameters for run '{Id}' in status '{Status}' (expected Running).");
        }

        EffectiveParametersJson = effectiveParametersJson;
    }

    public void Complete(DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot complete run '{Id}' in status '{Status}' (expected Running).");
        }

        Status = JobStatus.Completed;
        Stage = RunStage.Completed;
        Progress = 100;
        CompletedAt = now;
        EventSequence++;
    }

    public void Fail(ErrorCategory category, string message, DateTimeOffset now)
    {
        if (Status is JobStatus.Completed or JobStatus.Cancelled)
        {
            throw new JobStateConflictException($"Cannot fail run '{Id}' already in terminal status '{Status}'.");
        }

        Status = JobStatus.Failed;
        Stage = RunStage.Failed;
        FailureCategory = category;
        FailureMessage = message;
        CompletedAt = now;
        EventSequence++;
    }

    /// <summary>Decision D2: only pre-generation cancellation is supported in the baseline.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (Status != JobStatus.Queued && Stage is not (RunStage.Preparing or RunStage.Retrieving or RunStage.BuildingContext))
        {
            throw new JobStateConflictException($"Cannot cancel run '{Id}' once generation has started (stage '{Stage}').");
        }

        Status = JobStatus.Cancelled;
        Stage = RunStage.Cancelled;
        CompletedAt = now;
        EventSequence++;
    }

    /// <summary>
    /// TASK-RUN-05: entry point for a user-initiated cancel request. A
    /// still-<see cref="JobStatus.Queued"/> run (no worker processing it yet)
    /// transitions to <see cref="JobStatus.Cancelled"/> immediately - there is
    /// nothing racing this write. A claimed run in a pre-generation stage
    /// instead only records the flag (<see cref="CancellationRequested"/>);
    /// the worker's own <see cref="AgentRun"/> instance in its own
    /// transaction owns the actual transition (see <see cref="Cancel"/>),
    /// checked immediately before the Generating stage, so this request never
    /// writes to the same in-flight columns the worker is concurrently
    /// updating. Throws (a "clear message", per Decision D2) once generation
    /// has started or the run is already terminal.
    /// </summary>
    public void RequestCancellation(DateTimeOffset now)
    {
        if (Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
        {
            throw new JobStateConflictException($"Cannot cancel run '{Id}' already in terminal status '{Status}'.");
        }

        if (Status == JobStatus.Queued)
        {
            Cancel(now);
            return;
        }

        if (Stage is not (RunStage.Preparing or RunStage.Retrieving or RunStage.BuildingContext))
        {
            throw new JobStateConflictException($"Cannot cancel run '{Id}' once generation has started (stage '{Stage}').");
        }

        CancellationRequested = true;
    }

    public void ReleaseExpiredLease(DateTimeOffset now)
    {
        if (Status != JobStatus.Running)
        {
            throw new JobStateConflictException($"Cannot release lease for run '{Id}' in status '{Status}' (expected Running).");
        }

        Status = JobStatus.Queued;
        Stage = RunStage.Queued;
        WorkerId = null;
        StartedAt = null;
        LeaseExpiresAt = null;
        HeartbeatAt = null;
    }

    public void RequeueForRetry(Guid newOutboxMessageId, DateTimeOffset now)
    {
        if (Status != JobStatus.Failed)
        {
            throw new JobStateConflictException($"Cannot retry run '{Id}' in status '{Status}' (only Failed runs may be retried).");
        }

        Status = JobStatus.Queued;
        Stage = RunStage.Queued;
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
