using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Domain.Citations;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-CIT-01/CIT-03: EF Core-backed <see cref="ICitationRepository"/>.</summary>
public sealed class CitationRepository(EnterpriseKnowledgeAgentDbContext dbContext) : ICitationRepository
{
    public async Task<IReadOnlyList<Citation>> ListByOutputAsync(Guid generatedOutputId, CancellationToken cancellationToken = default) =>
        await dbContext.Citations
            .Where(c => c.GeneratedOutputId == generatedOutputId)
            .ToListAsync(cancellationToken);

    public async Task AddRangeAsync(IReadOnlyList<Citation> citations, CancellationToken cancellationToken = default)
    {
        await dbContext.Citations.AddRangeAsync(citations, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
