using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Realtime;

public class SignalREventTests
{
    private sealed record FileStatusChangedPayload(Guid KnowledgeFileId, string Status);

    [Fact]
    public void SignalREvent_ShouldRoundTripThroughJson()
    {
        var original = new SignalREvent<FileStatusChangedPayload>(
            EventId: Guid.NewGuid(),
            EventType: "knowledge-file.status-changed",
            ApplicationId: Guid.NewGuid(),
            EntityId: Guid.NewGuid(),
            OccurredAtUtc: DateTimeOffset.UtcNow,
            Sequence: 42,
            Payload: new FileStatusChangedPayload(Guid.NewGuid(), "Processing"));

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SignalREvent<FileStatusChangedPayload>>(json);

        deserialized.Should().Be(original);
    }
}
