using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Application.Agents;

/// <summary>TASK-AGT-01: persistence for <see cref="AgentEntity"/>, scoped by application (ASDD §9.3).</summary>
public interface IAgentRepository
{
    Task AddAsync(AgentEntity agent, CancellationToken cancellationToken = default);

    Task<AgentEntity?> GetAsync(Guid agentId, CancellationToken cancellationToken = default);

    /// <summary>Every agent in the application (any status, including Deleted), newest first - mirrors <see cref="Applications.IApplicationRepository"/>'s convention of never hiding soft-deleted rows so historical <c>AgentRun</c> references stay explicable.</summary>
    Task<IReadOnlyList<AgentEntity>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
