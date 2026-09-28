using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-JOB-01/02/03/05: persistence and atomic claim/lease tests against a real Postgres container (mirrors <see cref="ProcessingJobRepositoryTests"/>).</summary>
public sealed class AgentRunRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static AgentRun NewRun(Guid? applicationId = null, DateTimeOffset? now = null) =>
        AgentRun.Create(
            applicationId ?? Guid.NewGuid(),
            Guid.NewGuid(),
            "keycloak-sub-1",
            "Summarize the report",
            Guid.NewGuid().ToString(),
            outputTitle: null,
            "Support Agent",
            "You are a helpful assistant.",
            knowledgeScopeSnapshotJson: null,
            deploymentSnapshot: "gpt-4o",
            requestedParametersJson: null,
            effectiveParametersJson: null,
            retrievalSettingsJson: null,
            CorrelationId.New(),
            now ?? DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_all_fields()
    {
        var run = NewRun();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(run);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new AgentRunRepository(readContext).GetAsync(run.Id);

        loaded.Should().NotBeNull();
        loaded!.ApplicationId.Should().Be(run.ApplicationId);
        loaded.AgentId.Should().Be(run.AgentId);
        loaded.AgentNameSnapshot.Should().Be("Support Agent");
        loaded.Status.Should().Be(JobStatus.Queued);
    }

    [Fact]
    public async Task TryClaimAsync_on_queued_run_succeeds_and_persists_claim_fields()
    {
        var run = NewRun();
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(run);
            await repository.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        await using var claimContext = fixture.CreateDbContext();
        var claimed = await new AgentRunRepository(claimContext)
            .TryClaimAsync(run.Id, "worker-1", now, TimeSpan.FromMinutes(5));

        claimed.Should().BeTrue();

        await using var readContext = fixture.CreateDbContext();
        var reloaded = await new AgentRunRepository(readContext).GetAsync(run.Id);
        reloaded!.Status.Should().Be(JobStatus.Running);
        reloaded.WorkerId.Should().Be("worker-1");
        reloaded.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public async Task TryClaimAsync_concurrent_race_only_one_of_many_callers_wins()
    {
        var run = NewRun();
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(run);
            await repository.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        const int concurrentClaimers = 8;

        var results = await Task.WhenAll(Enumerable.Range(0, concurrentClaimers).Select(async i =>
        {
            await using var dbContext = fixture.CreateDbContext();
            return await new AgentRunRepository(dbContext)
                .TryClaimAsync(run.Id, $"worker-{i}", now, TimeSpan.FromMinutes(5));
        }));

        results.Count(claimed => claimed).Should().Be(1);
    }

    [Fact]
    public async Task HeartbeatAsync_for_a_different_worker_fails()
    {
        var run = NewRun();
        var now = DateTimeOffset.UtcNow;
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(run);
            await repository.SaveChangesAsync();
            await repository.TryClaimAsync(run.Id, "worker-1", now, TimeSpan.FromMinutes(5));
        }

        await using var heartbeatContext = fixture.CreateDbContext();
        var heartbeatOk = await new AgentRunRepository(heartbeatContext)
            .HeartbeatAsync(run.Id, "worker-imposter", now.AddMinutes(1), TimeSpan.FromMinutes(5));

        heartbeatOk.Should().BeFalse();
    }

    [Fact]
    public async Task ListExpiredLeasesAsync_returns_only_running_runs_past_their_lease()
    {
        var expiredRun = NewRun();
        var freshRun = NewRun();
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(expiredRun);
            await repository.AddAsync(freshRun);
            await repository.SaveChangesAsync();
            await repository.TryClaimAsync(expiredRun.Id, "worker-1", now.AddMinutes(-10), TimeSpan.FromMinutes(1));
            await repository.TryClaimAsync(freshRun.Id, "worker-2", now, TimeSpan.FromMinutes(30));
        }

        await using var readContext = fixture.CreateDbContext();
        var expired = await new AgentRunRepository(readContext).ListExpiredLeasesAsync(now);

        expired.Select(r => r.Id).Should().Contain(expiredRun.Id);
        expired.Select(r => r.Id).Should().NotContain(freshRun.Id);
    }

    [Fact]
    public async Task ListStaleUndeliveredAsync_returns_only_queued_runs_whose_outbox_row_is_still_undelivered()
    {
        var now = DateTimeOffset.UtcNow;
        var createdBefore = now.AddMinutes(-1);

        var undeliveredOutbox = OutboxMessage.Create(OutboxMessageType.AgentExecution, "{}", createdBefore.AddMinutes(-1));
        var deliveredOutbox = OutboxMessage.Create(OutboxMessageType.AgentExecution, "{}", createdBefore.AddMinutes(-1));
        deliveredOutbox.MarkDelivered(createdBefore);

        await using (var writeContext = fixture.CreateDbContext())
        {
            await writeContext.OutboxMessages.AddAsync(undeliveredOutbox);
            await writeContext.OutboxMessages.AddAsync(deliveredOutbox);
            await writeContext.SaveChangesAsync();
        }

        var staleUndeliveredRun = NewRun(now: createdBefore.AddMinutes(-5));
        staleUndeliveredRun.AttachOutboxMessage(undeliveredOutbox.Id);
        var deliveredRun = NewRun(now: createdBefore.AddMinutes(-5));
        deliveredRun.AttachOutboxMessage(deliveredOutbox.Id);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(staleUndeliveredRun);
            await repository.AddAsync(deliveredRun);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var stale = await new AgentRunRepository(readContext).ListStaleUndeliveredAsync(createdBefore);

        stale.Select(r => r.Id).Should().Contain(staleUndeliveredRun.Id);
        stale.Select(r => r.Id).Should().NotContain(deliveredRun.Id);
    }

    [Fact]
    public async Task ListFailedAsync_returns_only_failed_runs_for_the_given_application()
    {
        var applicationId = Guid.NewGuid();
        var failedRun = NewRun(applicationId);
        var queuedRun = NewRun(applicationId);
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(failedRun);
            await repository.AddAsync(queuedRun);
            await repository.SaveChangesAsync();
        }

        await using (var failContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(failContext);
            var loaded = await repository.GetAsync(failedRun.Id);
            loaded!.Claim("worker-1", now, TimeSpan.FromMinutes(5));
            loaded.Fail(ErrorCategory.GenerationFailure, "boom", now.AddMinutes(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var failed = await new AgentRunRepository(readContext).ListFailedAsync(applicationId);

        failed.Select(r => r.Id).Should().Contain(failedRun.Id);
        failed.Select(r => r.Id).Should().NotContain(queuedRun.Id);
    }

    [Fact]
    public async Task CountByStatusAsync_groups_by_status_and_honors_application_scope()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var queuedRun = NewRun(applicationId);
        var failedRun = NewRun(applicationId);
        var otherAppRun = NewRun(otherApplicationId);
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(writeContext);
            await repository.AddAsync(queuedRun);
            await repository.AddAsync(failedRun);
            await repository.AddAsync(otherAppRun);
            await repository.SaveChangesAsync();
        }

        await using (var failContext = fixture.CreateDbContext())
        {
            var repository = new AgentRunRepository(failContext);
            var loaded = await repository.GetAsync(failedRun.Id);
            loaded!.Claim("worker-1", now, TimeSpan.FromMinutes(5));
            loaded.Fail(ErrorCategory.GenerationFailure, "boom", now.AddMinutes(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var repositoryForRead = new AgentRunRepository(readContext);

        var scoped = await repositoryForRead.CountByStatusAsync(applicationId);
        scoped[JobStatus.Queued].Should().Be(1);
        scoped[JobStatus.Failed].Should().Be(1);
        scoped.Should().NotContainKey(JobStatus.Completed);

        var platformWide = await repositoryForRead.CountByStatusAsync(applicationId: null);
        platformWide[JobStatus.Queued].Should().BeGreaterThanOrEqualTo(2); // includes otherAppRun
        platformWide[JobStatus.Failed].Should().BeGreaterThanOrEqualTo(1);
    }
}
