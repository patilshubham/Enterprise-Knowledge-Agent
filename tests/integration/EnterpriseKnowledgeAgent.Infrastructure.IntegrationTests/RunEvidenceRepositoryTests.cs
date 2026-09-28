using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-CIT-01/CIT-02: persistence + integrity tests for <see cref="RunEvidenceRepository"/> against a real Postgres container.</summary>
public sealed class RunEvidenceRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    private static RunEvidence NewEvidence(Guid applicationId, Guid agentRunId, string sourceId) =>
        RunEvidence.Create(
            applicationId,
            agentRunId,
            sourceId,
            knowledgeChunkId: Guid.NewGuid(),
            knowledgeFileId: Guid.NewGuid(),
            fileNameSnapshot: "handbook.pdf",
            headingPathSnapshot: "Chapter 1 > Section 1.1",
            passageSnapshot: "Employees accrue 15 days of PTO per year.",
            characterStartSnapshot: 100,
            characterEndSnapshot: 142,
            processedAtSnapshot: Now,
            now: Now);

    [Fact]
    public async Task AddRangeAsync_persists_all_snapshot_fields()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var evidence = NewEvidence(applicationId, agentRunId, "SOURCE-001");

        await using (var writeContext = fixture.CreateDbContext())
        {
            await new RunEvidenceRepository(writeContext).AddRangeAsync([evidence]);
        }

        await using var readContext = fixture.CreateDbContext();
        var stored = (await new RunEvidenceRepository(readContext).ListByRunAsync(agentRunId)).Single();

        stored.SourceId.Should().Be("SOURCE-001");
        stored.ApplicationId.Should().Be(applicationId);
        stored.KnowledgeChunkId.Should().Be(evidence.KnowledgeChunkId);
        stored.KnowledgeFileId.Should().Be(evidence.KnowledgeFileId);
        stored.FileNameSnapshot.Should().Be("handbook.pdf");
        stored.HeadingPathSnapshot.Should().Be("Chapter 1 > Section 1.1");
        stored.PassageSnapshot.Should().Be("Employees accrue 15 days of PTO per year.");
        stored.CharacterStartSnapshot.Should().Be(100);
        stored.CharacterEndSnapshot.Should().Be(142);
        stored.ProcessedAtSnapshot.Should().BeCloseTo(Now, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ListByRunAsync_only_returns_evidence_for_the_requested_run_ordered_by_source_id()
    {
        var applicationId = Guid.NewGuid();
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new RunEvidenceRepository(writeContext);
            await repository.AddRangeAsync(
            [
                NewEvidence(applicationId, runA, "SOURCE-002"),
                NewEvidence(applicationId, runA, "SOURCE-001"),
                NewEvidence(applicationId, runB, "SOURCE-001"),
            ]);
        }

        await using var readContext = fixture.CreateDbContext();
        var forRunA = await new RunEvidenceRepository(readContext).ListByRunAsync(runA);

        forRunA.Select(e => e.SourceId).Should().Equal("SOURCE-001", "SOURCE-002");
    }

    [Fact]
    public async Task AddRangeAsync_rejects_a_duplicate_source_id_within_the_same_run()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();

        await using var dbContext = fixture.CreateDbContext();
        var repository = new RunEvidenceRepository(dbContext);
        await repository.AddRangeAsync([NewEvidence(applicationId, agentRunId, "SOURCE-001")]);

        var act = () => repository.AddRangeAsync([NewEvidence(applicationId, agentRunId, "SOURCE-001")]);

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
