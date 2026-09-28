using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Processing;

/// <summary>TASK-REP-03: unit tests for the deletion-cleanup consumer, using in-memory fakes for every dependency.</summary>
public sealed class DeletionOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private sealed class Harness
    {
        public FakeKnowledgeFileRepository FileRepository { get; } = new();
        public FakeKnowledgeChunkRepository ChunkRepository { get; } = new();
        public FakeObjectStorage ObjectStorage { get; } = new();

        public DeletionOrchestrator BuildOrchestrator() => new(
            FileRepository,
            ChunkRepository,
            ObjectStorage,
            TimeProvider.System,
            NullLogger<DeletionOrchestrator>.Instance);

        public KnowledgeFile SeedReadyFile()
        {
            var applicationId = Guid.NewGuid();
            var file = KnowledgeFile.Create(applicationId, "notes.txt", "hash-1", 100, "text/plain", "utf-8", "sub", Now);
            file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/source");
            file.MarkProcessing(Now);
            file.MarkReady(Now);
            FileRepository.Files[file.Id] = file;

            var chunk = KnowledgeChunk.Create(applicationId, file.Id, 0, string.Empty, "text", 1, 0, 4, "hash", Now);
            chunk.Activate();
            ChunkRepository.Chunks.Add(chunk);

            return file;
        }
    }

    [Fact]
    public async Task ProcessAsync_deletes_storage_and_chunks_and_marks_the_file_Deleted()
    {
        var harness = new Harness();
        var file = harness.SeedReadyFile();
        var message = new DeletionMessage(Guid.NewGuid(), file.ApplicationId, file.Id, CorrelationId.New(), 1);

        await harness.BuildOrchestrator().ProcessAsync(message);

        file.Status.Should().Be(UserFacingFileStatus.Deleted);
        file.DeletedAt.Should().NotBeNull();
        harness.ObjectStorage.DeletedKeys.Should().Contain(file.ObjectKey);
        harness.ChunkRepository.Chunks.Should().NotContain(c => c.FileId == file.Id);
    }

    [Fact]
    public async Task ProcessAsync_also_deletes_the_in_flight_replacement_object_when_present()
    {
        var harness = new Harness();
        var file = harness.SeedReadyFile();
        file.BeginReplacement($"applications/{file.ApplicationId}/files/{file.Id}/replacement/r1", "hash-2", 50, "text/plain", "utf-8", Now);
        var replacementObjectKey = file.ReplacementObjectKey!;
        var message = new DeletionMessage(Guid.NewGuid(), file.ApplicationId, file.Id, CorrelationId.New(), 1);

        await harness.BuildOrchestrator().ProcessAsync(message);

        harness.ObjectStorage.DeletedKeys.Should().Contain(replacementObjectKey);
        harness.ObjectStorage.DeletedKeys.Should().Contain(file.ObjectKey);
    }

    [Fact]
    public async Task ProcessAsync_is_a_no_op_when_the_file_no_longer_exists()
    {
        var harness = new Harness();
        var message = new DeletionMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CorrelationId.New(), 1);

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message);

        await act.Should().NotThrowAsync();
        harness.ObjectStorage.DeletedKeys.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_is_idempotent_for_an_already_deleted_file()
    {
        var harness = new Harness();
        var file = harness.SeedReadyFile();
        file.MarkDeleted(Now);
        var message = new DeletionMessage(Guid.NewGuid(), file.ApplicationId, file.Id, CorrelationId.New(), 1);

        await harness.BuildOrchestrator().ProcessAsync(message);

        // Already-Deleted short-circuits before any storage/chunk deletion is attempted again.
        harness.ObjectStorage.DeletedKeys.Should().BeEmpty();
        harness.ChunkRepository.Chunks.Should().Contain(c => c.FileId == file.Id);
    }
}
