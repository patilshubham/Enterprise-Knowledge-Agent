using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>
/// TASK-PRDSH-01: one row of the processing dashboard listing (platform-wide
/// or application-scoped). <see cref="FileName"/> is looked up live via
/// <c>IKnowledgeFileRepository</c> (module 6) rather than snapshotted, since
/// <c>ProcessingJob</c> only stores <c>FileId</c> (module 9 predates module
/// 6's schema landing) - shows "(deleted file)" if the file no longer exists.
/// </summary>
public sealed record ProcessingJobSummaryResponse(
    Guid Id,
    Guid ApplicationId,
    Guid FileId,
    string FileName,
    string Status,
    string Stage,
    int Progress,
    int AttemptNumber,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCategory,
    string? FailureMessage,
    bool RetryEligible)
{
    public static ProcessingJobSummaryResponse FromEntity(ProcessingJob job, string fileName) => new(
        job.Id,
        job.ApplicationId,
        job.FileId,
        fileName,
        job.Status.ToString(),
        job.Stage.ToString(),
        job.Progress,
        job.AttemptNumber,
        job.CreatedAt,
        job.StartedAt,
        job.CompletedAt,
        job.FailureCategory?.ToString(),
        job.FailureMessage,
        RetryEligible: job.Status == JobStatus.Failed);
}

/// <summary>TASK-PRDSH-02: the processing job detail drawer's payload - everything <see cref="ProcessingJobSummaryResponse"/> has plus worker/lease/correlation fields useful for support/debugging.</summary>
public sealed record ProcessingJobDetailResponse(
    Guid Id,
    Guid ApplicationId,
    Guid FileId,
    string FileName,
    string Status,
    string Stage,
    int Progress,
    int AttemptNumber,
    string? WorkerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? HeartbeatAt,
    DateTimeOffset? CompletedAt,
    string? FailureCategory,
    string? FailureMessage,
    string CorrelationId,
    bool RetryEligible)
{
    public static ProcessingJobDetailResponse FromEntity(ProcessingJob job, string fileName) => new(
        job.Id,
        job.ApplicationId,
        job.FileId,
        fileName,
        job.Status.ToString(),
        job.Stage.ToString(),
        job.Progress,
        job.AttemptNumber,
        job.WorkerId,
        job.CreatedAt,
        job.StartedAt,
        job.HeartbeatAt,
        job.CompletedAt,
        job.FailureCategory?.ToString(),
        job.FailureMessage,
        job.CorrelationId.ToString(),
        RetryEligible: job.Status == JobStatus.Failed);
}
