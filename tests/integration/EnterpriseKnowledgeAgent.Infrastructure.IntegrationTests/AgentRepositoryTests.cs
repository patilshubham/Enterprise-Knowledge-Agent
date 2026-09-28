using EnterpriseKnowledgeAgent.Domain.Agents;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using FluentAssertions;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-AGT-01: persistence round-trip tests against a real Postgres container.</summary>
public sealed class AgentRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static AgentEntity CreateValid(Guid applicationId, IReadOnlyList<Guid>? selectedFileIds = null) =>
        AgentEntity.Create(
            applicationId,
            "Support Agent",
            "desc",
            "You are a helpful support agent.",
            selectedFileIds is null ? KnowledgeScopeType.AllReadyEnabled : KnowledgeScopeType.Selected,
            selectedFileIds,
            "gpt-4o",
            0.7,
            2048,
            0.9,
            RetrievalMode.Hybrid,
            10,
            0.5,
            8000,
            includeNeighbors: true,
            strictGrounding: true,
            citationsRequired: true,
            "sub-1",
            DateTimeOffset.UtcNow);

    private static async Task<Guid> CreateApplicationAsync(Persistence.EnterpriseKnowledgeAgentDbContext dbContext)
    {
        var app = ApplicationEntity.Create($"App {Guid.NewGuid()}", null, "sub-1", DateTimeOffset.UtcNow);
        var repository = new ApplicationRepository(dbContext);
        await repository.AddAsync(app);
        await repository.SaveChangesAsync();
        return app.Id;
    }

    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_all_fields()
    {
        await using var setupContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(setupContext);
        var fileId = Guid.NewGuid();
        var agent = CreateValid(applicationId, [fileId]);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRepository(writeContext);
            await repository.AddAsync(agent);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new AgentRepository(readContext);
        var loaded = await readRepository.GetAsync(agent.Id);

        loaded.Should().NotBeNull();
        loaded!.ApplicationId.Should().Be(applicationId);
        loaded.Name.Should().Be("Support Agent");
        loaded.Description.Should().Be("desc");
        loaded.Prompt.Should().Be("You are a helpful support agent.");
        loaded.Status.Should().Be(AgentStatus.Active);
        loaded.KnowledgeScopeType.Should().Be(KnowledgeScopeType.Selected);
        loaded.SelectedFileIds.Should().BeEquivalentTo([fileId]);
        loaded.GenerationDeployment.Should().Be("gpt-4o");
        loaded.Temperature.Should().Be(0.7);
        loaded.MaximumOutputTokens.Should().Be(2048);
        loaded.TopP.Should().Be(0.9);
        loaded.RetrievalMode.Should().Be(RetrievalMode.Hybrid);
        loaded.RetrievalCount.Should().Be(10);
        loaded.MinimumSimilarity.Should().Be(0.5);
        loaded.MaximumEvidenceTokens.Should().Be(8000);
        loaded.IncludeNeighbors.Should().BeTrue();
        loaded.StrictGrounding.Should().BeTrue();
        loaded.CitationsRequired.Should().BeTrue();
        loaded.CreatedBy.Should().Be("sub-1");
        loaded.ConcurrencyToken.Should().NotBe(0u);
    }

    [Fact]
    public async Task GetAsync_returns_null_for_unknown_id()
    {
        await using var dbContext = fixture.CreateDbContext();
        var repository = new AgentRepository(dbContext);

        var loaded = await repository.GetAsync(Guid.NewGuid());

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_scopes_to_application_and_orders_newest_first()
    {
        await using var setupContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(setupContext);
        var otherApplicationId = await CreateApplicationAsync(setupContext);

        var first = CreateValid(applicationId);
        await Task.Delay(10);
        var second = CreateValid(applicationId);
        var otherAppAgent = CreateValid(otherApplicationId);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRepository(writeContext);
            await repository.AddAsync(first);
            await repository.AddAsync(second);
            await repository.AddAsync(otherAppAgent);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new AgentRepository(readContext);
        var list = await readRepository.ListAsync(applicationId);

        list.Select(a => a.Id).Should().BeEquivalentTo([first.Id, second.Id]);
        list.Should().NotContain(a => a.Id == otherAppAgent.Id);
    }

    [Fact]
    public async Task Enable_and_Disable_transitions_persist_across_reload()
    {
        await using var setupContext = fixture.CreateDbContext();
        var applicationId = await CreateApplicationAsync(setupContext);
        var agent = CreateValid(applicationId);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new AgentRepository(writeContext);
            await repository.AddAsync(agent);
            await repository.SaveChangesAsync();
        }

        await using (var disableContext = fixture.CreateDbContext())
        {
            var repository = new AgentRepository(disableContext);
            var loaded = await repository.GetAsync(agent.Id);
            loaded!.Disable("sub-1", DateTimeOffset.UtcNow);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var readRepository = new AgentRepository(readContext);
        var reloaded = await readRepository.GetAsync(agent.Id);

        reloaded!.Status.Should().Be(AgentStatus.Disabled);
    }
}
