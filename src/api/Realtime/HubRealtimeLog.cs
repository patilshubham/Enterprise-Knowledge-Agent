using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Api.Realtime;

/// <summary>Source-generated log messages (CA1848) for <see cref="HubContextRealtimeEventPublisher"/> (TASK-SIG-03/04).</summary>
internal static partial class HubRealtimeLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime publish of '{EventType}' for entity '{EntityId}' threw and was swallowed.")]
    public static partial void RealtimePublishThrew(this ILogger logger, Exception exception, string eventType, Guid entityId);
}
