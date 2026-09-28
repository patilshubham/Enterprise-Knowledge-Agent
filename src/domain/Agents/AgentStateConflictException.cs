namespace EnterpriseKnowledgeAgent.Domain.Agents;

/// <summary>TASK-AGT-01: thrown by <see cref="Agent"/> when an illegal state transition (Enable/Disable/edit-after-delete) is attempted.</summary>
public sealed class AgentStateConflictException(string message) : InvalidOperationException(message);
