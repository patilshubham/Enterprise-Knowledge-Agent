using EnterpriseKnowledgeAgent.Api.HealthChecks;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.Tests.HealthChecks;

/// <summary>TASK-OPS-01: <see cref="MessageQueueHealthCheck"/> maps <see cref="IMessageQueue.CheckHealthAsync"/> to a readiness result.</summary>
public sealed class MessageQueueHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_message_queue_reports_available()
    {
        var check = new MessageQueueHealthCheck(new FakeMessageQueue(new QueueHealth(true, null)));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_with_detail_when_message_queue_reports_unavailable()
    {
        var check = new MessageQueueHealthCheck(new FakeMessageQueue(new QueueHealth(false, "SQS unreachable")));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("SQS unreachable");
    }

    private sealed class FakeMessageQueue(QueueHealth health) : IMessageQueue
    {
        public Task<string> SendAsync<TMessage>(string queueName, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : IQueueMessage => throw new NotSupportedException();

        public Task<IReadOnlyList<QueueMessageEnvelope<TMessage>>> ReceiveAsync<TMessage>(
            string queueName, int maxMessages, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default)
            where TMessage : IQueueMessage => throw new NotSupportedException();

        public Task DeleteAsync(string queueName, string receiptHandle, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task ExtendVisibilityAsync(string queueName, string receiptHandle, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<QueueDepth> GetApproximateDepthAsync(string queueName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<QueueHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(health);
    }
}
