using EnterpriseKnowledgeAgent.Shared.Agents;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Application.Agents;

/// <summary>
/// TASK-AGT-05: resolves an <see cref="AgentEntity"/>'s configured knowledge
/// scope into the <see cref="Retrieval.RetrievalRequest.SelectedFileIds"/>
/// shape retrieval already understands. This is intentionally a thin
/// adapter: TASK-RET-01/05 already implemented every actual validation rule
/// this task calls for - <c>RetrievalService</c> scopes candidate search to
/// the application (so a foreign file id can never match), silently treats
/// any requested id that isn't Ready+enabled as excluded (via
/// <see cref="Retrieval.RetrievalResult.ExcludedFileIds"/>), and searches
/// every Ready+enabled file in the application when no explicit selection is
/// supplied. Module 19/20 (run submission/context assembly) is the actual
/// caller of this resolver once it exists.
/// </summary>
public static class AgentKnowledgeScopeResolver
{
    /// <summary><see langword="null"/> (meaning "every Ready+enabled file in the application") for <see cref="KnowledgeScopeType.AllReadyEnabled"/>, or the agent's fixed file-id list for <see cref="KnowledgeScopeType.Selected"/>.</summary>
    public static IReadOnlyList<Guid>? ResolveSelectedFileIds(AgentEntity agent) =>
        agent.KnowledgeScopeType == KnowledgeScopeType.Selected ? agent.SelectedFileIds : null;
}
