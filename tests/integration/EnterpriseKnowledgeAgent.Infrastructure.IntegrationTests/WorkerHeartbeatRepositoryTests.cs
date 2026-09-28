using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-JOB-06: worker heartbeat upsert/query tests against a real Postgres container.</summary>
public sealed class WorkerHeartbeatRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task UpsertAsync_first_call_creates_a_new_row()
    {
        var workerId = $"worker-{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;

        await using var dbContext = fixture.CreateDbContext();
        await new WorkerHeartbeatRepository(dbContext).UpsertAsync(workerId, "document-worker", now);

        await using var readContext = fixture.CreateDbContext();
        var all = await new WorkerHeartbeatRepository(readContext).ListAllAsync();
        var found = all.Single(h => h.WorkerId == workerId);
        found.WorkerKind.Should().Be("document-worker");
        // Postgres timestamps have microsecond precision vs .NET's tick precision, so a strict
        // Be() can fail by a fraction of a microsecond after a round-trip - BeCloseTo tolerates that.
        found.StartedAt.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));
        found.LastHeartbeatAt.Should().BeCloseTo(now, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task UpsertAsync_second_call_touches_the_existing_row_instead_of_duplicating()
    {
        var workerId = $"worker-{Guid.NewGuid()}";
        var startedAt = DateTimeOffset.UtcNow;
        var secondBeat = startedAt.AddMinutes(1);

        await using (var firstContext = fixture.CreateDbContext())
        {
            await new WorkerHeartbeatRepository(firstContext).UpsertAsync(workerId, "agent-worker", startedAt);
        }

        await using (var secondContext = fixture.CreateDbContext())
        {
            await new WorkerHeartbeatRepository(secondContext).UpsertAsync(workerId, "agent-worker", secondBeat);
        }

        await using var readContext = fixture.CreateDbContext();
        var all = await new WorkerHeartbeatRepository(readContext).ListAllAsync();
        var matches = all.Where(h => h.WorkerId == workerId).ToList();

        matches.Should().HaveCount(1);
        matches[0].StartedAt.Should().BeCloseTo(startedAt, TimeSpan.FromMilliseconds(1));
        matches[0].LastHeartbeatAt.Should().BeCloseTo(secondBeat, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ListStaleAsync_returns_only_workers_last_seen_before_the_threshold()
    {
        var staleWorkerId = $"worker-{Guid.NewGuid()}";
        var freshWorkerId = $"worker-{Guid.NewGuid()}";
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new WorkerHeartbeatRepository(writeContext);
            await repository.UpsertAsync(staleWorkerId, "document-worker", now.AddMinutes(-10));
            await repository.UpsertAsync(freshWorkerId, "document-worker", now);
        }

        await using var readContext = fixture.CreateDbContext();
        var stale = await new WorkerHeartbeatRepository(readContext).ListStaleAsync(now.AddMinutes(-5));

        stale.Select(h => h.WorkerId).Should().Contain(staleWorkerId);
        stale.Select(h => h.WorkerId).Should().NotContain(freshWorkerId);
    }
}
