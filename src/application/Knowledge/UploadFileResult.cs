namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-KNW-03: a single file provided to <see cref="KnowledgeFilesService.UploadAsync"/> - the Api layer buffers each multipart section into a seekable stream before calling in.</summary>
public sealed record UploadCandidate(string FileName, string ContentType, Stream Content);

/// <summary>TASK-KNW-03: outcome for one uploaded file.</summary>
public enum UploadOutcome
{
    Accepted,
    Warning,
    Rejected,
}

/// <summary>TASK-KNW-03: per-file upload result surfaced to the caller (FR-KNW-002).</summary>
public sealed record UploadFileResult(string FileName, UploadOutcome Outcome, Guid? FileId, string? Reason)
{
    public static UploadFileResult Accepted(string fileName, Guid fileId) => new(fileName, UploadOutcome.Accepted, fileId, null);

    public static UploadFileResult Warning(string fileName, string reason) => new(fileName, UploadOutcome.Warning, null, reason);

    public static UploadFileResult Rejected(string fileName, string reason) => new(fileName, UploadOutcome.Rejected, null, reason);
}
