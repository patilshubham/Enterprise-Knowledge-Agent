using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-KNW-04: file listing/detail response, joining live stage/progress from the latest processing job when applicable.</summary>
public sealed record KnowledgeFileResponse(
    Guid Id,
    Guid ApplicationId,
    string DisplayName,
    long FileSize,
    string MimeType,
    string Encoding,
    UserFacingFileStatus Status,
    bool IsEnabled,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? LastProcessedAt,
    ProcessingStage? Stage,
    int? Progress);
