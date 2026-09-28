using EnterpriseKnowledgeAgent.Shared.Errors;

namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>
/// TASK-PRMT-01/GEN-03/CIT-03: a classified failure raised anywhere in the
/// agent run execution pipeline (context budgeting, generation, citation
/// validation) - the worker (TASK-RUN-02) catches this generically and calls
/// <c>AgentRun.Fail(Category, Message, now)</c>, mirroring
/// <see cref="EnterpriseKnowledgeAgent.Application.AzureOpenAI.AzureOpenAiException"/>'s
/// established pattern for AOAI-specific failures.
/// </summary>
public sealed class AgentRunExecutionException(ErrorCategory category, string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException)
{
    public ErrorCategory Category { get; } = category;
}
