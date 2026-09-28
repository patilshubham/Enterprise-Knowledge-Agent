using Microsoft.EntityFrameworkCore;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-AGT-01: EF Core-backed <see cref="IAgentRepository"/>.</summary>
public sealed class AgentRepository(EnterpriseKnowledgeAgentDbContext dbContext) : Application.Agents.IAgentRepository
{
    public Task AddAsync(AgentEntity agent, CancellationToken cancellationToken = default) =>
        dbContext.Agents.AddAsync(agent, cancellationToken).AsTask();

    public Task<AgentEntity?> GetAsync(Guid agentId, CancellationToken cancellationToken = default) =>
        dbContext.Agents.SingleOrDefaultAsync(a => a.Id == agentId, cancellationToken);

    public async Task<IReadOnlyList<AgentEntity>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        await dbContext.Agents
            .Where(a => a.ApplicationId == applicationId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
