using EnterpriseKnowledgeAgent.Domain.Operations;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Operations;

/// <summary>TASK-JOB-04: transactional outbox row lifecycle (Decision D1).</summary>
public sealed class OutboxMessageTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_undelivered_with_zero_attempts()
    {
        var message = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, "{}", Now);

        message.MessageType.Should().Be(OutboxMessageType.DocumentProcessing);
        message.PayloadJson.Should().Be("{}");
        message.Delivered.Should().BeFalse();
        message.DeliveryAttempts.Should().Be(0);
        message.LastError.Should().BeNull();
        message.CreatedAt.Should().Be(Now);
        message.DeliveredAt.Should().BeNull();
    }

    [Fact]
    public void MarkDelivered_sets_delivered_flag_and_clears_last_error()
    {
        var message = OutboxMessage.Create(OutboxMessageType.AgentExecution, "{}", Now);
        message.RecordDeliveryFailure("timeout", Now.AddSeconds(1));

        message.MarkDelivered(Now.AddSeconds(2));

        message.Delivered.Should().BeTrue();
        message.DeliveredAt.Should().Be(Now.AddSeconds(2));
        message.LastError.Should().BeNull();
    }

    [Fact]
    public void RecordDeliveryFailure_increments_attempts_and_stores_error_without_marking_delivered()
    {
        var message = OutboxMessage.Create(OutboxMessageType.Deletion, "{}", Now);

        message.RecordDeliveryFailure("connection refused", Now.AddSeconds(1));
        message.RecordDeliveryFailure("connection refused", Now.AddSeconds(2));

        message.DeliveryAttempts.Should().Be(2);
        message.LastError.Should().Be("connection refused");
        message.Delivered.Should().BeFalse();
    }
}
