using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-OUT-01: persistence tests for <see cref="GeneratedOutputRepository"/> against a real Postgres container.</summary>
public sealed class GeneratedOutputRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static AgentRun NewRun(Guid applicationId, Guid agentId) =>
        AgentRun.Create(
            applicationId,
            agentId,
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
            Now);

    [Fact]
    public async Task AddAsync_persists_the_output_and_GetAsync_returns_it()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var output = GeneratedOutput.Create(applicationId, agentRunId, "Q3 Summary", "# Original\n\nBody.", "keycloak-sub-1", Now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new GeneratedOutputRepository(writeContext);
            await repository.AddAsync(output);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var stored = await new GeneratedOutputRepository(readContext).GetAsync(output.Id);

        stored.Should().NotBeNull();
        stored!.ApplicationId.Should().Be(applicationId);
        stored.AgentRunId.Should().Be(agentRunId);
        stored.Title.Should().Be("Q3 Summary");
        stored.OriginalMarkdown.Should().Be("# Original\n\nBody.");
        stored.CurrentMarkdown.Should().Be(stored.OriginalMarkdown);
        stored.IsEdited.Should().BeFalse();
    }

    [Fact]
    public async Task GetByAgentRunAsync_finds_the_output_for_that_run_and_returns_null_for_an_unrelated_run()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var output = GeneratedOutput.Create(applicationId, agentRunId, "Q3 Summary", "Body.", "keycloak-sub-1", Now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new GeneratedOutputRepository(writeContext);
            await repository.AddAsync(output);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var repositoryForRead = new GeneratedOutputRepository(readContext);

        var found = await repositoryForRead.GetByAgentRunAsync(agentRunId);
        var notFound = await repositoryForRead.GetByAgentRunAsync(Guid.NewGuid());

        found.Should().NotBeNull();
        found!.Id.Should().Be(output.Id);
        notFound.Should().BeNull();
    }

    [Fact]
    public async Task ListAsync_scopes_by_application_excludes_deleted_and_orders_newest_first()
    {
        var applicationId = Guid.NewGuid();
        var older = GeneratedOutput.Create(applicationId, Guid.NewGuid(), "Older", "Body.", "keycloak-sub-1", Now.AddHours(-2));
        var newer = GeneratedOutput.Create(applicationId, Guid.NewGuid(), "Newer", "Body.", "keycloak-sub-1", Now.AddHours(-1));
        var deleted = GeneratedOutput.Create(applicationId, Guid.NewGuid(), "Deleted", "Body.", "keycloak-sub-1", Now);
        deleted.SoftDelete();
        var otherApp = GeneratedOutput.Create(Guid.NewGuid(), Guid.NewGuid(), "Other app", "Body.", "keycloak-sub-1", Now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new GeneratedOutputRepository(writeContext);
            await repository.AddAsync(older);
            await repository.AddAsync(newer);
            await repository.AddAsync(deleted);
            await repository.AddAsync(otherApp);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var results = await new GeneratedOutputRepository(readContext).ListAsync(applicationId);

        results.Select(r => r.Title).Should().Equal("Newer", "Older");
    }

    [Fact]
    public async Task ListAsync_filters_by_search_created_by_and_date_range()
    {
        var applicationId = Guid.NewGuid();
        var alpha = GeneratedOutput.Create(applicationId, Guid.NewGuid(), "Alpha Report", "Body.", "user-a", Now.AddDays(-5));
        var beta = GeneratedOutput.Create(applicationId, Guid.NewGuid(), "Beta Summary", "Body.", "user-b", Now.AddDays(-1));

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new GeneratedOutputRepository(writeContext);
            await repository.AddAsync(alpha);
            await repository.AddAsync(beta);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var repositoryForRead = new GeneratedOutputRepository(readContext);

        var bySearch = await repositoryForRead.ListAsync(applicationId, search: "alpha");
        var byCreator = await repositoryForRead.ListAsync(applicationId, createdBy: "user-b");
        var byDateRange = await repositoryForRead.ListAsync(applicationId, from: Now.AddDays(-2), to: Now);

        bySearch.Select(r => r.Title).Should().Equal("Alpha Report");
        byCreator.Select(r => r.Title).Should().Equal("Beta Summary");
        byDateRange.Select(r => r.Title).Should().Equal("Beta Summary");
    }

    [Fact]
    public async Task ListAsync_filters_by_agent_id_and_run_status_via_the_owning_run()
    {
        var applicationId = Guid.NewGuid();
        var targetAgentId = Guid.NewGuid();
        var targetRun = NewRun(applicationId, targetAgentId);
        var otherRun = NewRun(applicationId, Guid.NewGuid());
        var forTargetAgent = GeneratedOutput.Create(applicationId, targetRun.Id, "For target agent", "Body.", "keycloak-sub-1", Now);
        var forOtherAgent = GeneratedOutput.Create(applicationId, otherRun.Id, "For other agent", "Body.", "keycloak-sub-1", Now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var agentRunRepository = new AgentRunRepository(writeContext);
            await agentRunRepository.AddAsync(targetRun);
            await agentRunRepository.AddAsync(otherRun);
            await agentRunRepository.SaveChangesAsync();

            var outputRepository = new GeneratedOutputRepository(writeContext);
            await outputRepository.AddAsync(forTargetAgent);
            await outputRepository.AddAsync(forOtherAgent);
            await outputRepository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var byAgent = await new GeneratedOutputRepository(readContext).ListAsync(applicationId, agentId: targetAgentId);
        var byStatus = await new GeneratedOutputRepository(readContext).ListAsync(applicationId, runStatus: JobStatus.Queued);
        var byMismatchedStatus = await new GeneratedOutputRepository(readContext).ListAsync(applicationId, runStatus: JobStatus.Completed);

        byAgent.Select(r => r.Title).Should().Equal("For target agent");
        byStatus.Select(r => r.Title).Should().BeEquivalentTo(["For target agent", "For other agent"]);
        byMismatchedStatus.Should().BeEmpty();
    }
}
