using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using EnterpriseKnowledgeAgent.Shared.Observability;

namespace EnterpriseKnowledgeAgent.Infrastructure.Messaging;

/// <summary>
/// TASK-SQS-01: <see cref="IMessageQueue"/> backed by the AWS SDK SQS client
/// configured for LocalStack. Queue URLs are resolved by logical name once
/// and cached (LocalStack/SQS URLs are stable for the container lifetime).
/// </summary>
public sealed class SqsMessageQueue(IAmazonSQS sqsClient) : IMessageQueue
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    private readonly ConcurrentDictionary<string, Task<string>> queueUrlCache = new();

    public async Task<string> SendAsync<TMessage>(string queueName, TMessage message, CancellationToken cancellationToken = default)
        where TMessage : IQueueMessage
    {
        var queueUrl = await ResolveQueueUrlAsync(queueName, cancellationToken);
        var response = await sqsClient.SendMessageAsync(
            new SendMessageRequest(queueUrl, JsonSerializer.Serialize(message, SerializerOptions)),
            cancellationToken);
        return response.MessageId;
    }

    public async Task<IReadOnlyList<QueueMessageEnvelope<TMessage>>> ReceiveAsync<TMessage>(
        string queueName,
        int maxMessages,
        TimeSpan visibilityTimeout,
        CancellationToken cancellationToken = default)
        where TMessage : IQueueMessage
    {
        var queueUrl = await ResolveQueueUrlAsync(queueName, cancellationToken);
        var response = await sqsClient.ReceiveMessageAsync(
            new ReceiveMessageRequest
            {
                QueueUrl = queueUrl,
                MaxNumberOfMessages = Math.Clamp(maxMessages, 1, 10),
                VisibilityTimeout = (int)visibilityTimeout.TotalSeconds,
                WaitTimeSeconds = 10,
                MessageSystemAttributeNames = ["ApproximateReceiveCount", "SentTimestamp"],
            },
            cancellationToken);

        var envelopes = new List<QueueMessageEnvelope<TMessage>>(response.Messages.Count);
        foreach (var message in response.Messages)
        {
            var payload = JsonSerializer.Deserialize<TMessage>(message.Body, SerializerOptions);
            if (payload is null)
            {
                continue;
            }

            var receiveCount = message.Attributes.TryGetValue("ApproximateReceiveCount", out var raw) && int.TryParse(raw, out var parsed)
                ? parsed
                : 1;

            // TASK-OBS-02: SentTimestamp (epoch ms) vs now is a proxy for
            // "oldest visible message age" - the max observed wait time
            // across a burst of polls approximates the oldest queued message's age.
            if (message.Attributes.TryGetValue("SentTimestamp", out var sentRaw) && long.TryParse(sentRaw, out var sentMs))
            {
                var waitSeconds = (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeMilliseconds(sentMs)).TotalSeconds;
                AppMetrics.QueueMessageWaitSeconds.Record(Math.Max(0, waitSeconds), new TagList { { "queue.name", queueName } });
            }

            envelopes.Add(new QueueMessageEnvelope<TMessage>(payload, message.ReceiptHandle, receiveCount));
        }

        return envelopes;
    }

    public async Task DeleteAsync(string queueName, string receiptHandle, CancellationToken cancellationToken = default)
    {
        var queueUrl = await ResolveQueueUrlAsync(queueName, cancellationToken);
        await sqsClient.DeleteMessageAsync(queueUrl, receiptHandle, cancellationToken);
    }

    public async Task ExtendVisibilityAsync(string queueName, string receiptHandle, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default)
    {
        var queueUrl = await ResolveQueueUrlAsync(queueName, cancellationToken);
        await sqsClient.ChangeMessageVisibilityAsync(queueUrl, receiptHandle, (int)visibilityTimeout.TotalSeconds, cancellationToken);
    }

    public async Task<QueueDepth> GetApproximateDepthAsync(string queueName, CancellationToken cancellationToken = default)
    {
        var queueUrl = await ResolveQueueUrlAsync(queueName, cancellationToken);
        var attributes = await sqsClient.GetQueueAttributesAsync(
            new GetQueueAttributesRequest
            {
                QueueUrl = queueUrl,
                AttributeNames = ["ApproximateNumberOfMessages", "ApproximateNumberOfMessagesNotVisible"],
            },
            cancellationToken);

        long dlqCount = 0;
        try
        {
            var dlqUrl = await ResolveQueueUrlAsync(queueName + SqsOptions.DeadLetterSuffix, cancellationToken);
            var dlqAttributes = await sqsClient.GetQueueAttributesAsync(
                new GetQueueAttributesRequest { QueueUrl = dlqUrl, AttributeNames = ["ApproximateNumberOfMessages"] },
                cancellationToken);
            dlqCount = dlqAttributes.ApproximateNumberOfMessages;
        }
        catch (QueueDoesNotExistException)
        {
            // No DLQ provisioned for this queue name - report zero rather than failing depth reporting.
        }

        var depth = new QueueDepth(
            attributes.ApproximateNumberOfMessages,
            attributes.ApproximateNumberOfMessagesNotVisible,
            dlqCount);

        AppMetrics.QueueDepth.Record(depth.ApproximateMessageCount, new TagList { { "queue.name", queueName }, { "queue.state", "visible" } });
        AppMetrics.QueueDepth.Record(depth.ApproximateNotVisibleCount, new TagList { { "queue.name", queueName }, { "queue.state", "in_flight" } });
        AppMetrics.QueueDepth.Record(depth.DeadLetterQueueMessageCount, new TagList { { "queue.name", queueName }, { "queue.state", "dead_letter" } });

        return depth;
    }

    public async Task<QueueHealth> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await sqsClient.ListQueuesAsync(new ListQueuesRequest { MaxResults = 1 }, cancellationToken);
            return new QueueHealth(IsAvailable: true, Detail: null);
        }
        catch (Exception ex) when (ex is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            return new QueueHealth(IsAvailable: false, Detail: ex.Message);
        }
    }

    private Task<string> ResolveQueueUrlAsync(string queueName, CancellationToken cancellationToken) =>
        queueUrlCache.GetOrAdd(queueName, name => ResolveQueueUrlCoreAsync(name, cancellationToken));

    private async Task<string> ResolveQueueUrlCoreAsync(string queueName, CancellationToken cancellationToken)
    {
        var response = await sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
        return response.QueueUrl;
    }
}
