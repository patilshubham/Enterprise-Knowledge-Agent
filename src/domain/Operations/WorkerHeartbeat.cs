namespace EnterpriseKnowledgeAgent.Domain.Operations;

/// <summary>
/// TASK-JOB-06: last-seen heartbeat for a worker process (ASDD §9), used by
/// the lease-expiry reaper and operations dashboards (module 26/31) to
/// detect stale/crashed workers.
/// </summary>
public sealed class WorkerHeartbeat
{
    /// <summary>
    /// Canonical staleness window: a worker whose last heartbeat is older than
    /// this is treated as stale/crashed. A generous multiple of the workers'
    /// ~5-20s heartbeat cadence so a couple of missed beats doesn't falsely
    /// flag a healthy worker. Single source of truth shared by the readiness
    /// health check, the operations/platform dashboards, and orphan pruning.
    /// </summary>
    public static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(2);

    public Guid Id { get; private set; }

    public string WorkerId { get; private set; } = string.Empty;

    public string WorkerKind { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset LastHeartbeatAt { get; private set; }

    private WorkerHeartbeat()
    {
    }

    public static WorkerHeartbeat Create(string workerId, string workerKind, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        WorkerId = workerId,
        WorkerKind = workerKind,
        StartedAt = now,
        LastHeartbeatAt = now,
    };

    public void Touch(DateTimeOffset now) => LastHeartbeatAt = now;

    /// <summary>True if this heartbeat is older than <see cref="StaleThreshold"/> relative to <paramref name="now"/>.</summary>
    public bool IsStale(DateTimeOffset now) => now - LastHeartbeatAt > StaleThreshold;
}
