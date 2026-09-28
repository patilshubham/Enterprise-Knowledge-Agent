using EnterpriseKnowledgeAgent.Shared.Messaging;

namespace EnterpriseKnowledgeAgent.Application.Messaging;

/// <summary>
/// TASK-SQS-01: SQS abstraction configured for LocalStack. Queue URLs are
/// resolved by logical name (see <c>SqsOptions</c>) so callers never deal
/// with LocalStack/AWS account-specific URLs directly. Messages are always
/// identifier-only (<see cref="IQueueMessage"/>) - never raw content,
/// prompts, or secrets (FR-PRC-001, reflection-tested in Shared.Tests).
/// </summary>
public interface IMessageQueue
{
    /// <summary>Sends a message to the named queue. Returns the provider-assigned message id.</summary>
    Task<string> SendAsync<TMessage>(string queueName, TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IQueueMessage;

    /// <summary>Long-polls for up to <paramref name="maxMessages"/> messages, setting the given visibility timeout.</summary>
    Task<IReadOnlyList<QueueMessageEnvelope<TMessage>>> ReceiveAsync<TMessage>(
        string queueName,
        int maxMessages,
        TimeSpan visibilityTimeout,
        CancellationToken cancellationToken = default)
        where TMessage : IQueueMessage;

    /// <summary>Deletes a message after successful processing (or after a terminal failure that should not be retried).</summary>
    Task DeleteAsync(string queueName, string receiptHandle, CancellationToken cancellationToken = default);

    /// <summary>Extends a message's visibility timeout while it is still being processed (lease/heartbeat renewal, TASK-JOB-03).</summary>
    Task ExtendVisibilityAsync(string queueName, string receiptHandle, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default);

    /// <summary>Approximate depth of the named queue and its dead-letter queue (TASK-SQS-04).</summary>
    Task<QueueDepth> GetApproximateDepthAsync(string queueName, CancellationToken cancellationToken = default);

    /// <summary>Lightweight readiness probe used by health checks (module 31).</summary>
    Task<QueueHealth> CheckHealthAsync(CancellationToken cancellationToken = default);
}
