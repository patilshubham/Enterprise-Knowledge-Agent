using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-JOB-01/02/03/05: persistence and atomic claim/lease tests against a real Postgres container.</summary>
public sealed class ProcessingJobRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static ProcessingJob NewJob(Guid? applicationId = null) =>
        ProcessingJob.Create(applicationId ?? Guid.NewGuid(), Guid.NewGuid(), CorrelationId.New(), DateTimeOffset.UtcNow);

    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_all_fields()
    {
        var job = NewJob();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(job);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new ProcessingJobRepository(readContext).GetAsync(job.Id);

        loaded.Should().NotBeNull();
        loaded!.ApplicationId.Should().Be(job.ApplicationId);
        loaded.FileId.Should().Be(job.FileId);
        loaded.Status.Should().Be(JobStatus.Queued);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_id()
    {
        await using var dbContext = fixture.CreateDbContext();

        var loaded = await new ProcessingJobRepository(dbContext).GetAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task TryClaimAsync_on_queued_job_succeeds_and_persists_claim_fields()
    {
        var job = NewJob();
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(job);
            await repository.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        await using var claimContext = fixture.CreateDbContext();
        var claimed = await new ProcessingJobRepository(claimContext)
            .TryClaimAsync(job.Id, "worker-1", now, TimeSpan.FromMinutes(5));

        claimed.Should().BeTrue();

        await using var readContext = fixture.CreateDbContext();
        var reloaded = await new ProcessingJobRepository(readContext).GetAsync(job.Id);
        reloaded!.Status.Should().Be(JobStatus.Running);
        reloaded.WorkerId.Should().Be("worker-1");
        reloaded.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public async Task TryClaimAsync_on_already_running_job_fails()
    {
        var job = NewJob();
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(job);
            await repository.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        await using (var firstClaimContext = fixture.CreateDbContext())
        {
            var firstClaimed = await new ProcessingJobRepository(firstClaimContext)
                .TryClaimAsync(job.Id, "worker-1", now, TimeSpan.FromMinutes(5));
            firstClaimed.Should().BeTrue();
        }

        await using var secondClaimContext = fixture.CreateDbContext();
        var secondClaimed = await new ProcessingJobRepository(secondClaimContext)
            .TryClaimAsync(job.Id, "worker-2", now, TimeSpan.FromMinutes(5));

        secondClaimed.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimAsync_concurrent_race_only_one_of_many_callers_wins()
    {
        var job = NewJob();
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(job);
            await repository.SaveChangesAsync();
        }

        var now = DateTimeOffset.UtcNow;
        const int concurrentClaimers = 8;

        var results = await Task.WhenAll(Enumerable.Range(0, concurrentClaimers).Select(async i =>
        {
            await using var dbContext = fixture.CreateDbContext();
            return await new ProcessingJobRepository(dbContext)
                .TryClaimAsync(job.Id, $"worker-{i}", now, TimeSpan.FromMinutes(5));
        }));

        results.Count(claimed => claimed).Should().Be(1);

        await using var readContext = fixture.CreateDbContext();
        var reloaded = await new ProcessingJobRepository(readContext).GetAsync(job.Id);
        reloaded!.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public async Task HeartbeatAsync_for_the_claiming_worker_extends_the_lease()
    {
        var job = NewJob();
        var now = DateTimeOffset.UtcNow;
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(job);
            await repository.SaveChangesAsync();
            await repository.TryClaimAsync(job.Id, "worker-1", now, TimeSpan.FromMinutes(5));
        }

        var heartbeatAt = now.AddMinutes(2);
        await using var heartbeatContext = fixture.CreateDbContext();
        var heartbeatOk = await new ProcessingJobRepository(heartbeatContext)
            .HeartbeatAsync(job.Id, "worker-1", heartbeatAt, TimeSpan.FromMinutes(5));

        heartbeatOk.Should().BeTrue();

        await using var readContext = fixture.CreateDbContext();
        var reloaded = await new ProcessingJobRepository(readContext).GetAsync(job.Id);
        // Postgres timestamps have microsecond precision vs .NET's tick precision, so a strict
        // Be() can fail by a fraction of a microsecond after a round-trip - BeCloseTo tolerates that.
        reloaded!.HeartbeatAt.Should().BeCloseTo(heartbeatAt, TimeSpan.FromMilliseconds(1));
        reloaded.LeaseExpiresAt.Should().BeCloseTo(heartbeatAt.AddMinutes(5), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task HeartbeatAsync_for_a_different_worker_fails()
    {
        var job = NewJob();
        var now = DateTimeOffset.UtcNow;
        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(job);
            await repository.SaveChangesAsync();
            await repository.TryClaimAsync(job.Id, "worker-1", now, TimeSpan.FromMinutes(5));
        }

        await using var heartbeatContext = fixture.CreateDbContext();
        var heartbeatOk = await new ProcessingJobRepository(heartbeatContext)
            .HeartbeatAsync(job.Id, "worker-imposter", now.AddMinutes(1), TimeSpan.FromMinutes(5));

        heartbeatOk.Should().BeFalse();
    }

    [Fact]
    public async Task ListExpiredLeasesAsync_returns_only_running_jobs_past_their_lease()
    {
        var expiredJob = NewJob();
        var freshJob = NewJob();
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(expiredJob);
            await repository.AddAsync(freshJob);
            await repository.SaveChangesAsync();
            await repository.TryClaimAsync(expiredJob.Id, "worker-1", now.AddMinutes(-10), TimeSpan.FromMinutes(1));
            await repository.TryClaimAsync(freshJob.Id, "worker-2", now, TimeSpan.FromMinutes(30));
        }

        await using var readContext = fixture.CreateDbContext();
        var expired = await new ProcessingJobRepository(readContext).ListExpiredLeasesAsync(now);

        expired.Select(j => j.Id).Should().Contain(expiredJob.Id);
        expired.Select(j => j.Id).Should().NotContain(freshJob.Id);
    }

    [Fact]
    public async Task ListStaleUndeliveredAsync_returns_only_queued_jobs_whose_outbox_row_is_still_undelivered()
    {
        var now = DateTimeOffset.UtcNow;
        var createdBefore = now.AddMinutes(-1);
        var correlationId = CorrelationId.New();

        var undeliveredOutbox = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, "{}", createdBefore.AddMinutes(-1));
        var deliveredOutbox = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, "{}", createdBefore.AddMinutes(-1));
        deliveredOutbox.MarkDelivered(createdBefore);

        await using (var writeContext = fixture.CreateDbContext())
        {
            await writeContext.OutboxMessages.AddAsync(undeliveredOutbox);
            await writeContext.OutboxMessages.AddAsync(deliveredOutbox);
            await writeContext.SaveChangesAsync();
        }

        var staleUndeliveredJob = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), correlationId, createdBefore.AddMinutes(-5));
        staleUndeliveredJob.AttachOutboxMessage(undeliveredOutbox.Id);
        var deliveredJob = ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), correlationId, createdBefore.AddMinutes(-5));
        deliveredJob.AttachOutboxMessage(deliveredOutbox.Id);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(staleUndeliveredJob);
            await repository.AddAsync(deliveredJob);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var stale = await new ProcessingJobRepository(readContext).ListStaleUndeliveredAsync(createdBefore);

        stale.Select(j => j.Id).Should().Contain(staleUndeliveredJob.Id);
        stale.Select(j => j.Id).Should().NotContain(deliveredJob.Id);
    }

    [Fact]
    public async Task ListFailedAsync_returns_only_failed_jobs_for_the_given_application()
    {
        var applicationId = Guid.NewGuid();
        var failedJob = NewJob(applicationId);
        var queuedJob = NewJob(applicationId);
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(failedJob);
            await repository.AddAsync(queuedJob);
            await repository.SaveChangesAsync();
        }

        await using (var failContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(failContext);
            var loaded = await repository.GetAsync(failedJob.Id);
            loaded!.Claim("worker-1", now, TimeSpan.FromMinutes(5));
            loaded.Fail(ErrorCategory.Timeout, "boom", now.AddMinutes(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var failed = await new ProcessingJobRepository(readContext).ListFailedAsync(applicationId);

        failed.Select(j => j.Id).Should().Contain(failedJob.Id);
        failed.Select(j => j.Id).Should().NotContain(queuedJob.Id);
    }

    [Fact]
    public async Task CountByStatusAsync_groups_by_status_and_honors_application_scope()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var queuedJob = NewJob(applicationId);
        var failedJob = NewJob(applicationId);
        var otherAppJob = NewJob(otherApplicationId);
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(writeContext);
            await repository.AddAsync(queuedJob);
            await repository.AddAsync(failedJob);
            await repository.AddAsync(otherAppJob);
            await repository.SaveChangesAsync();
        }

        await using (var failContext = fixture.CreateDbContext())
        {
            var repository = new ProcessingJobRepository(failContext);
            var loaded = await repository.GetAsync(failedJob.Id);
            loaded!.Claim("worker-1", now, TimeSpan.FromMinutes(5));
            loaded.Fail(ErrorCategory.Timeout, "boom", now.AddMinutes(1));
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var repositoryForRead = new ProcessingJobRepository(readContext);

        var scoped = await repositoryForRead.CountByStatusAsync(applicationId);
        scoped[JobStatus.Queued].Should().Be(1);
        scoped[JobStatus.Failed].Should().Be(1);
        scoped.Should().NotContainKey(JobStatus.Completed);

        var platformWide = await repositoryForRead.CountByStatusAsync(applicationId: null);
        platformWide[JobStatus.Queued].Should().BeGreaterThanOrEqualTo(2); // includes otherAppJob
        platformWide[JobStatus.Failed].Should().BeGreaterThanOrEqualTo(1);
    }
}
