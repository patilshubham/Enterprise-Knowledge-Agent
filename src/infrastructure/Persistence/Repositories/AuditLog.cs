using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>Source-generated log messages (CA1848) for <see cref="AuditEventRepository"/> (TASK-AUD-01).</summary>
internal static partial class AuditLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to write audit event for action {Action}; the primary action was not affected.")]
    public static partial void AuditWriteFailed(this ILogger logger, Exception exception, string action);
}
