using EnterpriseKnowledgeAgent.Domain.Operations;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Operations;

/// <summary>TASK-JOB-06: worker liveness heartbeat record.</summary>
public sealed class WorkerHeartbeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_started_and_last_heartbeat_to_now()
    {
        var heartbeat = WorkerHeartbeat.Create("worker-1", "document-worker", Now);

        heartbeat.WorkerId.Should().Be("worker-1");
        heartbeat.WorkerKind.Should().Be("document-worker");
        heartbeat.StartedAt.Should().Be(Now);
        heartbeat.LastHeartbeatAt.Should().Be(Now);
    }

    [Fact]
    public void Touch_updates_last_heartbeat_but_not_started_at()
    {
        var heartbeat = WorkerHeartbeat.Create("worker-1", "document-worker", Now);

        heartbeat.Touch(Now.AddMinutes(5));

        heartbeat.LastHeartbeatAt.Should().Be(Now.AddMinutes(5));
        heartbeat.StartedAt.Should().Be(Now);
    }
}
