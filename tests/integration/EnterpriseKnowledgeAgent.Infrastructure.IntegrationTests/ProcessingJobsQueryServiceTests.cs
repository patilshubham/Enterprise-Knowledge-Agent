using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-PRDSH-01/02: integration tests for <see cref="ProcessingJobsQueryService"/>
/// against a real Postgres container - mirrors <see cref="OutputsServiceTests"/>'s
/// convention of testing the orchestration layer directly.
/// </summary>
public sealed class ProcessingJobsQueryServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private const string Actor = "keycloak-sub-1";

    private static ProcessingJobsQueryService CreateService(EnterpriseKnowledgeAgentDbContext dbContext) => new(
        new ProcessingJobRepository(dbContext),
        new KnowledgeFileRepository(dbContext));

    private static async Task<Guid> CreateApplicationAsync(EnterpriseKnowledgeAgentDbContext dbContext, string? name = null)
    {
        var app = ApplicationEntity.Create(name ?? $"App {Guid.NewGuid()}", null, Actor, DateTimeOffset.UtcNow);
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();
        return app.Id;
    }

    private static async Task<KnowledgeFile> CreateFileAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, string displayName = "handbook.pdf")
    {
        var file = KnowledgeFile.Create(applicationId, displayName, $"hash-{Guid.NewGuid()}", 1024, "application/pdf", "utf-8", Actor, DateTimeOffset.UtcNow);
        var repository = new KnowledgeFileRepository(dbContext);
        await repository.AddAsync(file);
        await repository.SaveChangesAsync();
        return file;
    }

    private static async Task<ProcessingJob> CreateJobAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, Guid fileId)
    {
        var job = ProcessingJob.Create(applicationId, fileId, CorrelationId.New(), DateTimeOffset.UtcNow);
        var repository = new ProcessingJobRepository(dbContext);
        await repository.AddAsync(job);
        await repository.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task ListAsync_platform_wide_returns_jobs_across_applications_with_resolved_file_names()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appA = await CreateApplicationAsync(dbContext);
        var appB = await CreateApplicationAsync(dbContext);
        var fileA = await CreateFileAsync(dbContext, appA, "a.pdf");
        var fileB = await CreateFileAsync(dbContext, appB, "b.pdf");
        var jobA = await CreateJobAsync(dbContext, appA, fileA.Id);
        var jobB = await CreateJobAsync(dbContext, appB, fileB.Id);

        var service = CreateService(dbContext);
        var jobs = await service.ListAsync(applicationId: null, status: null, from: null, to: null);

        // Platform-wide is intentionally not scoped to one application, so
        // the shared test database may already contain jobs from other
        // tests in this class - assert the two jobs this test created are
        // both present rather than asserting an exact total count.
        jobs.Should().Contain(j => j.Id == jobA.Id && j.FileName == "a.pdf");
        jobs.Should().Contain(j => j.Id == jobB.Id && j.FileName == "b.pdf");
    }

    [Fact]
    public async Task ListAsync_scoped_to_one_application_excludes_other_applications_jobs()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appA = await CreateApplicationAsync(dbContext);
        var appB = await CreateApplicationAsync(dbContext);
        var fileA = await CreateFileAsync(dbContext, appA);
        var fileB = await CreateFileAsync(dbContext, appB);
        var jobA = await CreateJobAsync(dbContext, appA, fileA.Id);
        await CreateJobAsync(dbContext, appB, fileB.Id);

        var service = CreateService(dbContext);
        var jobs = await service.ListAsync(appA, status: null, from: null, to: null);

        jobs.Should().ContainSingle().Which.Id.Should().Be(jobA.Id);
    }

    [Fact]
    public async Task ListAsync_filters_by_status()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appId = await CreateApplicationAsync(dbContext);
        var file = await CreateFileAsync(dbContext, appId);
        var queuedJob = await CreateJobAsync(dbContext, appId, file.Id);

        var failingJob = await CreateJobAsync(dbContext, appId, file.Id);
        failingJob.Claim("worker-1", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        failingJob.Fail(ErrorCategory.GenerationFailure, "boom", DateTimeOffset.UtcNow);
        var jobRepository = new ProcessingJobRepository(dbContext);
        await jobRepository.SaveChangesAsync();

        var service = CreateService(dbContext);
        var failedJobs = await service.ListAsync(appId, JobStatus.Failed, from: null, to: null);
        var queuedJobs = await service.ListAsync(appId, JobStatus.Queued, from: null, to: null);

        failedJobs.Should().ContainSingle().Which.Id.Should().Be(failingJob.Id);
        failedJobs[0].RetryEligible.Should().BeTrue();
        queuedJobs.Should().ContainSingle().Which.Id.Should().Be(queuedJob.Id);
        queuedJobs[0].RetryEligible.Should().BeFalse();
    }

    [Fact]
    public async Task ListAsync_shows_deleted_file_placeholder_when_the_file_no_longer_exists()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appId = await CreateApplicationAsync(dbContext);
        var job = await CreateJobAsync(dbContext, appId, Guid.NewGuid());

        var service = CreateService(dbContext);
        var jobs = await service.ListAsync(appId, status: null, from: null, to: null);

        jobs.Should().ContainSingle().Which.FileName.Should().Be("(deleted file)");
        job.Should().NotBeNull();
    }

    [Fact]
    public async Task GetDetailAsync_returns_full_detail_including_correlation_id()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appId = await CreateApplicationAsync(dbContext);
        var file = await CreateFileAsync(dbContext, appId);
        var job = await CreateJobAsync(dbContext, appId, file.Id);

        var service = CreateService(dbContext);
        var detail = await service.GetDetailAsync(appId, job.Id);

        detail.Id.Should().Be(job.Id);
        detail.FileName.Should().Be(file.DisplayName);
        detail.CorrelationId.Should().NotBeNullOrWhiteSpace();
        detail.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task GetDetailAsync_throws_when_the_job_belongs_to_a_different_application()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appA = await CreateApplicationAsync(dbContext);
        var appB = await CreateApplicationAsync(dbContext);
        var file = await CreateFileAsync(dbContext, appA);
        var job = await CreateJobAsync(dbContext, appA, file.Id);

        var service = CreateService(dbContext);
        var act = async () => await service.GetDetailAsync(appB, job.Id);

        await act.Should().ThrowAsync<ProcessingJobNotFoundException>();
    }

    [Fact]
    public async Task GetDetailAsync_platform_scoped_ignores_application_boundary()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appId = await CreateApplicationAsync(dbContext);
        var file = await CreateFileAsync(dbContext, appId);
        var job = await CreateJobAsync(dbContext, appId, file.Id);

        var service = CreateService(dbContext);
        var detail = await service.GetDetailAsync(applicationId: null, job.Id);

        detail.Id.Should().Be(job.Id);
    }
}
