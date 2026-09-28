using EnterpriseKnowledgeAgent.Application.Dashboards;
using EnterpriseKnowledgeAgent.Domain.Agents;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-ADSH-01: end-to-end aggregation tests for <see cref="ApplicationDashboardService"/> against a real Postgres container - every dependency is a real repository.</summary>
public sealed class ApplicationDashboardServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static ApplicationDashboardService CreateService(Persistence.EnterpriseKnowledgeAgentDbContext dbContext) =>
        new(
            new ApplicationRepository(dbContext),
            new KnowledgeFileRepository(dbContext),
            new ProcessingJobRepository(dbContext),
            new AgentRunRepository(dbContext),
            new AgentRepository(dbContext),
            new GeneratedOutputRepository(dbContext));

    [Fact]
    public async Task GetAsync_unknown_application_throws_not_found()
    {
        await using var dbContext = fixture.CreateDbContext();
        var service = CreateService(dbContext);

        var act = () => service.GetAsync(Guid.NewGuid());

        await act.Should().ThrowAsync<Application.Applications.ApplicationNotFoundException>();
    }

    [Fact]
    public async Task GetAsync_aggregates_counts_scoped_to_the_application()
    {
        var now = DateTimeOffset.UtcNow;
        var application = ApplicationEntity.Create($"App {Guid.NewGuid()}", null, "sub-1", now);
        var otherApplication = ApplicationEntity.Create($"Other {Guid.NewGuid()}", null, "sub-1", now);

        var file = KnowledgeFile.Create(application.Id, "notes.txt", $"hash-{Guid.NewGuid()}", 10, "text/plain", "utf-8", "sub-1", now);
        var otherAppFile = KnowledgeFile.Create(otherApplication.Id, "other.txt", $"hash-{Guid.NewGuid()}", 10, "text/plain", "utf-8", "sub-1", now);

        var job = ProcessingJob.Create(application.Id, file.Id, CorrelationId.New(), now);

        var activeAgent = Agent.Create(
            application.Id, "Agent A", null, "prompt", Shared.Agents.KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.2, 800, null, Shared.Retrieval.RetrievalMode.Hybrid, 5, 0.5, 4000, true, false, true, "sub-1", now);
        var disabledAgent = Agent.Create(
            application.Id, "Agent B", null, "prompt", Shared.Agents.KnowledgeScopeType.AllReadyEnabled, null,
            "gpt-4o", 0.2, 800, null, Shared.Retrieval.RetrievalMode.Hybrid, 5, 0.5, 4000, true, false, true, "sub-1", now);
        disabledAgent.Disable("sub-1", now);

        var run = AgentRun.Create(
            application.Id, activeAgent.Id, "sub-1", "Summarize", Guid.NewGuid().ToString(), null,
            "Agent A", "prompt", null, "gpt-4o", null, null, null, CorrelationId.New(), now);

        var output = GeneratedOutput.Create(application.Id, run.Id, "Summary", "Body.", "sub-1", now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            await new ApplicationRepository(writeContext).AddAsync(application);
            await new ApplicationRepository(writeContext).AddAsync(otherApplication);
            await new KnowledgeFileRepository(writeContext).AddAsync(file);
            await new KnowledgeFileRepository(writeContext).AddAsync(otherAppFile);
            await new ProcessingJobRepository(writeContext).AddAsync(job);
            await new AgentRepository(writeContext).AddAsync(activeAgent);
            await new AgentRepository(writeContext).AddAsync(disabledAgent);
            await new AgentRunRepository(writeContext).AddAsync(run);
            await new GeneratedOutputRepository(writeContext).AddAsync(output);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var service = CreateService(readContext);

        var dashboard = await service.GetAsync(application.Id);

        dashboard.ApplicationId.Should().Be(application.Id);
        dashboard.ApplicationName.Should().Be(application.Name);
        dashboard.KnowledgeFiles.Queued.Should().Be(1);
        dashboard.ProcessingJobs.Queued.Should().Be(1);
        dashboard.AgentRuns.Queued.Should().Be(1);
        dashboard.ActiveAgents.Should().Be(1);
        dashboard.TotalAgents.Should().Be(2);
        dashboard.GeneratedOutputs.Should().Be(1);
        dashboard.AttentionItems.Should().BeEmpty();
    }
}
