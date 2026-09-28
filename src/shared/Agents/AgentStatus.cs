namespace EnterpriseKnowledgeAgent.Shared.Agents;

/// <summary>
/// Lifecycle state of an <c>Agent</c> (TASK-AGT-01, FR-AGT-001). Unlike
/// <c>ApplicationStatus</c> (Active/Archived/Deleted), agents are toggled via
/// Enable/Disable rather than Archive/Restore (TASK-AGT-02's endpoint
/// contract: <c>POST …/{id}/enable|disable</c>) - <see cref="Disabled"/> is
/// not a terminal state and can be re-enabled at any time.
/// </summary>
public enum AgentStatus
{
    Active,
    Disabled,
    Deleted,
}
