using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Retrieval;
using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Citations;

/// <summary>TASK-CIT-02: unit tests for <see cref="RunEvidenceService"/>'s snapshotting + idempotent persistence.</summary>
public sealed class RunEvidenceServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class FakeRunEvidenceRepository : IRunEvidenceRepository
    {
        public List<RunEvidence> Stored { get; } = [];

        public Task<IReadOnlyList<RunEvidence>> ListByRunAsync(Guid agentRunId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<RunEvidence>>([.. Stored.Where(e => e.AgentRunId == agentRunId).OrderBy(e => e.SourceId)]);

        public Task AddRangeAsync(IReadOnlyList<RunEvidence> evidence, CancellationToken cancellationToken = default)
        {
            Stored.AddRange(evidence);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeKnowledgeFileRepository : IKnowledgeFileRepository
    {
        public List<KnowledgeFile> Files { get; } = [];

        public Task<KnowledgeFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Files.FirstOrDefault(f => f.Id == fileId));

        public Task AddAsync(KnowledgeFile file, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by RunEvidenceService tests.");

        public Task<IReadOnlyList<KnowledgeFile>> ListAsync(
            Guid applicationId, string? search = null, UserFacingFileStatus? status = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by RunEvidenceService tests.");

        public Task<bool> ExistsActiveHashAsync(Guid applicationId, string contentHash, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by RunEvidenceService tests.");

        public Task<IReadOnlyDictionary<UserFacingFileStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by RunEvidenceService tests.");

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by RunEvidenceService tests.");
    }

    private static KnowledgeFile CreateReadyFile(Guid applicationId, string displayName)
    {
        var file = KnowledgeFile.Create(applicationId, displayName, contentHash: Guid.NewGuid().ToString("N"), fileSize: 100, mimeType: "text/plain", encoding: "utf-8", uploadedBy: "tester", Now);
        file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/original");
        file.MarkProcessing(Now);
        file.MarkReady(Now);
        return file;
    }

    [Fact]
    public async Task PersistAsync_snapshots_the_files_display_name_and_last_processed_time()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var file = CreateReadyFile(applicationId, "handbook.pdf");
        var fileRepository = new FakeKnowledgeFileRepository();
        fileRepository.Files.Add(file);
        var evidenceRepository = new FakeRunEvidenceRepository();
        var service = new RunEvidenceService(evidenceRepository, fileRepository);

        var source = new EvidenceSource("SOURCE-001", file.Id, Guid.NewGuid(), "handbook.pdf", "Chapter 1", "PTO is 15 days.", 0, 16, 16, 0.9);

        var result = await service.PersistAsync(applicationId, agentRunId, [source], Now);

        result.Should().ContainSingle();
        result[0].SourceId.Should().Be("SOURCE-001");
        result[0].FileNameSnapshot.Should().Be("handbook.pdf");
        result[0].PassageSnapshot.Should().Be("PTO is 15 days.");
        result[0].ProcessedAtSnapshot.Should().Be(Now);
        evidenceRepository.Stored.Should().ContainSingle();
    }

    [Fact]
    public async Task PersistAsync_called_twice_for_the_same_run_does_not_duplicate_rows()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var file = CreateReadyFile(applicationId, "handbook.pdf");
        var fileRepository = new FakeKnowledgeFileRepository();
        fileRepository.Files.Add(file);
        var evidenceRepository = new FakeRunEvidenceRepository();
        var service = new RunEvidenceService(evidenceRepository, fileRepository);
        var source = new EvidenceSource("SOURCE-001", file.Id, Guid.NewGuid(), "handbook.pdf", "Chapter 1", "PTO is 15 days.", 0, 16, 16, 0.9);

        await service.PersistAsync(applicationId, agentRunId, [source], Now);
        var second = await service.PersistAsync(applicationId, agentRunId, [source], Now);

        evidenceRepository.Stored.Should().ContainSingle();
        second.Should().ContainSingle();
    }

    [Fact]
    public async Task PersistAsync_falls_back_to_the_evidence_sources_own_display_name_when_the_file_no_longer_exists()
    {
        var applicationId = Guid.NewGuid();
        var agentRunId = Guid.NewGuid();
        var fileRepository = new FakeKnowledgeFileRepository();
        var evidenceRepository = new FakeRunEvidenceRepository();
        var service = new RunEvidenceService(evidenceRepository, fileRepository);
        var source = new EvidenceSource("SOURCE-001", Guid.NewGuid(), Guid.NewGuid(), "deleted-file.pdf", "Chapter 1", "PTO is 15 days.", 0, 16, 16, 0.9);

        var result = await service.PersistAsync(applicationId, agentRunId, [source], Now);

        result[0].FileNameSnapshot.Should().Be("deleted-file.pdf");
        result[0].ProcessedAtSnapshot.Should().BeNull();
    }
}
