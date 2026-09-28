using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-CIT-01/CIT-03: persistence tests for <see cref="CitationRepository"/> against a real Postgres container.</summary>
public sealed class CitationRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task AddRangeAsync_persists_a_citation_linked_to_its_run_evidence()
    {
        var applicationId = Guid.NewGuid();
        var generatedOutputId = Guid.NewGuid();
        var runEvidenceId = Guid.NewGuid();
        var citation = Citation.Create(applicationId, generatedOutputId, runEvidenceId, Now);

        await using (var writeContext = fixture.CreateDbContext())
        {
            await new CitationRepository(writeContext).AddRangeAsync([citation]);
        }

        await using var readContext = fixture.CreateDbContext();
        var stored = (await new CitationRepository(readContext).ListByOutputAsync(generatedOutputId)).Single();

        stored.ApplicationId.Should().Be(applicationId);
        stored.RunEvidenceId.Should().Be(runEvidenceId);
        stored.GeneratedOutputId.Should().Be(generatedOutputId);
    }

    [Fact]
    public async Task ListByOutputAsync_only_returns_citations_for_the_requested_output()
    {
        var applicationId = Guid.NewGuid();
        var outputA = Guid.NewGuid();
        var outputB = Guid.NewGuid();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new CitationRepository(writeContext);
            await repository.AddRangeAsync(
            [
                Citation.Create(applicationId, outputA, Guid.NewGuid(), Now),
                Citation.Create(applicationId, outputA, Guid.NewGuid(), Now),
                Citation.Create(applicationId, outputB, Guid.NewGuid(), Now),
            ]);
        }

        await using var readContext = fixture.CreateDbContext();
        var forOutputA = await new CitationRepository(readContext).ListByOutputAsync(outputA);

        forOutputA.Should().HaveCount(2);
        forOutputA.Should().OnlyContain(c => c.GeneratedOutputId == outputA);
    }
}
