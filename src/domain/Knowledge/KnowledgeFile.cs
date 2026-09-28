using EnterpriseKnowledgeAgent.Shared.Knowledge;
// The property below is also named `ReplacementStatus` - this alias lets
// instance methods reference the enum's members (e.g. `ReplacementState.Processing`)
// without the ambiguity that using the bare type name `ReplacementStatus` inside this class would cause.
using ReplacementState = EnterpriseKnowledgeAgent.Domain.Knowledge.ReplacementStatus;

namespace EnterpriseKnowledgeAgent.Domain.Knowledge;

/// <summary>
/// TASK-KNW-01: the <c>KnowledgeFile</c> aggregate (ASDD §9.1, FR-KNW-*).
/// Detailed processing stage/progress/failure detail lives on the
/// associated <c>ProcessingJob</c> (module 9) - this entity tracks only the
/// coarse user-facing lifecycle (<see cref="Status"/>) plus the fields the
/// upload/listing/replacement flows need directly. <see cref="IsEnabled"/>
/// is independent of <see cref="Status"/> so a Ready file's real status is
/// never lost while it is toggled off retrieval (<see cref="EffectiveStatus"/>
/// projects "Disabled" for display without destroying that history).
/// </summary>
public sealed class KnowledgeFile
{
    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Server-generated S3 key (<see cref="Storage.ObjectKeyFactory"/> in the Application layer) - never client-supplied.</summary>
    public string ObjectKey { get; private set; } = string.Empty;

    /// <summary>SHA-256 hash (hex) of the stored content, used for duplicate detection (FR-KNW-005) and idempotent re-processing (FR-PRC-006).</summary>
    public string ContentHash { get; private set; } = string.Empty;

    public long FileSize { get; private set; }

    public string MimeType { get; private set; } = string.Empty;

    public string Encoding { get; private set; } = string.Empty;

    public UserFacingFileStatus Status { get; private set; }

    public bool IsEnabled { get; private set; }

    public string UploadedBy { get; private set; } = string.Empty;

    public DateTimeOffset UploadedAt { get; private set; }

    public DateTimeOffset? LastProcessedAt { get; private set; }

    /// <summary>TASK-KNW-05/module 15: object key of an in-flight replacement upload, if any.</summary>
    public string? ReplacementObjectKey { get; private set; }

    /// <summary>TASK-KNW-05/module 15: coarse status of an in-flight replacement, if any.</summary>
    public ReplacementStatus? ReplacementStatus { get; private set; }

    /// <summary>TASK-REP-01: staged content hash for the in-flight replacement, applied to <see cref="ContentHash"/> only once <see cref="CompleteReplacement"/> commits the swap.</summary>
    public string? ReplacementContentHash { get; private set; }

    /// <summary>TASK-REP-01: staged file size for the in-flight replacement.</summary>
    public long? ReplacementFileSize { get; private set; }

    /// <summary>TASK-REP-01: staged MIME type for the in-flight replacement.</summary>
    public string? ReplacementMimeType { get; private set; }

    /// <summary>TASK-REP-01: staged encoding for the in-flight replacement.</summary>
    public string? ReplacementEncoding { get; private set; }

    /// <summary>TASK-REP-03: when the deletion worker finished cleaning up this file, if it has been deleted.</summary>
    public DateTimeOffset? DeletedAt { get; private set; }

    /// <summary>Projects <see cref="Status"/> through <see cref="IsEnabled"/> for display: a disabled file always shows as Disabled regardless of its underlying lifecycle value (FR-KNW-007). Deleting/Deleted always take priority since they are not meaningful to further mask.</summary>
    public UserFacingFileStatus EffectiveStatus =>
        Status is UserFacingFileStatus.Deleting or UserFacingFileStatus.Deleted
            ? Status
            : (!IsEnabled ? UserFacingFileStatus.Disabled : Status);

#pragma warning disable CS8618 // required by EF Core materialization; all properties are always populated via Create/mapping.
    private KnowledgeFile()
    {
    }
#pragma warning restore CS8618

