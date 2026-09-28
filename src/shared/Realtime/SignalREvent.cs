namespace EnterpriseKnowledgeAgent.Shared.Realtime;

/// <summary>
/// Envelope wrapping every SignalR push (ASDD §18.3). Clients use
/// <see cref="Sequence"/> to detect and ignore out-of-order/duplicate
/// deliveries and to reconcile missed events after a reconnect via a
/// follow-up REST fetch.
/// </summary>
/// <typeparam name="TPayload">The event-specific payload type (identifiers/status only, never secrets or full content).</typeparam>
public sealed record SignalREvent<TPayload>(
    Guid EventId,
    string EventType,
    Guid ApplicationId,
    Guid EntityId,
    DateTimeOffset OccurredAtUtc,
    long Sequence,
    TPayload Payload);
