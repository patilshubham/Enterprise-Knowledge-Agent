using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Messaging;

/// <summary>Source-generated log messages (CA1848) for <see cref="IdempotentQueueConsumer{TMessage}"/> (TASK-SQS-03).</summary>
internal static partial class MessagingLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Duplicate/stale delivery for message {MessageId} - job already claimed or completed, deleting without reprocessing.")]
    public static partial void DuplicateDelivery(this ILogger logger, Guid messageId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Processing failed for message {MessageId} - leaving it for SQS redrive.")]
    public static partial void ProcessingFailed(this ILogger logger, Exception exception, Guid messageId);
}
