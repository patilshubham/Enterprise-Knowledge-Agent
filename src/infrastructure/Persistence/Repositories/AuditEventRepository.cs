using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;

/// <summary>
/// TASK-AUD-01: EF Core-backed <see cref="IAuditWriter"/> + <see cref="IAuditEventRepository"/>.
/// <see cref="WriteAsync"/> deliberately never throws: an audit-write failure
/// (e.g. a transient DB error) must not block the primary action it is
/// recording, so failures are caught and logged here instead.
/// </summary>
public sealed class AuditEventRepository(
    EnterpriseKnowledgeAgentDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<AuditEventRepository> logger) : IAuditWriter, IAuditEventRepository
{
    public async Task WriteAsync(
        string action,
        string actorSubject,
        AuditOutcome outcome = AuditOutcome.Success,
        Guid? applicationId = null,
        string? entityType = null,
        string? entityId = null,
        string? entityDisplayName = null,
        string? failureReason = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditEvent = AuditEvent.Create(
                action,
                actorSubject,
                outcome,
                timeProvider.GetUtcNow(),
                applicationId,
                entityType,
                entityId,
                entityDisplayName,
                failureReason,
                correlationId);

            await AddAsync(auditEvent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.AuditWriteFailed(ex, action);
        }
    }

    public async Task AddAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        await dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AuditEventSearchResult> SearchAsync(AuditEventSearchFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AuditEvents.AsNoTracking().AsQueryable();

        if (filter.ApplicationId is { } applicationId)
        {
            query = query.Where(e => e.ApplicationId == applicationId);
        }

        if (!string.IsNullOrWhiteSpace(filter.ActorSubject))
        {
            query = query.Where(e => e.ActorSubject == filter.ActorSubject);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            query = query.Where(e => e.Action == filter.Action);
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityType))
        {
            query = query.Where(e => e.EntityType == filter.EntityType);
        }

        if (filter.Outcome is { } outcome)
        {
            query = query.Where(e => e.Outcome == outcome);
        }

        if (filter.From is { } from)
        {
            query = query.Where(e => e.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(e => e.OccurredAt <= to);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new AuditEventSearchResult(items, totalCount);
    }
}
