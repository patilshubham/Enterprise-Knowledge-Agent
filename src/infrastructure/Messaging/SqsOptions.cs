namespace EnterpriseKnowledgeAgent.Infrastructure.Messaging;

/// <summary>TASK-SQS-01/SQS-04: queue names and tuning (bound from the <c>Sqs</c> config section).</summary>
public sealed class SqsOptions
{
    public const string SectionName = "Sqs";

    /// <summary>Suffix LocalStack's init script (TASK-FND-08) appends to every queue name for its dead-letter queue.</summary>
    public const string DeadLetterSuffix = "-dlq";

    public required string DocumentProcessingQueue { get; init; }

    public required string AgentExecutionQueue { get; init; }

    public required string DeletionJobsQueue { get; init; }

    /// <summary>Matches the redrive policy's <c>maxReceiveCount</c> provisioned by TASK-FND-08's init script - kept here for health/diagnostics reporting, not enforcement (LocalStack/SQS enforces the real redrive).</summary>
    public int MaxReceiveCount { get; init; } = 5;

    /// <summary>Default visibility timeout (seconds) applied when a consumer receives messages (TASK-SQS-03/JOB-03 lease duration).</summary>
    public int DefaultVisibilityTimeoutSeconds { get; init; } = 60;
}
