namespace EnterpriseKnowledgeAgent.Application.Messaging;

/// <summary>Result of <see cref="IMessageQueue.CheckHealthAsync"/> (TASK-SQS-01, module 31).</summary>
public sealed record QueueHealth(bool IsAvailable, string? Detail);

/// <summary>Approximate visible + not-visible + DLQ depth for a queue (TASK-SQS-04, dashboards module 26).</summary>
public sealed record QueueDepth(long ApproximateMessageCount, long ApproximateNotVisibleCount, long DeadLetterQueueMessageCount);
