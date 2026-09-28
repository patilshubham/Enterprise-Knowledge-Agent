using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Domain.Citations;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-CIT-01/CIT-02: EF Core-backed <see cref="IRunEvidenceRepository"/>.</summary>
public sealed class RunEvidenceRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IRunEvidenceRepository
{
    public async Task<IReadOnlyList<RunEvidence>> ListByRunAsync(Guid agentRunId, CancellationToken cancellationToken = default) =>
        await dbContext.RunEvidences
            .Where(e => e.AgentRunId == agentRunId)
            .OrderBy(e => e.SourceId)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IReadOnlyList<RunEvidence> evidence, CancellationToken cancellationToken = default)
    {
        await dbContext.RunEvidences.AddRangeAsync(evidence, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
