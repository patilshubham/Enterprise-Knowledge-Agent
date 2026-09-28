using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>TASK-JOB-01/02/03: persistence for <see cref="AgentRun"/> (see <see cref="IProcessingJobRepository"/> remarks on atomic claim/heartbeat).</summary>
public interface IAgentRunRepository
{
    Task AddAsync(AgentRun run, CancellationToken cancellationToken = default);

    Task<AgentRun?> GetAsync(Guid runId, CancellationToken cancellationToken = default);

    /// <summary>TASK-RUN-01: idempotency lookup - returns the existing run for this (agentId, idempotencyKey) pair, if any, so a duplicate submission can return it instead of creating a new run.</summary>
    Task<AgentRun?> GetByIdempotencyKeyAsync(Guid agentId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<bool> TryClaimAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    /// <summary>TASK-RUN-05: no-tracking read of the latest persisted <see cref="AgentRun.CancellationRequested"/> value - a plain <see cref="GetAsync"/> would return the same tracked instance the worker has been mutating all pipeline and would never see a concurrent cancel request's write.</summary>
    Task<bool> IsCancellationRequestedAsync(Guid runId, CancellationToken cancellationToken = default);

    Task<bool> HeartbeatAsync(Guid runId, string workerId, DateTimeOffset now, TimeSpan leaseExtension, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRun>> ListExpiredLeasesAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRun>> ListStaleUndeliveredAsync(DateTimeOffset createdBefore, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentRun>> ListFailedAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>TASK-PDSH-01/TASK-ADSH-01: single aggregate query returning run counts per <see cref="JobStatus"/>, optionally scoped to one application (<paramref name="applicationId"/> null = platform-wide).</summary>
    Task<IReadOnlyDictionary<JobStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK-RDSH-01: the agent-run dashboard listing - newest first, unpaged
    /// (mirrors <c>IGeneratedOutputRepository.ListAsync</c>'s convention of
    /// leaving pagination to the frontend's table component).
    /// <paramref name="applicationId"/> is always required here (unlike the
    /// processing-job listing, agent runs are only ever browsed within one
    /// application - TASK-RDSH-01's route is application-scoped).
    /// </summary>
    Task<IReadOnlyList<AgentRun>> ListAsync(
        Guid applicationId,
        JobStatus? status = null,
        Guid? agentId = null,
        string? createdBy = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
