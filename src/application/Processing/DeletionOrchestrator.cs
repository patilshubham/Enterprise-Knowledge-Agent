using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>
/// TASK-REP-03: consumes <see cref="DeletionMessage"/> (module 15) to
/// asynchronously clean up a deleted <see cref="Domain.Knowledge.KnowledgeFile"/>'s
/// stored content and vectors (FR-KNW-008). Removes the active object (and
/// any in-flight replacement object) from object storage, removes all
/// chunks/vectors, then finalizes the file as
/// <see cref="Shared.Knowledge.UserFacingFileStatus.Deleted"/> - the record
/// itself is kept (not hard-deleted) so historical outputs/citations
/// (module 22) can still resolve the file id and render "source
/// unavailable" (FR-CIT-004). Every step is naturally idempotent
/// (<see cref="IObjectStorage.DeleteAsync"/> succeeds on a missing key,
/// bulk chunk delete is a no-op once already empty,
/// <see cref="Domain.Knowledge.KnowledgeFile.MarkDeleted"/> is a no-op once
/// already Deleted), so a duplicate/retried delivery of the same message is
/// always safe.
/// </summary>
public sealed class DeletionOrchestrator(
    IKnowledgeFileRepository fileRepository,
    IKnowledgeChunkRepository chunkRepository,
    IObjectStorage objectStorage,
    TimeProvider timeProvider,
    ILogger<DeletionOrchestrator> logger)
{
    public async Task ProcessAsync(DeletionMessage message, CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(message.KnowledgeFileId, cancellationToken);
        if (file is null)
        {
            logger.DeletionSkippedFileMissing(message.KnowledgeFileId);
            return;
        }

        if (file.Status == Shared.Knowledge.UserFacingFileStatus.Deleted)
        {
            logger.DeletionAlreadyComplete(file.Id);
            return;
        }

        if (!string.IsNullOrEmpty(file.ObjectKey))
        {
            await objectStorage.DeleteAsync(file.ObjectKey, cancellationToken);
        }

        if (!string.IsNullOrEmpty(file.ReplacementObjectKey))
        {
            await objectStorage.DeleteAsync(file.ReplacementObjectKey, cancellationToken);
        }

        await chunkRepository.DeleteAllForFileAsync(file.Id, cancellationToken);

        file.MarkDeleted(timeProvider.GetUtcNow());
        await fileRepository.SaveChangesAsync(cancellationToken);

        logger.DeletionCompleted(file.Id);
    }
}

internal static partial class DeletionOrchestratorLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Deletion skipped: knowledge file '{FileId}' no longer exists.")]
    public static partial void DeletionSkippedFileMissing(this ILogger<DeletionOrchestrator> logger, Guid fileId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Deletion skipped: knowledge file '{FileId}' was already deleted (duplicate/retried delivery).")]
    public static partial void DeletionAlreadyComplete(this ILogger<DeletionOrchestrator> logger, Guid fileId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deletion completed for knowledge file '{FileId}': storage + chunks/vectors removed.")]
    public static partial void DeletionCompleted(this ILogger<DeletionOrchestrator> logger, Guid fileId);
}
