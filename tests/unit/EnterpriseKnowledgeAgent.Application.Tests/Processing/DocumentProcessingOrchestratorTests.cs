using EnterpriseKnowledgeAgent.Application.Chunking;
using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace EnterpriseKnowledgeAgent.Application.Tests.Processing;

/// <summary>TASK-PRC-01/02/03/04: unit tests for the document-processing pipeline orchestrator, using in-memory fakes for every dependency.</summary>
public sealed class DocumentProcessingOrchestratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const string WorkerId = "worker-1";

    private sealed class Harness
    {
        public FakeProcessingJobRepository JobRepository { get; } = new();
        public FakeKnowledgeFileRepository FileRepository { get; } = new();
        public FakeKnowledgeChunkRepository ChunkRepository { get; } = new();
        public FakeOutboxRepository OutboxRepository { get; } = new();
        public FakeObjectStorage ObjectStorage { get; } = new();
        public FakeChunker Chunker { get; } = new();
        public FakeEmbeddingPipelineStage EmbeddingStage { get; } = new();
        public MarkdownAwareDocumentParser Parser { get; } = new();

        /// <summary>TASK-SIG-03: this suite exercises pipeline/persistence logic only - realtime publishing is covered separately.</summary>
        private sealed class NoOpRealtimeEventPublisher : IRealtimeEventPublisher
        {
            public Task PublishAsync<TPayload>(string eventType, IReadOnlyCollection<string> groups, SignalREvent<TPayload> envelope, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        public DocumentProcessingOrchestrator BuildOrchestrator() => new(
            JobRepository,
            FileRepository,
            ChunkRepository,
            OutboxRepository,
            ObjectStorage,
            Parser,
            Chunker,
            EmbeddingStage,
            new NoOpRealtimeEventPublisher(),
            TimeProvider.System,
            NullLogger<DocumentProcessingOrchestrator>.Instance);

        public (KnowledgeFile File, ProcessingJob Job, DocumentProcessingMessage Message) SeedClaimedJob(
            string displayName = "notes.txt", TimeSpan? leaseDuration = null)
        {
            var applicationId = Guid.NewGuid();
            var file = KnowledgeFile.Create(applicationId, displayName, "hash-1", 100, "text/plain", "utf-8", "sub", Now);
            file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/source");
            FileRepository.Files[file.Id] = file;

            var job = ProcessingJob.Create(applicationId, file.Id, CorrelationId.New(), Now);
            job.Claim(WorkerId, Now, leaseDuration ?? TimeSpan.FromMinutes(5));
            JobRepository.Jobs[job.Id] = job;

            var message = new DocumentProcessingMessage(Guid.NewGuid(), applicationId, file.Id, CorrelationId.New(), job.AttemptNumber);
            return (file, job, message);
        }

        /// <summary>TASK-REP-01/02: seeds a Ready file with an in-flight replacement (mirrors what <c>KnowledgeFilesService.ReplaceAsync</c> produces) plus a freshly-claimed processing job for it.</summary>
        public (KnowledgeFile File, ProcessingJob Job, DocumentProcessingMessage Message) SeedClaimedReplacementJob()
        {
            var applicationId = Guid.NewGuid();
            var file = KnowledgeFile.Create(applicationId, "notes.txt", "hash-1", 100, "text/plain", "utf-8", "sub", Now);
            file.SetObjectKey($"applications/{applicationId}/files/{file.Id}/source");
            file.MarkProcessing(Now);
            file.MarkReady(Now);
            file.BeginReplacement($"applications/{applicationId}/files/{file.Id}/replacement/r1", "hash-2", 200, "text/plain", "utf-8", Now);
            FileRepository.Files[file.Id] = file;

            var job = ProcessingJob.Create(applicationId, file.Id, CorrelationId.New(), Now);
            job.Claim(WorkerId, Now, TimeSpan.FromMinutes(5));
            JobRepository.Jobs[job.Id] = job;

            var message = new DocumentProcessingMessage(Guid.NewGuid(), applicationId, file.Id, CorrelationId.New(), job.AttemptNumber);
            return (file, job, message);
        }
    }

    [Fact]
    public async Task ProcessAsync_happy_path_completes_the_job_and_marks_the_file_ready()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedJob();
        harness.ObjectStorage.ContentToReturn = "Hello world.\n\nSecond paragraph.";

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        job.Status.Should().Be(JobStatus.Completed);
        job.Progress.Should().Be(100);
        file.Status.Should().Be(UserFacingFileStatus.Ready);
        harness.ChunkRepository.Chunks.Should().HaveCount(2);
        harness.EmbeddingStage.Calls.Should().ContainSingle(c => c.FileId == file.Id && c.ApplicationId == file.ApplicationId);
    }

    [Fact]
    public async Task ProcessAsync_deduplicates_chunks_already_persisted_by_content_hash()
    {
        var harness = new Harness();
        var (file, _, message) = harness.SeedClaimedJob();
        harness.ObjectStorage.ContentToReturn = "Hello world.\n\nSecond paragraph.";

        // Pre-seed a chunk whose content hash matches "Hello world." so it is not duplicated.
        var existingHash = ChunkPersistencePreparer.ComputeContentHash("Hello world.");
        var existingChunk = KnowledgeChunk.Create(file.ApplicationId, file.Id, 0, string.Empty, "Hello world.", 1, 0, 12, existingHash, Now);
        harness.ChunkRepository.Chunks.Add(existingChunk);

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        // Only the new "Second paragraph." chunk should have been added.
        harness.ChunkRepository.Chunks.Should().HaveCount(2);
        harness.ChunkRepository.Chunks.Count(c => c.ContentHash == existingHash).Should().Be(1);
    }

    [Fact]
    public async Task ProcessAsync_returns_without_error_when_no_job_exists_for_the_file()
    {
        var harness = new Harness();
        var message = new DocumentProcessingMessage(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CorrelationId.New(), 1);

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        harness.FileRepository.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_fails_the_job_when_the_file_no_longer_exists()
    {
        var harness = new Harness();
        var applicationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var job = ProcessingJob.Create(applicationId, fileId, CorrelationId.New(), Now);
        job.Claim(WorkerId, Now, TimeSpan.FromMinutes(5));
        harness.JobRepository.Jobs[job.Id] = job;
        var message = new DocumentProcessingMessage(Guid.NewGuid(), applicationId, fileId, CorrelationId.New(), 1);

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        job.Status.Should().Be(JobStatus.Failed);
    }

    [Fact]
    public async Task ProcessAsync_terminal_failure_marks_job_and_file_failed_without_throwing()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedJob();
        harness.ObjectStorage.ExceptionToThrowOnGet = new InvalidOperationException("boom - not retryable");

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        job.Status.Should().Be(JobStatus.Failed);
        file.Status.Should().Be(UserFacingFileStatus.Failed);
        harness.OutboxRepository.Messages.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_empty_document_is_a_terminal_validation_failure()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedJob();
        harness.ObjectStorage.ContentToReturn = "   \n\t  ";

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        job.Status.Should().Be(JobStatus.Failed);
        job.FailureCategory.Should().Be(ErrorCategory.Validation);
        file.Status.Should().Be(UserFacingFileStatus.Failed);
    }

    [Fact]
    public async Task ProcessAsync_retryable_failure_requeues_the_job_via_a_fresh_outbox_message_without_throwing()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedJob();
        harness.ObjectStorage.ExceptionToThrowOnGet = new TimeoutException("transient S3 timeout");

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        job.Status.Should().Be(JobStatus.Queued); // RequeueForRetry resets back to Queued for re-delivery.
        job.FailureCategory.Should().BeNull(); // reset by RequeueForRetry
        harness.OutboxRepository.Messages.Should().ContainSingle();
        job.OutboxMessageId.Should().Be(harness.OutboxRepository.Messages.Single().Id);
        file.Status.Should().Be(UserFacingFileStatus.Processing); // file is left mid-flight; only the job is retried.
    }

    [Fact]
    public async Task ProcessAsync_markdown_files_are_parsed_as_markdown()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedJob(displayName: "notes.md");
        harness.ObjectStorage.ContentToReturn = "# Title\n\nBody text.";

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        job.Status.Should().Be(JobStatus.Completed);
        harness.ChunkRepository.Chunks.Should().Contain(c => c.HeadingPath == "Title");
    }

    [Fact]
    public async Task ProcessAsync_replacement_success_swaps_chunks_completes_replacement_and_deletes_the_previous_object_instead_of_marking_ready()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedReplacementJob();
        var previousObjectKey = file.ObjectKey;
        harness.ObjectStorage.ContentToReturn = "Hello world.\n\nSecond paragraph.";

        await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        job.Status.Should().Be(JobStatus.Completed);
        // MarkReady is never called for a replacement (Status was never touched by BeginReplacement) - it just stays Ready.
        file.Status.Should().Be(UserFacingFileStatus.Ready);
        // The atomic swap (old-active removed, new-inactive activated) was invoked.
        harness.ChunkRepository.SwappedFileIds.Should().Contain(file.Id);
        // The embedding stage was told not to auto-activate (replacement mode relies on the swap instead).
        harness.EmbeddingStage.Calls.Should().ContainSingle(c => c.FileId == file.Id && !c.ActivateOnCompletion);
        // Replacement staging is cleared and the live object key now points at the former replacement key.
        file.ReplacementStatus.Should().BeNull();
        file.ObjectKey.Should().NotBe(previousObjectKey);
        // The now-superseded object was cleaned up from storage.
        harness.ObjectStorage.DeletedKeys.Should().Contain(previousObjectKey);
    }

    [Fact]
    public async Task ProcessAsync_replacement_terminal_failure_cleans_up_inactive_chunks_and_the_replacement_object_leaving_current_content_untouched()
    {
        var harness = new Harness();
        var (file, job, message) = harness.SeedClaimedReplacementJob();
        var previousObjectKey = file.ObjectKey;
        var replacementObjectKey = file.ReplacementObjectKey!;
        harness.ObjectStorage.ExceptionToThrowOnGet = new InvalidOperationException("boom - not retryable");

        var act = async () => await harness.BuildOrchestrator().ProcessAsync(message, WorkerId);

        await act.Should().NotThrowAsync();
        job.Status.Should().Be(JobStatus.Failed);
        // A failed replacement never marks the file Failed - current content stays exactly as it was.
        file.Status.Should().Be(UserFacingFileStatus.Ready);
        file.ObjectKey.Should().Be(previousObjectKey);
        file.ReplacementStatus.Should().Be(ReplacementStatus.Failed);
        file.ReplacementObjectKey.Should().BeNull();
        harness.ObjectStorage.DeletedKeys.Should().Contain(replacementObjectKey);
        harness.ChunkRepository.SwappedFileIds.Should().NotContain(file.Id);
    }
}
