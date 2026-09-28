using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-SQS-01/02/03/04: send/receive/delete, visibility-timeout redelivery,
/// and queue-depth reporting against the real LocalStack SQS (see
/// <see cref="LocalStackFixture"/>). Requires
/// `docker compose --profile diagnostics up -d localstack` to be running.
/// </summary>
public sealed class SqsMessageQueueTests
{
    private readonly SqsMessageQueue queue;

    public SqsMessageQueueTests()
    {
        queue = new SqsMessageQueue(LocalStackFixture.CreateSqsClient());
    }

    private static DeletionMessage NewMessage(int attemptNumber = 1) =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CorrelationId.New(), attemptNumber);

    [Fact]
    public async Task SendAsync_then_ReceiveAsync_round_trips_the_message_and_DeleteAsync_removes_it()
    {
        var message = NewMessage();

        await queue.SendAsync(LocalStackFixture.DeletionJobsQueue, message);

        var envelope = await ReceiveOneMatchingAsync(message.MessageId);

        envelope.Should().NotBeNull();
        envelope!.Message.ApplicationId.Should().Be(message.ApplicationId);
        envelope.Message.KnowledgeFileId.Should().Be(message.KnowledgeFileId);
        envelope.ApproximateReceiveCount.Should().BeGreaterThanOrEqualTo(1);

        await queue.DeleteAsync(LocalStackFixture.DeletionJobsQueue, envelope.ReceiptHandle);
    }

    [Fact]
    public async Task ReceiveAsync_a_message_still_within_its_visibility_timeout_is_not_redelivered_to_a_second_receiver()
    {
        var message = NewMessage();
        await queue.SendAsync(LocalStackFixture.DeletionJobsQueue, message);

        var first = await ReceiveOneMatchingAsync(message.MessageId, visibilityTimeout: TimeSpan.FromSeconds(30));
        first.Should().NotBeNull();

        var second = await ReceiveOneMatchingAsync(message.MessageId, visibilityTimeout: TimeSpan.FromSeconds(30), maxAttempts: 1);
        second.Should().BeNull("the message is still invisible to other receivers within its visibility timeout");

        await queue.DeleteAsync(LocalStackFixture.DeletionJobsQueue, first!.ReceiptHandle);
    }

    [Fact]
    public async Task ExtendVisibilityAsync_keeps_the_message_hidden_past_the_original_timeout()
    {
        var message = NewMessage();
        await queue.SendAsync(LocalStackFixture.DeletionJobsQueue, message);

        var received = await ReceiveOneMatchingAsync(message.MessageId, visibilityTimeout: TimeSpan.FromSeconds(2));
        received.Should().NotBeNull();

        await queue.ExtendVisibilityAsync(LocalStackFixture.DeletionJobsQueue, received!.ReceiptHandle, TimeSpan.FromSeconds(30));

        // Wait past the original (now-extended) timeout and confirm it's still not redelivered.
        await Task.Delay(TimeSpan.FromSeconds(3));
        var stillHidden = await ReceiveOneMatchingAsync(message.MessageId, maxAttempts: 1);
        stillHidden.Should().BeNull();

        await queue.DeleteAsync(LocalStackFixture.DeletionJobsQueue, received.ReceiptHandle);
    }

    [Fact]
    public async Task GetApproximateDepthAsync_returns_without_throwing_for_a_provisioned_queue()
    {
        var depth = await queue.GetApproximateDepthAsync(LocalStackFixture.DeletionJobsQueue);

        depth.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_reports_available_when_LocalStack_is_reachable()
    {
        var health = await queue.CheckHealthAsync();

        health.IsAvailable.Should().BeTrue();
    }

    /// <summary>Polls a handful of times to find the envelope matching <paramref name="messageId"/>, tolerating other tests' in-flight messages on the same shared queue.</summary>
    private async Task<QueueMessageEnvelope<DeletionMessage>?> ReceiveOneMatchingAsync(
        Guid messageId,
        TimeSpan? visibilityTimeout = null,
        int maxAttempts = 5)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var envelopes = await queue.ReceiveAsync<DeletionMessage>(
                LocalStackFixture.DeletionJobsQueue,
                maxMessages: 10,
                visibilityTimeout ?? TimeSpan.FromSeconds(30));

            var match = envelopes.FirstOrDefault(e => e.Message.MessageId == messageId);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
    }
}
