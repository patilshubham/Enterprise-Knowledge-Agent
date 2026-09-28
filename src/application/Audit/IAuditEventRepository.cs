using EnterpriseKnowledgeAgent.Domain.Audit;

namespace EnterpriseKnowledgeAgent.Application.Audit;

/// <summary>TASK-AUD-03: filter/paging parameters for searching audit events.</summary>
/// <param name="ApplicationId">When set, restricts results to this application; when null, includes platform-level events plus every application's events (platform-role search).</param>
public sealed record AuditEventSearchFilter(
    Guid? ApplicationId = null,
    string? ActorSubject = null,
    string? Action = null,
    string? EntityType = null,
    AuditOutcome? Outcome = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 50);

/// <summary>TASK-AUD-03: a page of audit events plus the total matching count (for pagination controls).</summary>
public sealed record AuditEventSearchResult(IReadOnlyList<AuditEvent> Items, int TotalCount);

/// <summary>TASK-AUD-01/03: persistence + search for audit events.</summary>
public interface IAuditEventRepository
{
    Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);

    Task<AuditEventSearchResult> SearchAsync(AuditEventSearchFilter filter, CancellationToken cancellationToken = default);
}
