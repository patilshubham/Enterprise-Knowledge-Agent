using EnterpriseKnowledgeAgent.Domain.Operations;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>TASK-JOB-06: persistence for worker heartbeats.</summary>
public interface IWorkerHeartbeatRepository
{
    /// <summary>Inserts a new heartbeat row for a worker id not seen before, or touches the existing one (TASK-JOB-06).</summary>
    Task UpsertAsync(string workerId, string workerKind, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkerHeartbeat>> ListAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkerHeartbeat>> ListStaleAsync(DateTimeOffset threshold, CancellationToken cancellationToken = default);
}
