using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Domain.Audit;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-AUD-01/03: audit event write/search tests against a real Postgres container.</summary>
public sealed class AuditEventRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static AuditEventRepository CreateRepository(Persistence.EnterpriseKnowledgeAgentDbContext dbContext) =>
        new(dbContext, TimeProvider.System, NullLogger<AuditEventRepository>.Instance);

    [Fact]
    public async Task WriteAsync_persists_a_retrievable_audit_event()
    {
        var applicationId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        await using (var dbContext = fixture.CreateDbContext())
        {
            await CreateRepository(dbContext).WriteAsync(
                AuditActions.ApplicationCreated,
                "actor-1",
                applicationId: applicationId,
                entityType: "Application",
                entityId: applicationId.ToString(),
                entityDisplayName: "Contoso Onboarding",
                correlationId: correlationId);
        }

        await using var readContext = fixture.CreateDbContext();
        var result = await CreateRepository(readContext).SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId));

        result.TotalCount.Should().Be(1);
        var written = result.Items.Single();
        written.Action.Should().Be(AuditActions.ApplicationCreated);
        written.ActorSubject.Should().Be("actor-1");
        written.Outcome.Should().Be(AuditOutcome.Success);
        written.EntityDisplayName.Should().Be("Contoso Onboarding");
        written.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public async Task WriteAsync_never_throws_even_when_the_underlying_write_fails()
    {
        // Action has a 100-char max length (AuditEventConfiguration) - a
        // longer value fails at the database (Postgres varchar truncation
        // error), exercising the "audit write failures never block the
        // primary action" requirement (TASK-AUD-01).
        var tooLongAction = new string('a', 500);

        await using var dbContext = fixture.CreateDbContext();
        var act = async () => await CreateRepository(dbContext).WriteAsync(tooLongAction, "actor-1");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SearchAsync_filters_by_application_actor_action_outcome_and_date_range()
    {
        var applicationId = Guid.NewGuid();
        var otherApplicationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var dbContext = fixture.CreateDbContext())
        {
            var repository = CreateRepository(dbContext);
            await repository.WriteAsync(AuditActions.KnowledgeFileUploaded, "actor-a", applicationId: applicationId);
            await repository.WriteAsync(AuditActions.KnowledgeFileDeleted, "actor-b", applicationId: applicationId);
            await repository.WriteAsync(AuditActions.KnowledgeFileUploaded, "actor-a", AuditOutcome.Failure, applicationId: applicationId, failureReason: "Rejected");
            await repository.WriteAsync(AuditActions.KnowledgeFileUploaded, "actor-a", applicationId: otherApplicationId);
        }

        await using var readContext = fixture.CreateDbContext();
        var repo = CreateRepository(readContext);

        (await repo.SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId))).TotalCount.Should().Be(3);
        (await repo.SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId, ActorSubject: "actor-a"))).TotalCount.Should().Be(2);
        (await repo.SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId, Action: AuditActions.KnowledgeFileDeleted))).TotalCount.Should().Be(1);
        (await repo.SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId, Outcome: AuditOutcome.Failure))).TotalCount.Should().Be(1);
        (await repo.SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId, From: now.AddDays(1)))).TotalCount.Should().Be(0);
        (await repo.SearchAsync(new AuditEventSearchFilter(ApplicationId: otherApplicationId))).TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task SearchAsync_paginates_results_ordered_by_most_recent_first()
    {
        var applicationId = Guid.NewGuid();

        await using (var dbContext = fixture.CreateDbContext())
        {
            var repository = CreateRepository(dbContext);
            for (var i = 0; i < 5; i++)
            {
                await repository.WriteAsync(AuditActions.AgentRunSubmitted, $"actor-{i}", applicationId: applicationId);
            }
        }

        await using var readContext = fixture.CreateDbContext();
        var page1 = await CreateRepository(readContext).SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId, Page: 1, PageSize: 2));
        var page2 = await CreateRepository(readContext).SearchAsync(new AuditEventSearchFilter(ApplicationId: applicationId, Page: 2, PageSize: 2));

        page1.TotalCount.Should().Be(5);
        page1.Items.Should().HaveCount(2);
        page2.Items.Should().HaveCount(2);
        page1.Items.Select(i => i.Id).Should().NotIntersectWith(page2.Items.Select(i => i.Id));
    }
}
