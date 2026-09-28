using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Messaging;

/// <summary>TASK-SQS-03: tuning knobs for <see cref="IdempotentQueueConsumer{TMessage}"/>.</summary>
public sealed class QueueConsumerOptions
{
    /// <summary>Max messages fetched per <see cref="IMessageQueue.ReceiveAsync{TMessage}"/> long-poll.</summary>
    public int MaxMessagesPerPoll { get; init; } = 10;

    /// <summary>Initial (and per-heartbeat renewed) SQS visibility timeout - should exceed <see cref="HeartbeatInterval"/> with margin so a single missed heartbeat doesn't cause a premature redelivery.</summary>
    public TimeSpan VisibilityTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>How often the consumer extends SQS visibility and calls the caller's DB heartbeat while <c>processAsync</c> is still running.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(20);
}

/// <summary>
/// TASK-SQS-03: reusable worker consumer loop - long-poll receive, atomic
/// job claim (delegated to module 9 so this class stays job-type-agnostic),
/// visibility-timeout extension via heartbeat while processing, delete-on-
/// success, and leaving failed/duplicate messages for SQS's own redrive
/// policy (module 8/TASK-FND-08) to eventually route to the DLQ after
/// <c>maxReceiveCount</c>. Not itself a hosted service - a thin
/// <see cref="Microsoft.Extensions.Hosting.BackgroundService"/> per worker
/// (module 10/19, not yet built) is expected to loop calling
/// <see cref="ProcessNextBatchAsync"/>.
/// </summary>
public sealed class IdempotentQueueConsumer<TMessage>(
    IMessageQueue queue,
    string queueName,
    QueueConsumerOptions options,
    ILogger logger)
    where TMessage : IQueueMessage
{
    /// <summary>
    /// Receives up to <see cref="QueueConsumerOptions.MaxMessagesPerPoll"/>
    /// messages and processes each: <paramref name="tryClaimAsync"/> returns
    /// <see langword="false"/> for a duplicate delivery of an
    /// already-claimed/completed job (safe no-op - the message is deleted
    /// immediately without calling <paramref name="processAsync"/>).
    /// <paramref name="processAsync"/> throwing leaves the message
    /// undeleted so SQS redelivers it (retry) or eventually DLQs it.
    /// Returns the number of messages successfully processed and deleted.
    /// </summary>
    public async Task<int> ProcessNextBatchAsync(
        Func<TMessage, CancellationToken, Task<bool>> tryClaimAsync,
        Func<TMessage, CancellationToken, Task> processAsync,
        Func<TMessage, CancellationToken, Task> heartbeatAsync,
        CancellationToken cancellationToken = default)
    {
        var envelopes = await queue.ReceiveAsync<TMessage>(
            queueName, options.MaxMessagesPerPoll, options.VisibilityTimeout, cancellationToken);

        var processedCount = 0;

        foreach (var envelope in envelopes)
        {
            var claimed = await tryClaimAsync(envelope.Message, cancellationToken);
            if (!claimed)
            {
                logger.DuplicateDelivery(envelope.Message.MessageId);
                await queue.DeleteAsync(queueName, envelope.ReceiptHandle, cancellationToken);
                continue;
            }

            if (await ProcessWithHeartbeatAsync(envelope, processAsync, heartbeatAsync, cancellationToken))
            {
                await queue.DeleteAsync(queueName, envelope.ReceiptHandle, cancellationToken);
                processedCount++;
            }
        }

        return processedCount;
    }

    private async Task<bool> ProcessWithHeartbeatAsync(
        QueueMessageEnvelope<TMessage> envelope,
        Func<TMessage, CancellationToken, Task> processAsync,
        Func<TMessage, CancellationToken, Task> heartbeatAsync,
        CancellationToken cancellationToken)
    {
        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatLoop = HeartbeatLoopAsync(envelope, heartbeatAsync, heartbeatCts.Token);

        try
        {
            await processAsync(envelope.Message, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ProcessingFailed(ex, envelope.Message.MessageId);
            return false;
        }
        finally
        {
            heartbeatCts.Cancel();
            try
            {
                await heartbeatLoop;
            }
            catch (OperationCanceledException)
            {
                // Expected once heartbeatCts is cancelled above.
            }
        }
    }

    private async Task HeartbeatLoopAsync(
        QueueMessageEnvelope<TMessage> envelope,
        Func<TMessage, CancellationToken, Task> heartbeatAsync,
        CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(options.HeartbeatInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            await queue.ExtendVisibilityAsync(queueName, envelope.ReceiptHandle, options.VisibilityTimeout, cancellationToken);
            await heartbeatAsync(envelope.Message, cancellationToken);
        }
    }
}
