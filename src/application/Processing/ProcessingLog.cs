using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>
/// Source-generated log messages (CA1848) for <see cref="DocumentProcessingOrchestrator"/>
/// (mirrors the Infrastructure <c>JobsLog</c> pattern).
/// </summary>
internal static partial class ProcessingLog
{
    [LoggerMessage(Level = LogLevel.Error, Message = "No processing job found for knowledge file '{FileId}' - dropping message.")]
    public static partial void NoJobFoundForFile(this ILogger logger, Guid fileId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Document processing failed retryably for file '{FileId}' (category {Category}, attempt {Attempt}) - requeued.")]
    public static partial void RetryableFailureRequeued(this ILogger logger, Exception exception, Guid fileId, EnterpriseKnowledgeAgent.Shared.Errors.ErrorCategory category, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Document processing failed terminally for file '{FileId}' (category {Category}).")]
    public static partial void TerminalFailure(this ILogger logger, Exception exception, Guid fileId, EnterpriseKnowledgeAgent.Shared.Errors.ErrorCategory category);
}
