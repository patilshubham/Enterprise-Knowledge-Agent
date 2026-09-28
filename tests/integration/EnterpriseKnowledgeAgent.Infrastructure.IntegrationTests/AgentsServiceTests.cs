using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Domain.Agents;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-AGT-02: CRUD + lifecycle integration tests for
/// <see cref="AgentsService"/> against a real Postgres container - mirrors
/// <see cref="ApplicationsServiceTests"/>'s convention of testing the
/// orchestration layer directly rather than through a full HTTP harness
/// (see that class's doc comment for the rationale). There is no
/// login/authorization in this app, so there is no role-based prompt
/// redaction any more - <see cref="AgentsService.GetAsync"/>/
/// <see cref="AgentsService.ListAsync"/> always include the prompt.
/// </summary>
public sealed class AgentsServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private const string Actor = "keycloak-sub-1";
    private const string ChatDeployment = "gpt-4o";
    private const string NoTopPDeployment = "o1-mini";

    private sealed class FakeDeploymentRegistry : IDeploymentRegistry
    {
        public IReadOnlyList<DeploymentInfo> ListDeployments() =>
        [
            new DeploymentInfo(ChatDeployment, DeploymentPurpose.Chat, 128_000, 4_096, EmbeddingDimensions: null, SupportsTopP: true),
            new DeploymentInfo(NoTopPDeployment, DeploymentPurpose.Chat, 128_000, 65_536, EmbeddingDimensions: null, SupportsTopP: false),
            new DeploymentInfo("text-embedding-3-small", DeploymentPurpose.Embedding, null, null, 1536),
        ];
    }

    private static AgentsService CreateService(Persistence.EnterpriseKnowledgeAgentDbContext dbContext) => new(
        new Persistence.Repositories.AgentRepository(dbContext),
        new ApplicationReadOnlyGuard(new Persistence.Repositories.ApplicationRepository(dbContext)),
        new AgentParameterValidator(new FakeDeploymentRegistry()),
        TimeProvider.System);

    private static CreateAgentRequest ValidCreateRequest(string? generationDeployment = null, double? topP = null) => new(
        "Support Agent",
        "desc",
        "You are a helpful support agent.",
        "AllReadyEnabled",
        null,
        generationDeployment ?? ChatDeployment,
        0.7,
        2048,
        topP,
        "Hybrid",
        10,
        0.5,
        8000,
        IncludeNeighbors: true,
        StrictGrounding: true,
        CitationsRequired: true);

    private static async Task<Guid> CreateApplicationAsync(Persistence.EnterpriseKnowledgeAgentDbContext dbContext)
    {
        var app = ApplicationEntity.Create($"App {Guid.NewGuid()}", null, Actor, DateTimeOffset.UtcNow);
        var repository = new Persistence.Repositories.ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();
        return app.Id;
    }

    [Fact]
    public async Task CreateAsync_persists_new_agent()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);

        var created = await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        created.Name.Should().Be("Support Agent");
        created.ApplicationId.Should().Be(applicationId);
        created.Status.Should().Be("Active");
        created.Prompt.Should().Be("You are a helpful support agent.");
        created.CreatedBy.Should().Be(Actor);
    }

    [Fact]
    public async Task CreateAsync_unknown_deployment_throws_validation_exception()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(applicationId, ValidCreateRequest(generationDeployment: "not-configured"), Actor);

        await act.Should().ThrowAsync<AgentValidationException>();
    }

    [Fact]
    public async Task CreateAsync_topP_on_unsupported_deployment_throws_validation_exception()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(applicationId, ValidCreateRequest(generationDeployment: NoTopPDeployment, topP: 0.9), Actor);

        await act.Should().ThrowAsync<AgentValidationException>();
    }

    [Fact]
    public async Task CreateAsync_on_archived_application_throws_read_only_exception()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var appRepository = new Persistence.Repositories.ApplicationRepository(dbContext);
        var app = await appRepository.GetAsync(applicationId);
        app!.Archive(DateTimeOffset.UtcNow);
        await appRepository.SaveChangesAsync();
        var service = CreateService(dbContext);

        var act = () => service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        await act.Should().ThrowAsync<ApplicationReadOnlyException>();
    }

    [Fact]
    public async Task GetAsync_includes_prompt()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        var result = await service.GetAsync(created.Id);

        result.Prompt.Should().NotBeNull();
    }

    [Fact]
    public async Task ListAsync_includes_prompt_for_every_agent()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);
        await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        var list = await service.ListAsync(applicationId);

        list.Should().OnlyContain(a => a.Prompt != null);
    }

    [Fact]
    public async Task GetAsync_unknown_id_throws_not_found()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);

        var act = () => service.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<AgentNotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_overwrites_configuration()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        var updateRequest = new UpdateAgentRequest(
            "Renamed", "new desc", "new prompt", "AllReadyEnabled", null, ChatDeployment,
            1.0, 512, null, "Semantic", 3, 0.2, 2000, false, false, false);
        var updated = await service.UpdateAsync(applicationId, created.Id, updateRequest, "sub-2");

        updated.Name.Should().Be("Renamed");
        updated.Prompt.Should().Be("new prompt");
        updated.RetrievalMode.Should().Be("Semantic");
        updated.UpdatedBy.Should().Be("sub-2");
    }

    [Fact]
    public async Task EnableAsync_then_DisableAsync_round_trips_status()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        var disabled = await service.DisableAsync(applicationId, created.Id, Actor);
        disabled.Status.Should().Be("Disabled");

        var enabled = await service.EnableAsync(applicationId, created.Id, Actor);
        enabled.Status.Should().Be("Active");
    }

    [Fact]
    public async Task DisableAsync_already_disabled_throws_state_conflict()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);
        await service.DisableAsync(applicationId, created.Id, Actor);

        var act = () => service.DisableAsync(applicationId, created.Id, Actor);

        await act.Should().ThrowAsync<AgentStateConflictException>();
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent()
    {
        await using var dbContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(dbContext);
        var service = CreateService(dbContext);
        var created = await service.CreateAsync(applicationId, ValidCreateRequest(), Actor);

        var first = await service.DeleteAsync(applicationId, created.Id, Actor);
        var second = await service.DeleteAsync(applicationId, created.Id, Actor);

        first.Status.Should().Be("Deleted");
        second.Status.Should().Be("Deleted");
    }
}
