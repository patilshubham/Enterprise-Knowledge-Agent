using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using Microsoft.EntityFrameworkCore;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>TASK-KNW-01: EF Core-backed <see cref="IKnowledgeFileRepository"/>.</summary>
public sealed class KnowledgeFileRepository(EnterpriseKnowledgeAgentDbContext dbContext) : IKnowledgeFileRepository
{
    public Task AddAsync(KnowledgeFile file, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeFiles.AddAsync(file, cancellationToken).AsTask();

    public Task<KnowledgeFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeFiles.SingleOrDefaultAsync(f => f.Id == fileId, cancellationToken);

    public async Task<IReadOnlyList<KnowledgeFile>> ListAsync(
        Guid applicationId,
        string? search = null,
        UserFacingFileStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeFiles.Where(f => f.ApplicationId == applicationId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(f => EF.Functions.ILike(f.DisplayName, pattern));
        }

        if (status is { } requiredStatus)
        {
            query = requiredStatus == UserFacingFileStatus.Disabled
                ? query.Where(f => !f.IsEnabled)
                : query.Where(f => f.Status == requiredStatus && f.IsEnabled);
        }

        return await query.OrderByDescending(f => f.UploadedAt).ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsActiveHashAsync(Guid applicationId, string contentHash, CancellationToken cancellationToken = default) =>
        dbContext.KnowledgeFiles.AnyAsync(
            f => f.ApplicationId == applicationId
                && f.ContentHash == contentHash
                && f.Status != UserFacingFileStatus.Deleting
                && f.Status != UserFacingFileStatus.Deleted,
            cancellationToken);

    public async Task<IReadOnlyDictionary<UserFacingFileStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.KnowledgeFiles.AsNoTracking().AsQueryable();
        if (applicationId is { } appId)
        {
            query = query.Where(f => f.ApplicationId == appId);
        }

        // `EffectiveStatus` (Disabled/IsEnabled-projected) is a computed C#
        // property, not translatable to SQL - project the two raw columns
        // server-side (one lightweight query) and group in-memory instead.
        var rows = await query
            .Select(f => new { f.Status, f.IsEnabled })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.Status is UserFacingFileStatus.Deleting or UserFacingFileStatus.Deleted
                ? r.Status
                : (!r.IsEnabled ? UserFacingFileStatus.Disabled : r.Status))
            .ToDictionary(g => g.Key, g => g.Count());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
