using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-KNW-01: persistence for <see cref="KnowledgeFile"/>, scoped by application (ASDD §9.1).</summary>
public interface IKnowledgeFileRepository
{
    Task AddAsync(KnowledgeFile file, CancellationToken cancellationToken = default);

    Task<KnowledgeFile?> GetAsync(Guid fileId, CancellationToken cancellationToken = default);

    /// <summary>TASK-KNW-04: filtered listing scoped to an application, newest upload first.</summary>
    Task<IReadOnlyList<KnowledgeFile>> ListAsync(
        Guid applicationId,
        string? search = null,
        UserFacingFileStatus? status = null,
        CancellationToken cancellationToken = default);

    /// <summary>TASK-KNW-03: FR-KNW-005 duplicate detection - true if another file in the same application already has this content hash and is not being deleted.</summary>
    Task<bool> ExistsActiveHashAsync(Guid applicationId, string contentHash, CancellationToken cancellationToken = default);

    /// <summary>TASK-PDSH-01/TASK-ADSH-01: single aggregate query returning file counts per <see cref="KnowledgeFile.EffectiveStatus"/> (Disabled/IsEnabled-projected, matching the listing UI), optionally scoped to one application (<paramref name="applicationId"/> null = platform-wide).</summary>
    Task<IReadOnlyDictionary<UserFacingFileStatus, int>> CountByStatusAsync(Guid? applicationId, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
