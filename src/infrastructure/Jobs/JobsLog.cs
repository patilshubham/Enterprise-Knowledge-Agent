using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Jobs;

/// <summary>
/// Source-generated log messages (CA1848) for the TASK-JOB-04/05/06
/// background services (mirrors the TASK-APP-02 <c>ApplicationAuditLog</c>
/// pattern).
/// </summary>
internal static partial class JobsLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox publisher poll failed; will retry on next interval.")]
    public static partial void PublisherPollFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to publish outbox message {OutboxMessageId} of type {MessageType}.")]
    public static partial void PublishFailed(this ILogger logger, Exception exception, Guid outboxMessageId, string messageType);

    [LoggerMessage(Level = LogLevel.Error, Message = "Outbox reconciler pass failed; will retry on next interval.")]
    public static partial void ReconcilerPassFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Reconciler republished stale undelivered outbox message {OutboxMessageId} - the primary publisher missed it.")]
    public static partial void ReconcilerRepublished(this ILogger logger, Guid outboxMessageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Reconciler failed to republish outbox message {OutboxMessageId}.")]
    public static partial void ReconcilerRepublishFailed(this ILogger logger, Exception exception, Guid outboxMessageId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Lease reaper pass failed; will retry on next interval.")]
    public static partial void ReaperPassFailed(this ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Released expired lease on processing job {ProcessingJobId} (worker {WorkerId} did not heartbeat in time).")]
    public static partial void ReleasedExpiredJobLease(this ILogger logger, Guid processingJobId, string? workerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Released expired lease on agent run {AgentRunId} (worker {WorkerId} did not heartbeat in time).")]
    public static partial void ReleasedExpiredRunLease(this ILogger logger, Guid agentRunId, string? workerId);
}