    /// <summary>
    /// Creates the entity with <see cref="ObjectKey"/> left empty - the
    /// caller (TASK-KNW-03) must immediately call <see cref="SetObjectKey"/>
    /// with a key derived from the newly-assigned <see cref="Id"/> (the
    /// canonical key format embeds the file id, so it cannot be computed
    /// before this call generates one).
    /// </summary>
    public static KnowledgeFile Create(
        Guid applicationId,
        string displayName,
        string contentHash,
        long fileSize,
        string mimeType,
        string encoding,
        string uploadedBy,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(uploadedBy);

        return new KnowledgeFile
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            DisplayName = displayName.Trim(),
            ObjectKey = string.Empty,
            ContentHash = contentHash,
            FileSize = fileSize,
            MimeType = mimeType,
            Encoding = encoding,
            Status = UserFacingFileStatus.Queued,
            IsEnabled = true,
            UploadedBy = uploadedBy,
            UploadedAt = now,
        };
    }

    /// <summary>TASK-KNW-03: assigns the server-generated S3 key once, immediately after <see cref="Create"/>.</summary>
    public void SetObjectKey(string objectKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectKey);
        ObjectKey = objectKey;
    }

    /// <summary>Called when the document worker claims the associated processing job.</summary>
    public void MarkProcessing(DateTimeOffset now)
    {
        if (Status is not (UserFacingFileStatus.Queued or UserFacingFileStatus.Failed))
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot mark file '{Id}' Processing from status '{Status}' (expected Queued or Failed).");
        }

        Status = UserFacingFileStatus.Processing;
    }

    public void MarkReady(DateTimeOffset now)
    {
        if (Status != UserFacingFileStatus.Processing)
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot mark file '{Id}' Ready from status '{Status}' (expected Processing).");
        }

        Status = UserFacingFileStatus.Ready;
        LastProcessedAt = now;
    }

    public void MarkFailed(DateTimeOffset now)
    {
        if (Status is not (UserFacingFileStatus.Processing or UserFacingFileStatus.Queued))
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot mark file '{Id}' Failed from status '{Status}' (expected Queued or Processing).");
        }

        Status = UserFacingFileStatus.Failed;
    }

    /// <summary>FR-KNW-007: excludes the file from retrieval while keeping it admin-visible. Only meaningful once a file has completed processing.</summary>
    public void Disable(DateTimeOffset now)
    {
        if (Status != UserFacingFileStatus.Ready)
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot disable file '{Id}' in status '{Status}' (expected Ready).");
        }

        if (!IsEnabled)
        {
            return; // idempotent
        }

        IsEnabled = false;
    }

    public void Enable(DateTimeOffset now)
    {
        if (IsEnabled)
        {
            return; // idempotent
        }

        IsEnabled = true;
    }

    /// <summary>TASK-KNW-05: idempotent - marking an already-Deleting file is a safe no-op (deletion is retryable).</summary>
    public void MarkDeleting(DateTimeOffset now)
    {
        if (Status == UserFacingFileStatus.Deleting)
        {
            return;
        }

        Status = UserFacingFileStatus.Deleting;
    }

    /// <summary>TASK-KNW-05: re-queues a Ready or Failed file for another processing pass.</summary>
    public void Reprocess(DateTimeOffset now)
    {
        if (Status is not (UserFacingFileStatus.Ready or UserFacingFileStatus.Failed))
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot reprocess file '{Id}' in status '{Status}' (expected Ready or Failed).");
        }

        Status = UserFacingFileStatus.Queued;
    }

    /// <summary>
    /// TASK-REP-01: begins an isolated replacement upload. Only allowed while
    /// the file is Ready and no other replacement is currently Processing -
    /// current content (<see cref="ObjectKey"/>/<see cref="Status"/>) is left
    /// completely untouched and stays searchable throughout (ASDD §13.3).
    /// The new content's metadata is staged on the <c>Replacement*</c>
    /// properties until <see cref="CompleteReplacement"/> commits the swap.
    /// </summary>
    public void BeginReplacement(string replacementObjectKey, string contentHash, long fileSize, string mimeType, string encoding, DateTimeOffset now)
    {
        if (Status != UserFacingFileStatus.Ready)
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot replace file '{Id}' in status '{Status}' (expected Ready).");
        }

        if (ReplacementStatus == ReplacementState.Processing)
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot replace file '{Id}': a replacement is already Processing.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(replacementObjectKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentHash);

        ReplacementObjectKey = replacementObjectKey;
        ReplacementStatus = ReplacementState.Processing;
        ReplacementContentHash = contentHash;
        ReplacementFileSize = fileSize;
        ReplacementMimeType = mimeType;
        ReplacementEncoding = encoding;
    }

    /// <summary>
    /// TASK-REP-02: commits a successful replacement - swaps the staged
    /// content into the live fields (<see cref="ObjectKey"/> becomes the
    /// former <see cref="ReplacementObjectKey"/>) and clears staging.
    /// <see cref="Status"/> was never changed by replacement, so it stays
    /// Ready. The caller is responsible for deleting the previous
    /// <see cref="ObjectKey"/>'s object in storage (this method only
    /// returns it) and for the chunk-level atomic swap
    /// (<c>IKnowledgeChunkRepository.SwapReplacementAsync</c>).
    /// </summary>
    public string CompleteReplacement(DateTimeOffset now)
    {
        if (ReplacementStatus != ReplacementState.Processing || ReplacementObjectKey is null)
        {
            throw new KnowledgeFileStateConflictException(
                $"Cannot complete replacement for file '{Id}': no replacement is Processing.");
        }

        var previousObjectKey = ObjectKey;

        ObjectKey = ReplacementObjectKey;
        ContentHash = ReplacementContentHash!;
        FileSize = ReplacementFileSize!.Value;
        MimeType = ReplacementMimeType!;
        Encoding = ReplacementEncoding!;
        LastProcessedAt = now;

        ReplacementObjectKey = null;
        ReplacementStatus = null;
        ReplacementContentHash = null;
        ReplacementFileSize = null;
        ReplacementMimeType = null;
        ReplacementEncoding = null;

        return previousObjectKey;
    }

    /// <summary>
    /// TASK-REP-02: records a failed replacement attempt - current content is
    /// untouched (never marked Failed; <see cref="Status"/> stays Ready), and
    /// staged replacement metadata is cleared since its temp artifacts (the
    /// replacement object + inactive chunks) are being cleaned up by the
    /// caller. <see cref="ReplacementStatus"/> is left as Failed (rather than
    /// cleared to null) so the UI can surface the failure until the next
    /// replacement attempt or acknowledgement overwrites it.
    /// </summary>
    public void FailReplacement(DateTimeOffset now)
    {
        ReplacementObjectKey = null;
        ReplacementContentHash = null;
        ReplacementFileSize = null;
        ReplacementMimeType = null;
        ReplacementEncoding = null;
        ReplacementStatus = ReplacementState.Failed;
    }

    /// <summary>TASK-REP-03: finalizes deletion once the worker has removed storage/vectors. Idempotent - a retried delivery of the same cleanup is a safe no-op.</summary>
    public void MarkDeleted(DateTimeOffset now)
    {
        if (Status == UserFacingFileStatus.Deleted)
        {
            return;
        }

        Status = UserFacingFileStatus.Deleted;
        DeletedAt = now;
    }
}

/// <summary>TASK-KNW-05/module 15 placeholder: coarse status of an in-flight file replacement.</summary>
public enum ReplacementStatus
{
    Processing,
    Ready,
    Failed,
}
