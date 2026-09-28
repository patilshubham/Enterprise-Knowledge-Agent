using System.Security.Cryptography;
using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Messaging;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>
/// TASK-KNW-03/04/05: upload (validate → hash → store → job), listing +
/// preview, and disable/enable/delete/reprocess orchestration for
/// <see cref="KnowledgeFile"/> (FR-KNW-*). Mirrors <see cref="Applications.ApplicationsService"/>'s
/// convention: business rules live in the domain entity or here, the Api
/// layer only maps HTTP↔calls and exceptions↔Problem Details.
/// </summary>
public sealed class KnowledgeFilesService(
    IKnowledgeFileRepository fileRepository,
    IKnowledgeChunkRepository chunkRepository,
    IObjectStorage objectStorage,
    IProcessingJobRepository processingJobRepository,
    IOutboxRepository outboxRepository,
    JobCreationService jobCreationService,
    FileValidationService validationService,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    /// <summary>TASK-KNW-03: validates, hashes, stores, and enqueues processing for each candidate file independently - one file's rejection never affects the others.</summary>
    public async Task<IReadOnlyList<UploadFileResult>> UploadAsync(
        Guid applicationId,
        IReadOnlyList<UploadCandidate> files,
        string uploadedBy,
        bool allowDuplicates,
        CancellationToken cancellationToken = default)
    {
        var results = new List<UploadFileResult>(files.Count);

        foreach (var file in files)
        {
            var validation = await validationService.ValidateAsync(file.FileName, file.Content, cancellationToken);
            if (!validation.IsValid)
            {
                results.Add(UploadFileResult.Rejected(file.FileName, validation.Reason!.Value.ToString()));
                continue;
            }

            file.Content.Position = 0;
            var contentHash = await ComputeHashAsync(file.Content, cancellationToken);

            if (!allowDuplicates && await fileRepository.ExistsActiveHashAsync(applicationId, contentHash, cancellationToken))
            {
                results.Add(UploadFileResult.Warning(file.FileName, "DuplicateContent"));
                continue;
            }

            var now = timeProvider.GetUtcNow();
            var knowledgeFile = KnowledgeFile.Create(
                applicationId,
                validation.SanitizedFileName!,
                contentHash,
                file.Content.Length,
                file.ContentType,
                validation.DetectedEncodingWebName!,
                uploadedBy,
                now);

            var objectKey = ObjectKeyFactory.Source(applicationId, knowledgeFile.Id);
            knowledgeFile.SetObjectKey(objectKey);

            file.Content.Position = 0;
            await objectStorage.PutAsync(objectKey, file.Content, file.ContentType, cancellationToken);

            await fileRepository.AddAsync(knowledgeFile, cancellationToken);
            await fileRepository.SaveChangesAsync(cancellationToken);

            await jobCreationService.CreateProcessingJobAsync(applicationId, knowledgeFile.Id, CorrelationContext.CorrelationIdOrNew(), cancellationToken);

            results.Add(UploadFileResult.Accepted(file.FileName, knowledgeFile.Id));
        }

        return results;
    }

    /// <summary>TASK-REP-01: validates and stores a replacement upload at a dedicated replacement key, then enqueues isolated processing that produces inactive chunks - current content is untouched and stays searchable throughout.</summary>
    public async Task<KnowledgeFileResponse> ReplaceAsync(
        Guid fileId, UploadCandidate file, CancellationToken cancellationToken = default)
    {
        var knowledgeFile = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);

        var validation = await validationService.ValidateAsync(file.FileName, file.Content, cancellationToken);
        if (!validation.IsValid)
        {
            throw new FileValidationFailedException(validation.Reason!.Value);
        }

        file.Content.Position = 0;
        var contentHash = await ComputeHashAsync(file.Content, cancellationToken);

        var replacementId = Guid.NewGuid();
        var objectKey = ObjectKeyFactory.Replacement(knowledgeFile.ApplicationId, knowledgeFile.Id, replacementId);

        file.Content.Position = 0;
        await objectStorage.PutAsync(objectKey, file.Content, file.ContentType, cancellationToken);

        var now = timeProvider.GetUtcNow();
        knowledgeFile.BeginReplacement(objectKey, contentHash, file.Content.Length, file.ContentType, validation.DetectedEncodingWebName!, now);
        await fileRepository.SaveChangesAsync(cancellationToken);

        await jobCreationService.CreateProcessingJobAsync(knowledgeFile.ApplicationId, knowledgeFile.Id, CorrelationContext.CorrelationIdOrNew(), cancellationToken);

        return await ToResponseAsync(knowledgeFile, cancellationToken);
    }

    /// <summary>TASK-KNW-04: filtered listing with live stage/progress joined from the latest processing job.</summary>
    public async Task<IReadOnlyList<KnowledgeFileResponse>> ListAsync(
        Guid applicationId, string? search, UserFacingFileStatus? status, CancellationToken cancellationToken = default)
    {
        var files = await fileRepository.ListAsync(applicationId, search, status, cancellationToken);
        var responses = new List<KnowledgeFileResponse>(files.Count);

        foreach (var file in files)
        {
            responses.Add(await ToResponseAsync(file, cancellationToken));
        }

        return responses;
    }

    public async Task<KnowledgeFileResponse> GetAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);
        return await ToResponseAsync(file, cancellationToken);
    }

    /// <summary>TASK-KNW-04: streams decoded text for authorized preview (FR-3.4). Only meaningful once a file has reached Ready.</summary>
    public async Task<(string FileName, string Content)> PreviewAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);

        await using var stream = await objectStorage.GetAsync(file.ObjectKey, cancellationToken);
        using var reader = new StreamReader(stream, System.Text.Encoding.GetEncoding(file.Encoding));
        var content = await reader.ReadToEndAsync(cancellationToken);
        return (file.DisplayName, content);
    }

    public async Task<KnowledgeFileResponse> DisableAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);
        file.Disable(timeProvider.GetUtcNow());
        await fileRepository.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(file, cancellationToken);
    }

    public async Task<KnowledgeFileResponse> EnableAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);
        file.Enable(timeProvider.GetUtcNow());
        await fileRepository.SaveChangesAsync(cancellationToken);
        return await ToResponseAsync(file, cancellationToken);
    }

    /// <summary>TASK-KNW-05: marks the file Deleting and enqueues a <see cref="DeletionMessage"/> for the (module 15) cleanup worker. Idempotent/retryable.</summary>
    public async Task DeleteAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);

        var now = timeProvider.GetUtcNow();
        file.MarkDeleting(now);
        await fileRepository.SaveChangesAsync(cancellationToken);

        var message = new DeletionMessage(Guid.NewGuid(), file.ApplicationId, file.Id, CorrelationContext.CorrelationIdOrNew(), AttemptNumber: 1);
        var outbox = OutboxMessage.Create(OutboxMessageType.Deletion, JsonSerializer.Serialize(message, SerializerOptions), now);
        await outboxRepository.AddAsync(outbox, cancellationToken);
        await outboxRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>TASK-KNW-05: re-queues a Ready or Failed file for another processing pass, dropping its prior chunks (they will be regenerated).</summary>
    public async Task<KnowledgeFileResponse> ReprocessAsync(Guid fileId, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(fileId, cancellationToken) ?? throw new KnowledgeFileNotFoundException(fileId);

        file.Reprocess(timeProvider.GetUtcNow());
        await chunkRepository.DeleteAllForFileAsync(fileId, cancellationToken);
        await fileRepository.SaveChangesAsync(cancellationToken);

        await jobCreationService.CreateProcessingJobAsync(file.ApplicationId, file.Id, CorrelationContext.CorrelationIdOrNew(), cancellationToken);

        return await ToResponseAsync(file, cancellationToken);
    }

    private async Task<KnowledgeFileResponse> ToResponseAsync(KnowledgeFile file, CancellationToken cancellationToken)
    {
        ProcessingStage? stage = null;
        int? progress = null;

        if (file.Status == UserFacingFileStatus.Processing)
        {
            var job = await processingJobRepository.GetLatestByFileAsync(file.Id, cancellationToken);
            if (job is not null)
            {
                stage = job.Stage;
                progress = job.Progress;
            }
        }

        return new KnowledgeFileResponse(
            file.Id,
            file.ApplicationId,
            file.DisplayName,
            file.FileSize,
            file.MimeType,
            file.Encoding,
            file.EffectiveStatus,
            file.IsEnabled,
            file.UploadedBy,
            file.UploadedAt,
            file.LastProcessedAt,
            stage,
            progress);
    }

    private static async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken)
    {
        content.Position = 0;
        var hashBytes = await SHA256.HashDataAsync(content, cancellationToken);
        content.Position = 0;
        return Convert.ToHexStringLower(hashBytes);
    }
}
