using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Application.Applications;

/// <summary>
/// TASK-APP-01: persistence contract for the <c>Application</c> aggregate.
/// Every method that targets a single application takes its
/// <c>applicationId</c> explicitly (never "load all, then filter") - the
/// convention TASK-AUTHZ-02 establishes for every resource repository.
/// </summary>
public interface IApplicationRepository
{
    Task<ApplicationEntity?> GetAsync(Guid applicationId, CancellationToken cancellationToken = default);

    /// <summary>All applications, including archived/deleted. Callers filter by role/status as needed (TASK-APP-02).</summary>
    Task<IReadOnlyList<ApplicationEntity>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ApplicationEntity application, CancellationToken cancellationToken = default);

    /// <summary>
    /// True if a non-deleted application already has <paramref name="name"/>
    /// (case-insensitive), excluding <paramref name="excludeApplicationId"/>
    /// (used when checking a rename). Deleted applications' names are free to
    /// reuse (TASK-APP-01's uniqueness policy).
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? excludeApplicationId = null, CancellationToken cancellationToken = default);

    /// <summary>TASK-PDSH-01: single aggregate query returning application counts per <see cref="Shared.Applications.ApplicationStatus"/> (platform-wide only - there is no application-scoped variant of this count).</summary>
    Task<IReadOnlyDictionary<Shared.Applications.ApplicationStatus, int>> CountByStatusAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
