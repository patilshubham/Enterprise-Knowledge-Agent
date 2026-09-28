using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Api.ErrorHandling;

/// <summary>Source-generated log messages (CA1848) for <see cref="GlobalExceptionHandler"/> (TASK-ERR-01).</summary>
internal static partial class GlobalExceptionHandlerLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception (correlation '{CorrelationId}', category '{Category}').")]
    public static partial void UnhandledException(this ILogger logger, Exception exception, string correlationId, string category);
}
