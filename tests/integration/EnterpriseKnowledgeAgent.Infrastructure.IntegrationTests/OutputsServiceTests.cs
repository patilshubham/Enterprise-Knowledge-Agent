using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-OUT-02/03/04/05 + TASK-CIT-04: integration tests for
/// <see cref="OutputsService"/> against a real Postgres container - mirrors
/// <see cref="AgentsServiceTests"/>'s convention of testing the
/// orchestration layer directly.
/// </summary>
public sealed class OutputsServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
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

    /// <summary>TASK-SIG-03/04: this test suite exercises the DB-persistence path only - realtime publishing is covered separately.</summary>
    private sealed class NoOpRealtimeEventPublisher : IRealtimeEventPublisher
    {
        public Task PublishAsync<TPayload>(string eventType, IReadOnlyCollection<string> groups, SignalREvent<TPayload> envelope, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private static OutputsService CreateService(EnterpriseKnowledgeAgentDbContext dbContext) => new(
        new GeneratedOutputRepository(dbContext),
        new AgentRunRepository(dbContext),
        new CitationRepository(dbContext),
        new RunEvidenceRepository(dbContext),
        new KnowledgeFileRepository(dbContext),
        CreateAgentRunSubmissionService(dbContext),
        TimeProvider.System);

    private static AgentRunSubmissionService CreateAgentRunSubmissionService(EnterpriseKnowledgeAgentDbContext dbContext) => new(
        new AgentRepository(dbContext),
        new AgentRunRepository(dbContext),
        new AgentParameterValidator(new FakeDeploymentRegistry()),
        new JobCreationService(new ProcessingJobRepository(dbContext), new AgentRunRepository(dbContext), new OutboxRepository(dbContext), new NoOpRealtimeEventPublisher(), TimeProvider.System));

    private static async Task<Guid> CreateApplicationAsync(EnterpriseKnowledgeAgentDbContext dbContext)
    {
        var app = ApplicationEntity.Create($"App {Guid.NewGuid()}", null, Actor, DateTimeOffset.UtcNow);
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();
        return app.Id;
    }

    private static async Task<Guid> CreateActiveAgentAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId)
    {
        var agentsService = new AgentsService(
            new AgentRepository(dbContext),
            new ApplicationReadOnlyGuard(new ApplicationRepository(dbContext)),
            new AgentParameterValidator(new FakeDeploymentRegistry()),
            TimeProvider.System);

        var created = await agentsService.CreateAsync(
            applicationId,
            new CreateAgentRequest(
                "Support Agent", "desc", "You are a helpful support agent.", "AllReadyEnabled", null,
                ChatDeployment, 0.7, 2048, null, "Hybrid", 10, 0.5, 8000,
                IncludeNeighbors: true, StrictGrounding: true, CitationsRequired: true),
            Actor);

        return created.Id;
    }

    private static async Task<AgentRun> CreateRunAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, Guid agentId, string userRequest = "Summarize the report")
    {
        var run = AgentRun.Create(
            applicationId, agentId, Actor, userRequest, Guid.NewGuid().ToString(), outputTitle: null,
            "Support Agent", "You are a helpful support agent.", knowledgeScopeSnapshotJson: null,
            deploymentSnapshot: ChatDeployment, requestedParametersJson: null, effectiveParametersJson: null,
            retrievalSettingsJson: null, CorrelationId.New(), DateTimeOffset.UtcNow);

        var repository = new AgentRunRepository(dbContext);
        await repository.AddAsync(run);
        await repository.SaveChangesAsync();
        return run;
    }

    private static async Task<GeneratedOutput> CreateOutputAsync(EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, Guid agentRunId, string title = "Q3 Summary")
    {
        var output = GeneratedOutput.Create(applicationId, agentRunId, title, "# Original\n\nBody with [SOURCE-001].", Actor, DateTimeOffset.UtcNow);
        var repository = new GeneratedOutputRepository(dbContext);
        await repository.AddAsync(output);
        await repository.SaveChangesAsync();
        return output;
    }

    private static async Task<(KnowledgeFile File, RunEvidence Evidence, Citation Citation)> CreateCitationChainAsync(
        EnterpriseKnowledgeAgentDbContext dbContext, Guid applicationId, Guid agentRunId, Guid generatedOutputId)
    {
        var file = KnowledgeFile.Create(applicationId, "handbook.pdf", "hash-1", 1024, "application/pdf", "utf-8", Actor, DateTimeOffset.UtcNow);
        var fileRepository = new KnowledgeFileRepository(dbContext);
        await fileRepository.AddAsync(file);
        await fileRepository.SaveChangesAsync();

        var evidence = RunEvidence.Create(
            applicationId, agentRunId, "SOURCE-001", Guid.NewGuid(), file.Id,
            "handbook.pdf", "Section 1 > Policy", "The relevant passage.", 100, 140, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var evidenceRepository = new RunEvidenceRepository(dbContext);
        await evidenceRepository.AddRangeAsync([evidence]);

        var citation = Citation.Create(applicationId, generatedOutputId, evidence.Id, DateTimeOffset.UtcNow);
        var citationRepository = new CitationRepository(dbContext);
        await citationRepository.AddRangeAsync([citation]);

        return (file, evidence, citation);
    }

    [Fact]
    public async Task GetDetailAsync_returns_the_output_with_its_run_snapshot_and_citations()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);
        await CreateCitationChainAsync(dbContext, applicationId, run.Id, output.Id);

        var service = CreateService(dbContext);
        var detail = await service.GetDetailAsync(applicationId, output.Id);

        detail.Id.Should().Be(output.Id);
        detail.Title.Should().Be("Q3 Summary");
        detail.AgentId.Should().Be(agentId);
        detail.AgentName.Should().Be("Support Agent");
        detail.DeploymentSnapshot.Should().Be(ChatDeployment);
        detail.Citations.Should().HaveCount(1);
        detail.Citations[0].SourceId.Should().Be("SOURCE-001");
        detail.Citations[0].Passage.Should().Be("The relevant passage.");
        detail.Citations[0].SourceUnavailable.Should().BeFalse();
    }

    [Fact]
    public async Task GetDetailAsync_throws_not_found_for_a_deleted_output()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        await service.DeleteAsync(applicationId, output.Id);

        var act = () => service.GetDetailAsync(applicationId, output.Id);
        await act.Should().ThrowAsync<OutputNotFoundException>();
    }

    [Fact]
    public async Task GetDetailAsync_throws_not_found_for_a_cross_application_output()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var otherApplicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        var act = () => service.GetDetailAsync(otherApplicationId, output.Id);

        await act.Should().ThrowAsync<OutputNotFoundException>();
    }

    [Fact]
    public async Task EditAsync_updates_current_markdown_and_leaves_original_untouched()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        var edited = await service.EditAsync(applicationId, output.Id, "# Edited\n\nNew body.", "keycloak-sub-2");

        edited.CurrentMarkdown.Should().Be("# Edited\n\nNew body.");
        edited.OriginalMarkdown.Should().Be("# Original\n\nBody with [SOURCE-001].");
        edited.IsEdited.Should().BeTrue();
        edited.LastEditedBy.Should().Be("keycloak-sub-2");
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        await service.DeleteAsync(applicationId, output.Id);
        var act = () => service.DeleteAsync(applicationId, output.Id);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ListAsync_maps_agent_name_from_the_owning_run()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        var results = await service.ListAsync(applicationId, search: null, agentId: null, createdBy: null, from: null, to: null, runStatus: null);

        results.Should().ContainSingle();
        results[0].AgentId.Should().Be(agentId);
        results[0].AgentName.Should().Be("Support Agent");
    }

    [Fact]
    public async Task GetCitationsAsync_flags_a_citation_as_source_unavailable_once_its_file_is_gone()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var evidence = RunEvidence.Create(
            applicationId, run.Id, "SOURCE-001", Guid.NewGuid(), Guid.NewGuid(),
            "deleted-file.pdf", "Section 1", "A passage from a now-deleted file.", 0, 30, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        await new RunEvidenceRepository(dbContext).AddRangeAsync([evidence]);
        var citation = Citation.Create(applicationId, output.Id, evidence.Id, DateTimeOffset.UtcNow);
        await new CitationRepository(dbContext).AddRangeAsync([citation]);

        var service = CreateService(dbContext);
        var citations = await service.GetCitationsAsync(applicationId, output.Id);
        var single = await service.GetCitationAsync(applicationId, output.Id, citation.Id);

        citations.Should().ContainSingle();
        citations[0].SourceUnavailable.Should().BeTrue();
        single.SourceUnavailable.Should().BeTrue();
        single.FileName.Should().Be("deleted-file.pdf");
    }

    [Fact]
    public async Task GetCitationAsync_throws_not_found_for_an_unknown_citation_id()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId);
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        var act = () => service.GetCitationAsync(applicationId, output.Id, Guid.NewGuid());

        await act.Should().ThrowAsync<CitationNotFoundException>();
    }

    [Fact]
    public async Task RegenerateAsync_creates_a_new_run_for_the_same_agent_and_leaves_the_original_untouched()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var agentId = await CreateActiveAgentAsync(dbContext, applicationId);
        var run = await CreateRunAsync(dbContext, applicationId, agentId, userRequest: "Summarize Q3 results");
        var output = await CreateOutputAsync(dbContext, applicationId, run.Id);

        var service = CreateService(dbContext);
        var result = await service.RegenerateAsync(applicationId, output.Id, "keycloak-sub-2");

        result.RunId.Should().NotBe(run.Id);

        var newRun = await new AgentRunRepository(dbContext).GetAsync(result.RunId);
        newRun.Should().NotBeNull();
        newRun!.AgentId.Should().Be(agentId);
        newRun.UserRequest.Should().Be("Summarize Q3 results");
        newRun.IdempotencyKey.Should().NotBe(run.IdempotencyKey);

        var originalStillThere = await new AgentRunRepository(dbContext).GetAsync(run.Id);
        originalStillThere.Should().NotBeNull();
        originalStillThere!.Id.Should().Be(run.Id);
    }
}
