using EnterpriseKnowledgeAgent.Api.HealthChecks;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Operations;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.Tests.HealthChecks;

/// <summary>TASK-OPS-01: readiness logic for "required worker heartbeat current" (ASDD §24).</summary>
public sealed class WorkerHeartbeatHealthCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_all_required_worker_kinds_have_a_current_heartbeat()
    {
        var repository = new FakeWorkerHeartbeatRepository(
            Heartbeat("document-worker-1", "document-worker", Now),
            Heartbeat("agent-worker-1", "agent-worker", Now));
        var check = new WorkerHeartbeatHealthCheck(repository, new FakeTimeProvider(Now));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_a_worker_kind_has_gone_stale()
    {
        var stale = Now - WorkerHeartbeatHealthCheck.StaleThreshold - TimeSpan.FromSeconds(1);
        var repository = new FakeWorkerHeartbeatRepository(
            Heartbeat("document-worker-1", "document-worker", stale),
            Heartbeat("agent-worker-1", "agent-worker", Now));
        var check = new WorkerHeartbeatHealthCheck(repository, new FakeTimeProvider(Now));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("document-worker-1");
    }

    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_a_kind_has_a_stale_orphan_alongside_a_current_instance()
    {
        // A restarted/redeployed worker leaves behind a stale row (fresh worker
        // id per process) while the live instance keeps reporting - the kind is
        // still up and must not drag readiness down (would otherwise 503).
        var stale = Now - WorkerHeartbeatHealthCheck.StaleThreshold - TimeSpan.FromSeconds(1);
        var repository = new FakeWorkerHeartbeatRepository(
            Heartbeat("agent-worker-old", "agent-worker", stale),
            Heartbeat("agent-worker-new", "agent-worker", Now),
            Heartbeat("document-worker-1", "document-worker", Now));
        var check = new WorkerHeartbeatHealthCheck(repository, new FakeTimeProvider(Now));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_degraded_when_a_required_worker_kind_has_never_reported()
    {
        var repository = new FakeWorkerHeartbeatRepository(
            Heartbeat("document-worker-1", "document-worker", Now));
        var check = new WorkerHeartbeatHealthCheck(repository, new FakeTimeProvider(Now));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Degraded);
        result.Description.Should().Contain("agent-worker");
    }

    private static WorkerHeartbeat Heartbeat(string workerId, string workerKind, DateTimeOffset lastHeartbeatAt)
    {
        var heartbeat = WorkerHeartbeat.Create(workerId, workerKind, lastHeartbeatAt);
        heartbeat.Touch(lastHeartbeatAt);
        return heartbeat;
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeWorkerHeartbeatRepository(params WorkerHeartbeat[] heartbeats) : IWorkerHeartbeatRepository
    {
        public Task UpsertAsync(string workerId, string workerKind, DateTimeOffset now, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WorkerHeartbeat>> ListAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkerHeartbeat>>(heartbeats);

        public Task<IReadOnlyList<WorkerHeartbeat>> ListStaleAsync(DateTimeOffset threshold, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkerHeartbeat>>(heartbeats.Where(h => h.LastHeartbeatAt < threshold).ToList());
    }
}
