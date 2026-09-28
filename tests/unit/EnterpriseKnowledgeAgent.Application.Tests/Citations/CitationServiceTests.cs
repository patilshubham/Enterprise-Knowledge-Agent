using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Domain.Citations;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Citations;

/// <summary>TASK-CIT-03: unit tests for <see cref="CitationService"/>'s validate-then-persist orchestration and idempotency.</summary>
public sealed class CitationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakeCitationRepository : ICitationRepository
    {
        public List<Citation> Stored { get; } = [];

        public Task<IReadOnlyList<Citation>> ListByOutputAsync(Guid generatedOutputId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Citation>>([.. Stored.Where(c => c.GeneratedOutputId == generatedOutputId)]);

        public Task AddRangeAsync(IReadOnlyList<Citation> citations, CancellationToken cancellationToken = default)
        {
            Stored.AddRange(citations);
            return Task.CompletedTask;
        }
    }

    private static RunEvidence Evidence(Guid applicationId, Guid agentRunId, string sourceId) =>
        RunEvidence.Create(
            applicationId,
            agentRunId,
            sourceId,
            knowledgeChunkId: Guid.NewGuid(),
            knowledgeFileId: Guid.NewGuid(),
            fileNameSnapshot: "handbook.pdf",
            headingPathSnapshot: "Chapter 1",
            passageSnapshot: "Employees accrue 15 days of PTO per year.",
            characterStartSnapshot: 0,
            characterEndSnapshot: 42,
            processedAtSnapshot: Now,
            now: Now);

    [Fact]
    public async Task ValidateAndPersistAsync_persists_a_citation_per_valid_marker()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var evidence = Evidence(applicationId, agentRunId, "SOURCE-001");
        var repository = new FakeCitationRepository();
        var service = new CitationService(repository);

        var result = await service.ValidateAndPersistAsync(applicationId, outputId, ["[SOURCE-001]"], [evidence], Now);

        result.Citations.Should().ContainSingle();
        repository.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task ValidateAndPersistAsync_called_twice_for_the_same_output_does_not_duplicate_rows()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var outputId = Guid.NewGuid();
        var evidence = Evidence(applicationId, agentRunId, "SOURCE-001");
        var repository = new FakeCitationRepository();
        var service = new CitationService(repository);

        await service.ValidateAndPersistAsync(applicationId, outputId, ["[SOURCE-001]"], [evidence], Now);
        var second = await service.ValidateAndPersistAsync(applicationId, outputId, ["[SOURCE-001]"], [evidence], Now);

        repository.Stored.Should().ContainSingle();
        second.Citations.Should().ContainSingle();
    }

    [Fact]
    public async Task ValidateAndPersistAsync_does_not_persist_invalid_markers()
    {
        var applicationId = Guid.NewGuid();
        var repository = new FakeCitationRepository();
        var service = new CitationService(repository);

        var result = await service.ValidateAndPersistAsync(applicationId, Guid.NewGuid(), ["[SOURCE-001]"], [], Now);

        result.Citations.Should().BeEmpty();
        result.InvalidMarkers.Should().Equal("[SOURCE-001]");
        repository.Stored.Should().BeEmpty();
    }
}
