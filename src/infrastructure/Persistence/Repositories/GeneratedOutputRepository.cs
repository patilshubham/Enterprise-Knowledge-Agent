using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-OUT-01: EF Core-backed <see cref="IGeneratedOutputRepository"/>.</summary>
public sealed class GeneratedOutputRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IGeneratedOutputRepository
{
    public Task AddAsync(GeneratedOutput output, CancellationToken cancellationToken = default) =>
        dbContext.GeneratedOutputs.AddAsync(output, cancellationToken).AsTask();

    public Task<GeneratedOutput?> GetAsync(Guid outputId, CancellationToken cancellationToken = default) =>
        dbContext.GeneratedOutputs.SingleOrDefaultAsync(o => o.Id == outputId, cancellationToken);

    public Task<GeneratedOutput?> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default) =>
        dbContext.GeneratedOutputs.SingleOrDefaultAsync(o => o.AgentRunId == agentRunId, cancellationToken);

    public async Task<IReadOnlyList<GeneratedOutput>> ListAsync(
        Guid applicationId,
        string? search = null,
        Guid? agentId = null,
        string? createdBy = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        JobStatus? runStatus = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GeneratedOutputs.Where(o => o.ApplicationId == applicationId && !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(o => EF.Functions.ILike(o.Title, pattern));
        }

        if (!string.IsNullOrWhiteSpace(createdBy))
        {
            query = query.Where(o => o.CreatedBy == createdBy);
        }

        if (from is not null)
        {
            query = query.Where(o => o.CreatedAt >= from.Value);
        }

        if (to is not null)
        {
            query = query.Where(o => o.CreatedAt <= to.Value);
        }

        if (agentId is not null || runStatus is not null)
        {
            query = query.Where(o => dbContext.AgentRuns.Any(r =>
                r.Id == o.AgentRunId
                && (agentId == null || r.AgentId == agentId)
                && (runStatus == null || r.Status == runStatus)));
        }

        return await query.OrderByDescending(o => o.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
