namespace EnterpriseKnowledgeAgent.Infrastructure.Realtime;

/// <summary>
/// TASK-SIG-03/04: configuration for cross-process realtime-event
/// publishing. <see cref="ApiBaseUrl"/> is only used by the worker-side
/// <c>HttpRealtimeEventPublisher</c> (the Api process publishes in-process
/// and ignores it); <see cref="InternalToken"/> is used both by that
/// publisher (as the outbound shared-secret header) and by the Api's
/// internal publish endpoint (to validate it) - the same value must be
/// configured on all three processes (see docker-compose.yml's
/// <c>Realtime__*</c> env vars).
/// </summary>
public sealed class RealtimeOptions
{
    public const string SectionName = "Realtime";

    public string ApiBaseUrl { get; set; } = string.Empty;

    public string InternalToken { get; set; } = string.Empty;
}
