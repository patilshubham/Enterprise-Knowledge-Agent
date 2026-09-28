using EnterpriseKnowledgeAgent.Shared.Errors;

namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>
/// TASK-AOI-03: wraps an Azure OpenAI call failure with its already-classified
/// <see cref="ErrorCategory"/> (Throttled/Timeout/DependencyUnavailable/
/// Configuration/ContextOverflow) so callers such as
/// <c>DocumentProcessingOrchestrator.ClassifyException</c> can map it straight
/// through without re-deriving the category from the underlying Azure SDK
/// exception type.
/// </summary>
public sealed class AzureOpenAiException(ErrorCategory category, string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public ErrorCategory Category { get; } = category;
}
