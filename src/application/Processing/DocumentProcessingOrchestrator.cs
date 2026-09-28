using System.Diagnostics;
using System.Text;
using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Chunking;
using EnterpriseKnowledgeAgent.Application.Embeddings;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using EnterpriseKnowledgeAgent.Shared.Observability;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>
/// TASK-PRC-01/02/03/04: the document-processing pipeline body (FR-PRC-001..
/// 008), hosted by the document worker (module 10). Stages: download from
/// object storage, normalize + parse (TASK-PRC-03), chunk (TASK-CHK-01),
/// persist chunks inactive (TASK-CHK-02), then hand off to
/// <see cref="IEmbeddingPipelineStage"/> (module 12/13, P4 uses the no-op
/// placeholder) which activates them. Retryable failures (ASDD §12.4 /
/// TASK-JOB-06) are automatically re-queued via a fresh outbox row up to
/// <see cref="RetryClassifier.DefaultMaxAttempts"/>, reusing the exact same
/// <see cref="ProcessingJob.RequeueForRetry"/> path as manual retry; terminal
/// failures mark the job Failed and the file Failed for the admin to retry
/// manually (JOB-06). Every outcome (success, requeue, terminal failure)
/// completes without throwing, so the caller (module 10's
/// <c>IdempotentQueueConsumer</c>) always deletes the current SQS message -
/// future attempts are driven entirely by the outbox, never by native SQS
/// redelivery (Decision D1 keeps that the single publishing path).
/// </summary>
public sealed class DocumentProcessingOrchestrator(
    IProcessingJobRepository jobRepository,
    IKnowledgeFileRepository fileRepository,
    IKnowledgeChunkRepository chunkRepository,
    IOutboxRepository outboxRepository,
    IObjectStorage objectStorage,
    IDocumentParser parser,
    IChunker chunker,
    IEmbeddingPipelineStage embeddingStage,
    IRealtimeEventPublisher realtimePublisher,
    TimeProvider timeProvider,
    ILogger<DocumentProcessingOrchestrator> logger)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    public async Task ProcessAsync(DocumentProcessingMessage message, string workerId, CancellationToken cancellationToken = default)
    {
        var job = await jobRepository.GetLatestByFileAsync(message.KnowledgeFileId, cancellationToken);
        if (job is null)
        {
            logger.NoJobFoundForFile(message.KnowledgeFileId);
            return;
        }

        var file = await fileRepository.GetAsync(message.KnowledgeFileId, cancellationToken);
        if (file is null)
        {
            var missingFileNow = timeProvider.GetUtcNow();
            job.Fail(ErrorCategory.InternalFailure, $"Knowledge file '{message.KnowledgeFileId}' no longer exists.", missingFileNow);
            await jobRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        using var jobActivity = AppTracing.Source.StartActivity("DocumentProcessing.Execute");
        jobActivity?.SetTag("job.id", job.Id);
        jobActivity?.SetTag("file.id", file.Id);

        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";
        try
        {
            await RunPipelineAsync(job, file, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            outcome = "failure";
            AppMetrics.JobFailures.Add(1, new TagList { { "category", ClassifyException(ex).ToString() } });
            await HandleFailureAsync(job, file, message, ex, cancellationToken);
        }
        finally
        {
            AppMetrics.JobDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, new TagList { { "outcome", outcome } });
        }
    }

    private async Task RunPipelineAsync(ProcessingJob job, KnowledgeFile file, CancellationToken cancellationToken)
    {
        // TASK-REP-01: a replacement in Processing drives this same pipeline
        // over the replacement object/staged chunks while current content
        // stays untouched and searchable (file.Status is never touched here).
        var isReplacement = file.ReplacementStatus == ReplacementStatus.Processing;
        var sourceObjectKey = isReplacement ? file.ReplacementObjectKey! : file.ObjectKey;
        var sourceEncoding = isReplacement ? file.ReplacementEncoding! : file.Encoding;

        var now = timeProvider.GetUtcNow();
        if (!isReplacement)
        {
            file.MarkProcessing(now);
            await fileRepository.SaveChangesAsync(cancellationToken);
        }

        job.UpdateProgress(ProcessingStage.Downloading, 10, timeProvider.GetUtcNow());
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileStageChangedAsync(job, cancellationToken);

        string rawText;
        using (AppTracing.Source.StartActivity("Download"))
        await using (var stream = await objectStorage.GetAsync(sourceObjectKey, cancellationToken))
        {
            using var reader = new StreamReader(stream, ResolveEncoding(sourceEncoding));
            rawText = await reader.ReadToEndAsync(cancellationToken);
        }

        job.UpdateProgress(ProcessingStage.Validating, 25, timeProvider.GetUtcNow());
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileStageChangedAsync(job, cancellationToken);
        var normalized = TextNormalizer.Normalize(rawText);

        job.UpdateProgress(ProcessingStage.Parsing, 40, timeProvider.GetUtcNow());
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileStageChangedAsync(job, cancellationToken);
        var isMarkdown = file.DisplayName.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
        // IDocumentParser.Parse throws EmptyDocumentException itself for
        // empty/whitespace-only content or a zero-block parse result.
        IReadOnlyList<ParsedBlock> blocks;
        using (AppTracing.Source.StartActivity("Parse"))
        {
            blocks = parser.Parse(normalized, isMarkdown);
        }

        job.UpdateProgress(ProcessingStage.Chunking, 60, timeProvider.GetUtcNow());
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileStageChangedAsync(job, cancellationToken);
        using (AppTracing.Source.StartActivity("Chunk"))
        {
            var drafts = chunker.Chunk(blocks);
            AppMetrics.ChunksPerFile.Record(drafts.Count);
            // Scoped to inactive chunks only (see IKnowledgeChunkRepository remarks) so a
            // replacement's unchanged-content chunks are always recreated, never silently
            // reused from the old active generation that TASK-REP-02 is about to remove.
            var existingHashes = await chunkRepository.ListContentHashesByFileAsync(file.Id, cancellationToken);
            var chunkNow = timeProvider.GetUtcNow();
            var chunks = ChunkPersistencePreparer.Prepare(file.ApplicationId, file.Id, drafts, chunkNow)
                .Where(c => !existingHashes.Contains(c.ContentHash))
                .ToList();
            if (chunks.Count > 0)
            {
                await chunkRepository.AddRangeAsync(chunks, cancellationToken);
                await chunkRepository.SaveChangesAsync(cancellationToken);
            }
        }

        job.UpdateProgress(ProcessingStage.GeneratingEmbeddings, 80, timeProvider.GetUtcNow());
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileStageChangedAsync(job, cancellationToken);
        using (AppTracing.Source.StartActivity("Generate embeddings"))
        {
            await embeddingStage.RunAsync(
                file.ApplicationId,
                file.Id,
                onProgress: async (fraction, ct) =>
                {
                    job.UpdateProgress(ProcessingStage.GeneratingEmbeddings, 80 + (int)(fraction * 15), timeProvider.GetUtcNow());
                    await jobRepository.SaveChangesAsync(ct);
                    await PublishFileStageChangedAsync(job, ct);
                },
                // TASK-REP-02: a replacement's new-generation chunks stay inactive until the
                // atomic swap below - never activated early alongside the still-active old generation.
                activateOnCompletion: !isReplacement,
                cancellationToken: cancellationToken);
        }

        job.UpdateProgress(ProcessingStage.Finalizing, 95, timeProvider.GetUtcNow());
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileStageChangedAsync(job, cancellationToken);

        var completedNow = timeProvider.GetUtcNow();
        if (isReplacement)
        {
            // TASK-REP-02: one transaction removes the old generation and activates the new one.
            await chunkRepository.SwapReplacementAsync(file.Id, cancellationToken);

            var previousObjectKey = file.CompleteReplacement(completedNow);
            await fileRepository.SaveChangesAsync(cancellationToken);

            // Best-effort post-commit cleanup of the now-superseded object (IObjectStorage.DeleteAsync is idempotent).
            await objectStorage.DeleteAsync(previousObjectKey, cancellationToken);
        }
        else
        {
            file.MarkReady(completedNow);
            await fileRepository.SaveChangesAsync(cancellationToken);
        }

        job.Complete(completedNow);
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileCompletedAsync(job, cancellationToken);
    }

    private async Task HandleFailureAsync(
        ProcessingJob job,
        KnowledgeFile file,
        DocumentProcessingMessage message,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var category = ClassifyException(exception);
        var now = timeProvider.GetUtcNow();

        if (RetryClassifier.ShouldAutoRetry(category, job.AttemptNumber))
        {
            var retryMessage = message with { MessageId = Guid.NewGuid(), AttemptNumber = job.AttemptNumber + 1 };
            var outboxRow = OutboxMessage.Create(
                OutboxMessageType.DocumentProcessing, JsonSerializer.Serialize(retryMessage, SerializerOptions), now);
            await outboxRepository.AddAsync(outboxRow, cancellationToken);
            await outboxRepository.SaveChangesAsync(cancellationToken);

            // RequeueForRetry requires the job to be in the Failed status
            // (it is also the manual-retry entry point, TASK-JOB-06) - fail
            // it first so this automatic retry satisfies that precondition.
            job.Fail(category, exception.Message, now);
            job.RequeueForRetry(outboxRow.Id, now);
            await jobRepository.SaveChangesAsync(cancellationToken);

            logger.RetryableFailureRequeued(exception, file.Id, category, job.AttemptNumber);
            return;
        }

        job.Fail(category, exception.Message, now);
        await jobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileFailedAsync(job, category, cancellationToken);

        if (file.ReplacementStatus == ReplacementStatus.Processing)
        {
            // TASK-REP-02: a failed replacement never touches current content -
            // only the temp replacement object + not-yet-activated chunks are cleaned up.
            await chunkRepository.DeleteInactiveForFileAsync(file.Id, cancellationToken);
            await objectStorage.DeleteAsync(file.ReplacementObjectKey!, cancellationToken);

            file.FailReplacement(now);
            await fileRepository.SaveChangesAsync(cancellationToken);

            logger.TerminalFailure(exception, file.Id, category);
            return;
        }

        file.MarkFailed(now);
        await fileRepository.SaveChangesAsync(cancellationToken);

        logger.TerminalFailure(exception, file.Id, category);
    }

    private static ErrorCategory ClassifyException(Exception exception) => exception switch
    {
        EmptyDocumentException => ErrorCategory.Validation,
        ObjectNotFoundException => ErrorCategory.InternalFailure,
        TimeoutException => ErrorCategory.Timeout,
        EnterpriseKnowledgeAgent.Application.AzureOpenAI.AzureOpenAiException aoiEx => aoiEx.Category,
        EmbeddingValidationException => ErrorCategory.EmbeddingFailure,
        _ when exception.GetType().FullName == "Amazon.S3.AmazonS3Exception" => ErrorCategory.StorageFailure,
        _ => ErrorCategory.InternalFailure,
    };

    // TASK-SIG-03: published post-commit only (each call site above always
    // follows the matching SaveChangesAsync) - never before persistence
    // (ASDD §18.4). This orchestrator runs in the document-worker process,
    // a separate OS process from the Api that hosts the SignalR hub, so
    // realtimePublisher is the HTTP-relay implementation there (see
    // Infrastructure's HttpRealtimeEventPublisher) - failures are swallowed
    // internally by every implementation, never surfaced here.
    private Task PublishFileStageChangedAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<FileStageChangedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.FileStageChanged, job.ApplicationId, job.FileId,
            timeProvider.GetUtcNow(), job.EventSequence, new FileStageChangedPayload(job.FileId, job.Stage, job.Progress));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.FileStageChanged, [RealtimeGroups.Application(job.ApplicationId)], envelope, cancellationToken);
    }

    private async Task PublishFileCompletedAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<FileCompletedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.FileCompleted, job.ApplicationId, job.FileId,
            timeProvider.GetUtcNow(), job.EventSequence, new FileCompletedPayload(job.FileId));
        await realtimePublisher.PublishAsync(
            RealtimeEventTypes.FileCompleted, [RealtimeGroups.Application(job.ApplicationId)], envelope, cancellationToken);
        await PublishDashboardChangedAsync(job.ApplicationId, RealtimeEventTypes.FileCompleted, cancellationToken);
    }

    private async Task PublishFileFailedAsync(ProcessingJob job, ErrorCategory category, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<FileFailedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.FileFailed, job.ApplicationId, job.FileId,
            timeProvider.GetUtcNow(), job.EventSequence, new FileFailedPayload(job.FileId, category));
        await realtimePublisher.PublishAsync(
            RealtimeEventTypes.FileFailed, [RealtimeGroups.Application(job.ApplicationId)], envelope, cancellationToken);
        await PublishDashboardChangedAsync(job.ApplicationId, RealtimeEventTypes.FileFailed, cancellationToken);
    }

    private Task PublishDashboardChangedAsync(Guid applicationId, string reason, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<DashboardChangedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.DashboardChanged, applicationId, applicationId,
            timeProvider.GetUtcNow(), Sequence: 0, new DashboardChangedPayload(reason));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.DashboardChanged,
            [RealtimeGroups.Application(applicationId), RealtimeGroups.OperationsGroup],
            envelope,
            cancellationToken);
    }

    private static Encoding ResolveEncoding(string webName)
    {
        try
        {
            return Encoding.GetEncoding(webName);
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
