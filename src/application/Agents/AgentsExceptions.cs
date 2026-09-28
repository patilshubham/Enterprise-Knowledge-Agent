namespace EnterpriseKnowledgeAgent.Application.Agents;

/// <summary>TASK-AGT-02: thrown when an agentId doesn't resolve to an existing agent (or doesn't belong to the requested application). The Api layer maps this to <c>404 Not Found</c>.</summary>
public sealed class AgentNotFoundException(Guid agentId) : Exception($"Agent '{agentId}' was not found.")
{
    public Guid AgentId { get; } = agentId;
}

/// <summary>TASK-AGT-03: thrown when create/edit parameters fail deployment-capability or range validation (FR-PAR-004). The Api layer maps this to <c>400 Bad Request</c>.</summary>
public sealed class AgentValidationException(string message) : Exception(message);
