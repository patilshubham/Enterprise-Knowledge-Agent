using EnterpriseKnowledgeAgent.Application.Applications;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-APP-01: EF Core-backed <see cref="IApplicationRepository"/>.</summary>
public sealed class ApplicationRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IApplicationRepository
{
    public Task<ApplicationEntity?> GetAsync(Guid applicationId, CancellationToken cancellationToken = default) =>
        dbContext.Applications.SingleOrDefaultAsync(a => a.Id == applicationId, cancellationToken);

    public async Task<IReadOnlyList<ApplicationEntity>> ListAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Applications
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ApplicationEntity application, CancellationToken cancellationToken = default) =>
        await dbContext.Applications.AddAsync(application, cancellationToken);

    public Task<bool> NameExistsAsync(string name, Guid? excludeApplicationId = null, CancellationToken cancellationToken = default)
    {
        var normalized = name.Trim().ToLowerInvariant();

        // Compares against the "NormalizedName" computed column (see
        // ApplicationConfiguration) rather than translating ToLower()/
        // string.Equals(..., StringComparison) into SQL, which Npgsql
        // either can't translate or would evaluate case-sensitively.
        var query = dbContext.Applications
            .Where(a => a.Status != Shared.Applications.ApplicationStatus.Deleted)
            .Where(a => EF.Property<string>(a, "NormalizedName") == normalized);

        if (excludeApplicationId is { } excludeId)
        {
            query = query.Where(a => a.Id != excludeId);
        }

        return query.AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Shared.Applications.ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default)
    {
        var counts = await dbContext.Applications
            .AsNoTracking()
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
