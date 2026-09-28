using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-RDSH-01/02: integration tests for <see cref="AgentRunsQueryService"/>
/// against a real Postgres container - mirrors <see cref="OutputsServiceTests"/>'s
/// convention of testing the orchestration layer directly.
/// </summary>
public sealed class AgentRunsQueryServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private const string Actor = "keycloak-sub-1";
    private const string ChatDeployment = "gpt-4o";

    private sealed class FakeDeploymentRegistry : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
        ];
    }

    private static AgentRunsQueryService CreateService(EnterpriseKnowledgeAgentDbContext dbContext) => new(
        new AgentRunRepository(dbContext),
        new RunEvidenceRepository(dbContext),
        new GeneratedOutputRepository(dbContext));

    private static async Task<Guid> CreateApplicationAsync(EnterpriseKnowledgeAgentDbContext dbContext)
    {
        var app = ApplicationEntity.Create($"App {Guid.NewGuid()}", null, Actor, DateTimeOffset.UtcNow);
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();
        return app.Id;
    }

    private static async Task<Guid> CreateActiveAgentAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, string name = "Support Agent")
    {
        var agentsService = new AgentsService(
            new AgentRepository(dbContext),
            new ApplicationReadOnlyGuard(new ApplicationRepository(dbContext)),
            new AgentParameterValidator(new FakeDeploymentRegistry()),
            TimeProvider.System);

        var created = await agentsService.CreateAsync(
            applicationId,
            new CreateAgentRequest(
                name, "desc", "You are a helpful support agent.", "AllReadyEnabled", null,
                ChatDeployment, 0.7, 2048, null, "Hybrid", 10, 0.5, 8000,
                IncludeNeighbors: true, StrictGrounding: true, CitationsRequired: true),
            Actor);

        return created.Id;
    }

    private static async Task<AgentRun> CreateRunAsync(
        EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, Guid agentId, string requestedBy = Actor, string userRequest = "Summarize the report")
    {
        var run = AgentRun.Create(
            applicationId, agentId, requestedBy, userRequest, Guid.NewGuid().ToString(), outputTitle: null,
            "Support Agent", "You are a helpful support agent.", knowledgeScopeSnapshotJson: null,
            deploymentSnapshot: ChatDeployment, requestedParametersJson: null, effectiveParametersJson: null,
            retrievalSettingsJson: null, CorrelationId.New(), DateTimeOffset.UtcNow);

        var repository = new AgentRunRepository(dbContext);
        await repository.AddAsync(run);
        await repository.SaveChangesAsync();
        return run;
    }

    [Fact]
    public async Task ListAsync_scoped_to_application_excludes_other_applications_runs()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appA = await CreateApplicationAsync(dbContext);
        var appB = await CreateApplicationAsync(dbContext);
        var agentA = await CreateActiveAgentAsync(dbContext, appA);
        var agentB = await CreateActiveAgentAsync(dbContext, appB);
        var runA = await CreateRunAsync(dbContext, appA, agentA);
        await CreateRunAsync(dbContext, appB, agentB);

        var service = CreateService(dbContext);
        var runs = await service.ListAsync(appA, status: null, agentId: null, createdBy: null, from: null, to: null);

        runs.Should().ContainSingle().Which.Id.Should().Be(runA.Id);
        runs[0].AgentName.Should().Be("Support Agent");
    }

    [Fact]
    public async Task ListAsync_filters_by_status_agent_and_creator()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appId = await CreateApplicationAsync(dbContext);
        var agent = await CreateActiveAgentAsync(dbContext, appId);
        var queuedRun = await CreateRunAsync(dbContext, appId, agent, requestedBy: "alice");

        var failingRun = await CreateRunAsync(dbContext, appId, agent, requestedBy: "bob");
        failingRun.Claim("worker-1", DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        failingRun.Fail(ErrorCategory.GenerationFailure, "boom", DateTimeOffset.UtcNow);
        var runRepository = new AgentRunRepository(dbContext);
        await runRepository.SaveChangesAsync();

        var service = CreateService(dbContext);

        var failedRuns = await service.ListAsync(appId, JobStatus.Failed, agentId: null, createdBy: null, from: null, to: null);
        var aliceRuns = await service.ListAsync(appId, status: null, agentId: null, createdBy: "alice", from: null, to: null);

        failedRuns.Should().ContainSingle().Which.Id.Should().Be(failingRun.Id);
        failedRuns[0].RetryEligible.Should().BeTrue();
        aliceRuns.Should().ContainSingle().Which.Id.Should().Be(queuedRun.Id);
    }

    [Fact]
    public async Task GetDetailAsync_returns_snapshot_evidence_and_linked_output()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appId = await CreateApplicationAsync(dbContext);
        var agent = await CreateActiveAgentAsync(dbContext, appId);
        var run = await CreateRunAsync(dbContext, appId, agent);

        var file = KnowledgeFile.Create(appId, "handbook.pdf", "hash-1", 1024, "application/pdf", "utf-8", Actor, DateTimeOffset.UtcNow);
        var fileRepository = new KnowledgeFileRepository(dbContext);
        await fileRepository.AddAsync(file);
        await fileRepository.SaveChangesAsync();

        var evidence = RunEvidence.Create(
            appId, run.Id, "SOURCE-001", Guid.NewGuid(), file.Id,
            "handbook.pdf", "Section 1 > Policy", "The relevant passage.", 100, 140, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var evidenceRepository = new RunEvidenceRepository(dbContext);
        await evidenceRepository.AddRangeAsync([evidence]);

        var output = GeneratedOutput.Create(appId, run.Id, "Q3 Summary", "# Body [SOURCE-001]", Actor, DateTimeOffset.UtcNow);
        var outputRepository = new GeneratedOutputRepository(dbContext);
        await outputRepository.AddAsync(output);
        await outputRepository.SaveChangesAsync();

        var service = CreateService(dbContext);
        var detail = await service.GetDetailAsync(appId, run.Id);

        detail.Id.Should().Be(run.Id);
        detail.AgentName.Should().Be("Support Agent");
        detail.AgentPromptSnapshot.Should().Be("You are a helpful support agent.");
        detail.CorrelationId.Should().NotBeNullOrWhiteSpace();
        detail.GeneratedOutputId.Should().Be(output.Id);
        detail.Evidence.Should().ContainSingle().Which.SourceId.Should().Be("SOURCE-001");
    }

    [Fact]
    public async Task GetDetailAsync_throws_when_the_run_belongs_to_a_different_application()
    {
        await using var dbContext = fixture.CreateDbContext();
        var appA = await CreateApplicationAsync(dbContext);
        var appB = await CreateApplicationAsync(dbContext);
        var agentA = await CreateActiveAgentAsync(dbContext, appA);
        var run = await CreateRunAsync(dbContext, appA, agentA);

        var service = CreateService(dbContext);
        var act = async () => await service.GetDetailAsync(appB, run.Id);

        await act.Should().ThrowAsync<AgentRunNotFoundException>();
    }
}
