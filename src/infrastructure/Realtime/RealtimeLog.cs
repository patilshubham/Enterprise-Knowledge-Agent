using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Realtime;

/// <summary>Source-generated log messages (CA1848) for <see cref="HttpRealtimeEventPublisher"/> (TASK-SIG-03/04).</summary>
internal static partial class RealtimeLog
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime publish of '{EventType}' for entity '{EntityId}' failed with status {StatusCode}.")]
    public static partial void RealtimePublishFailedWithStatus(this ILogger logger, string eventType, Guid entityId, int statusCode);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Realtime publish of '{EventType}' for entity '{EntityId}' threw and was swallowed.")]
    public static partial void RealtimePublishThrew(this ILogger logger, Exception exception, string eventType, Guid entityId);
}
