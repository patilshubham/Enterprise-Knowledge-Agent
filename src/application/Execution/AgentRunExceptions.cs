namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>TASK-RDSH-01/02: thrown when an agent run doesn't exist, or exists but isn't visible in the requested scope (cross-application access - 404-masking, mirrors <c>OutputNotFoundException</c>).</summary>
public sealed class AgentRunNotFoundException(Guid runId) : Exception($"Agent run '{runId}' was not found.")
{
}
