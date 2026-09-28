using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Messaging;

public class QueueMessageSerializationTests
{
    [Fact]
    public void DocumentProcessingMessage_ShouldRoundTripThroughJson()
    {
        var original = new DocumentProcessingMessage(
            MessageId: Guid.NewGuid(),
            ApplicationId: Guid.NewGuid(),
            KnowledgeFileId: Guid.NewGuid(),
            CorrelationId: CorrelationId.New(),
            AttemptNumber: 1);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DocumentProcessingMessage>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void AgentExecutionMessage_ShouldRoundTripThroughJson()
    {
        var original = new AgentExecutionMessage(
            MessageId: Guid.NewGuid(),
            ApplicationId: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            RunId: Guid.NewGuid(),
            CorrelationId: CorrelationId.New(),
            AttemptNumber: 2);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<AgentExecutionMessage>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void DeletionMessage_ShouldRoundTripThroughJson()
    {
        var original = new DeletionMessage(
            MessageId: Guid.NewGuid(),
            ApplicationId: Guid.NewGuid(),
            KnowledgeFileId: Guid.NewGuid(),
            CorrelationId: CorrelationId.New(),
            AttemptNumber: 3);

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<DeletionMessage>(json);

        deserialized.Should().Be(original);
    }
}
