using EnterpriseKnowledgeAgent.Shared.Applications;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Dashboards;

/// <summary>
/// TASK-PDSH-01/TASK-ADSH-01: shared job/run status breakdown (<c>ProcessingJob</c>
/// and <c>AgentRun</c> both use <see cref="JobStatus"/> for their coarse
/// lifecycle) - reused for both processing jobs and agent runs in both
/// dashboard responses.
/// </summary>
public sealed record JobStatusCountsResponse(int Queued, int Running, int Completed, int Failed, int Cancelled)
{
    public static JobStatusCountsResponse FromCounts(IReadOnlyDictionary<JobStatus, int> counts) => new(
        counts.GetValueOrDefault(JobStatus.Queued),
        counts.GetValueOrDefault(JobStatus.Running),
        counts.GetValueOrDefault(JobStatus.Completed),
        counts.GetValueOrDefault(JobStatus.Failed),
        counts.GetValueOrDefault(JobStatus.Cancelled));
}

/// <summary>TASK-PDSH-01/TASK-ADSH-01: knowledge file counts by user-facing (Disabled/IsEnabled-projected) status.</summary>
public sealed record KnowledgeFileCountsResponse(int Queued, int Processing, int Ready, int Failed, int Disabled, int Deleting, int Deleted)
{
    public static KnowledgeFileCountsResponse FromCounts(IReadOnlyDictionary<UserFacingFileStatus, int> counts) => new(
        counts.GetValueOrDefault(UserFacingFileStatus.Queued),
        counts.GetValueOrDefault(UserFacingFileStatus.Processing),
        counts.GetValueOrDefault(UserFacingFileStatus.Ready),
        counts.GetValueOrDefault(UserFacingFileStatus.Failed),
        counts.GetValueOrDefault(UserFacingFileStatus.Disabled),
        counts.GetValueOrDefault(UserFacingFileStatus.Deleting),
        counts.GetValueOrDefault(UserFacingFileStatus.Deleted));
}

/// <summary>TASK-PDSH-01: application counts by lifecycle state.</summary>
public sealed record ApplicationCountsResponse(int Active, int Archived, int Deleted)
{
    public static ApplicationCountsResponse FromCounts(IReadOnlyDictionary<ApplicationStatus, int> counts) => new(
        counts.GetValueOrDefault(ApplicationStatus.Active),
        counts.GetValueOrDefault(ApplicationStatus.Archived),
        counts.GetValueOrDefault(ApplicationStatus.Deleted));
}

/// <summary>TASK-PDSH-01: a single worker process's last-seen heartbeat (module 31), with staleness already evaluated server-side.</summary>
public sealed record WorkerHeartbeatResponse(string WorkerId, string WorkerKind, DateTimeOffset LastHeartbeatAt, bool IsStale);

/// <summary>TASK-PDSH-01: approximate depth of one SQS queue + its dead-letter queue (TASK-SQS-04).</summary>
public sealed record QueueDepthResponse(string QueueName, long ApproximateMessageCount, long DeadLetterQueueMessageCount);

/// <summary>
/// TASK-PDSH-01/TASK-ADSH-01: a single "needs attention" callout (failed
/// jobs/runs, stale workers, unavailable AOAI, non-empty DLQs). No entity
/// ids are carried here (FR-DSH-001/002 dashboards are counts-only overviews
/// - the owning list pages, already built in modules 4/9/19, are where a
/// user drills into specifics).
/// </summary>
public sealed record DashboardAttentionItem(string Kind, string Message, int Count);

/// <summary>TASK-PDSH-01: `GET /api/platform/dashboard` response (FR-DSH-001, ASDD §21.1). Restricted to platform roles.</summary>
public sealed record PlatformDashboardResponse(
    ApplicationCountsResponse Applications,
    KnowledgeFileCountsResponse KnowledgeFiles,
    JobStatusCountsResponse ProcessingJobs,
    JobStatusCountsResponse AgentRuns,
    IReadOnlyList<WorkerHeartbeatResponse> Workers,
    bool AoaiAvailable,
    IReadOnlyList<QueueDepthResponse> Queues,
    IReadOnlyList<DashboardAttentionItem> AttentionItems);

/// <summary>TASK-ADSH-01: `GET /api/applications/{id}/dashboard` response (FR-APP-004, ASDD §21.2). Scoped to one application.</summary>
public sealed record ApplicationDashboardResponse(
    Guid ApplicationId,
    string ApplicationName,
    KnowledgeFileCountsResponse KnowledgeFiles,
    JobStatusCountsResponse ProcessingJobs,
    JobStatusCountsResponse AgentRuns,
    int ActiveAgents,
    int TotalAgents,
    int GeneratedOutputs,
    IReadOnlyList<DashboardAttentionItem> AttentionItems);
