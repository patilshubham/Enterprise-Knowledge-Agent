using System.Reflection;
using System.Text.Json;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Realtime;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Shared.Tests.Realtime;

/// <summary>
/// TASK-SIG-02: unit coverage for the event catalog/group builders and a
/// content-safety guard over every <c>*Payload</c> record (mirrors
/// <c>QueueMessageContentSafetyTests</c>'s convention for
/// <see cref="EnterpriseKnowledgeAgent.Shared.Messaging.IQueueMessage"/>) -
/// payloads may only carry identifiers/enums/small counters, never file/
/// output content, prompts, or secrets (ASDD §18.1/§18.3).
/// </summary>
public class RealtimePayloadsTests
{
    private static readonly string[] ForbiddenPropertyNameFragments =
    [
        "content",
        "prompt",
        "secret",
        "password",
        "token",
        "apikey",
    ];

    public static IEnumerable<object[]> PayloadTypes()
    {
        var assembly = typeof(RealtimeEventTypes).Assembly;

        return assembly.GetTypes()
            .Where(t => t.Namespace == typeof(RealtimeEventTypes).Namespace && t.Name.EndsWith("Payload", StringComparison.Ordinal))
            .Select(t => new object[] { t });
    }

    [Theory]
    [MemberData(nameof(PayloadTypes))]
    public void Payload_ShouldNotExposeContentOrSecretProperties(Type payloadType)
    {
        var offendingProperties = payloadType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => ForbiddenPropertyNameFragments.Any(fragment =>
                p.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Name)
            .ToList();

        offendingProperties.Should().BeEmpty(
            $"realtime payload '{payloadType.Name}' must carry identifiers/enums/counters only, never raw content/prompt/secret data");
    }

    [Fact]
    public void PayloadTypes_ShouldIncludeAllKnownPayloads()
    {
        var discovered = PayloadTypes().Select(args => (Type)args[0]).ToList();

        discovered.Should().Contain(
        [
            typeof(FileQueuedPayload),
            typeof(FileStageChangedPayload),
            typeof(FileCompletedPayload),
            typeof(FileFailedPayload),
            typeof(RunQueuedPayload),
            typeof(RunStageChangedPayload),
            typeof(RunCompletedPayload),
            typeof(RunFailedPayload),
            typeof(RunCancelledPayload),
            typeof(DashboardChangedPayload),
        ]);
    }

    [Fact]
    public void FileStageChangedEnvelope_ShouldRoundTripThroughJson()
    {
        var original = new SignalREvent<FileStageChangedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.FileStageChanged, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, Sequence: 7, new FileStageChangedPayload(Guid.NewGuid(), ProcessingStage.Chunking, 60));

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SignalREvent<FileStageChangedPayload>>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void RunFailedEnvelope_ShouldRoundTripThroughJson()
    {
        var original = new SignalREvent<RunFailedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunFailed, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, Sequence: 3, new RunFailedPayload(Guid.NewGuid(), ErrorCategory.Timeout));

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<SignalREvent<RunFailedPayload>>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void RealtimePublishRequest_ShouldRoundTripEnvelopeAsJsonElement()
    {
        // TASK-SIG-03/04: this is exactly how HttpRealtimeEventPublisher (worker
        // side) serializes and RealtimeEndpoints (Api side) deserializes/relays.
        var envelope = new SignalREvent<RunCompletedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunCompleted, Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, Sequence: 1, new RunCompletedPayload(Guid.NewGuid()));

        var request = new RealtimePublishRequest(
            RealtimeEventTypes.RunCompleted, [RealtimeGroups.Run(envelope.EntityId)], JsonSerializer.SerializeToElement(envelope));

        var json = JsonSerializer.Serialize(request);
        var deserializedRequest = JsonSerializer.Deserialize<RealtimePublishRequest>(json);

        deserializedRequest.Should().NotBeNull();
        deserializedRequest!.EventType.Should().Be(RealtimeEventTypes.RunCompleted);
        deserializedRequest.Groups.Should().ContainSingle().Which.Should().Be($"run:{envelope.EntityId}");

        var roundTrippedEnvelope = deserializedRequest.Envelope.Deserialize<SignalREvent<RunCompletedPayload>>();
        roundTrippedEnvelope.Should().Be(envelope);
    }

    [Fact]
    public void RealtimeGroups_ShouldFormatWellKnownGroupNames()
    {
        var userId = Guid.NewGuid();
        var applicationId = Guid.NewGuid();
        var runId = Guid.NewGuid();

        RealtimeGroups.User(userId).Should().Be($"user:{userId}");
        RealtimeGroups.Application(applicationId).Should().Be($"application:{applicationId}");
        RealtimeGroups.Run(runId).Should().Be($"run:{runId}");
        RealtimeGroups.OperationsGroup.Should().Be("platform:operations");
    }
}
