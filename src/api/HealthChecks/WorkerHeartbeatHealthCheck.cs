using System.Diagnostics;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Shared.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.HealthChecks;

/// <summary>
/// TASK-OPS-01 (ASDD §24 readiness): "required worker heartbeat current".
/// Evaluated per worker <em>kind</em>: a kind is healthy as long as at least
/// one of its instances has a current heartbeat, so orphaned rows left behind
/// by a restarted/redeployed worker (each process uses a fresh worker id) do
/// not drag readiness down while a live instance is running. Unhealthy only if
/// a required kind has instances but none is current (crashed/hung); Degraded
/// (not blocking - still 200 on <c>/health/ready</c>) if a required worker kind
/// has never reported at all, since that is the normal state for a fresh/local
/// environment where the workers haven't started yet rather than a real outage.
/// </summary>
public sealed class WorkerHeartbeatHealthCheck(IWorkerHeartbeatRepository repository, TimeProvider timeProvider)
    : IHealthCheck
{
    /// <summary>Staleness window (single source of truth on <see cref="WorkerHeartbeat"/>), re-exposed here for the operations endpoint and tests.</summary>
    public static readonly TimeSpan StaleThreshold = WorkerHeartbeat.StaleThreshold;

    private static readonly string[] RequiredWorkerKinds = ["document-worker", "agent-worker"];

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var all = await repository.ListAllAsync(cancellationToken);

        // TASK-OBS-02: sample every known worker's heartbeat age each time
        // readiness is checked (worker kind is a small closed set - safe label).
        foreach (var worker in all)
        {
            AppMetrics.WorkerHeartbeatAgeSeconds.Record(
                (now - worker.LastHeartbeatAt).TotalSeconds, new TagList { { "worker.kind", worker.WorkerKind } });
        }

        var byKind = all
            .GroupBy(w => w.WorkerKind)
            .ToDictionary(g => g.Key, g => g.ToList());

        var missingKinds = RequiredWorkerKinds
            .Where(kind => !byKind.ContainsKey(kind))
            .ToList();

        // A required kind is down only if it has reported instances but every
        // one of them is stale - a single current instance keeps the kind up.
        var downKinds = RequiredWorkerKinds
            .Where(kind => byKind.TryGetValue(kind, out var rows) && rows.TrueForAll(w => w.IsStale(now)))
            .ToList();

        if (downKinds.Count > 0)
        {
            var staleWorkers = downKinds.SelectMany(kind => byKind[kind].Select(w => w.WorkerId));
            return HealthCheckResult.Unhealthy(
                $"No current heartbeat for worker kind(s) {string.Join(", ", downKinds)} (all instances stale in {StaleThreshold}: {string.Join(", ", staleWorkers)}).");
        }

        if (missingKinds.Count > 0)
        {
            return HealthCheckResult.Degraded($"No heartbeat ever recorded for worker kind(s): {string.Join(", ", missingKinds)}.");
        }

        return HealthCheckResult.Healthy("All required workers have a current heartbeat.");
    }
}
