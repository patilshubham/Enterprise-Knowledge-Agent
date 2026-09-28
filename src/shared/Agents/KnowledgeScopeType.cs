namespace EnterpriseKnowledgeAgent.Shared.Agents;

/// <summary>
/// TASK-AGT-01/FR-AGT-004: which knowledge files an agent's retrieval may
/// search. Resolved to a concrete file-id set by
/// <c>AgentKnowledgeScopeResolver</c> (TASK-AGT-05) before every run.
/// </summary>
public enum KnowledgeScopeType
{
    /// <summary>Every ready+enabled file in the application at run time (no fixed list stored).</summary>
    AllReadyEnabled,

    /// <summary>A fixed set of file ids (<c>Agent.SelectedFileIds</c>), each still required to be ready+enabled at run time.</summary>
    Selected,
}
