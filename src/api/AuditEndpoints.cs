using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Domain.Audit;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-AUD-03: platform-wide and per-application audit search endpoints
/// (FR-17, ASDD §7.2/§19.3). Open to any caller - there is no
/// login/authorization in this app. The per-application route is a
/// convenience/filtered view - the <c>applicationId</c> route segment scopes
/// the query.
/// </summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/platform/audit", async (
            string? actor,
            string? action,
            string? entityType,
            AuditOutcome? outcome,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            IAuditEventRepository repository,
            CancellationToken cancellationToken) =>
        {
            var filter = new AuditEventSearchFilter(
                ApplicationId: null,
                ActorSubject: actor,
                Action: action,
                EntityType: entityType,
                Outcome: outcome,
                From: from,
                To: to,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await repository.SearchAsync(filter, cancellationToken);
            return Results.Ok(AuditEventSearchResponse.From(result, filter.Page, filter.PageSize));
        })
            .WithTags("Audit");

        endpoints.MapGet("/api/applications/{applicationId:guid}/activity", async (
            Guid applicationId,
            string? actor,
            string? action,
            string? entityType,
            AuditOutcome? outcome,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            ApplicationsService applicationsService,
            IAuditEventRepository repository,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await applicationsService.GetAsync(applicationId, cancellationToken);
            }
            catch (ApplicationNotFoundException)
            {
                return Results.NotFound();
            }

            var filter = new AuditEventSearchFilter(
                ApplicationId: applicationId,
                ActorSubject: actor,
                Action: action,
                EntityType: entityType,
                Outcome: outcome,
                From: from,
                To: to,
                Page: page ?? 1,
                PageSize: pageSize ?? 50);

            var result = await repository.SearchAsync(filter, cancellationToken);
            return Results.Ok(AuditEventSearchResponse.From(result, filter.Page, filter.PageSize));
        })
            .WithTags("Audit");

        return endpoints;
    }
}

/// <summary>TASK-AUD-03: a single audit row shaped for the frontend (mirrors <see cref="AuditEvent"/>, no secrets).</summary>
public sealed record AuditEventResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string Action,
    string ActorSubject,
    string Outcome,
    Guid? ApplicationId,
    string? EntityType,
    string? EntityId,
    string? EntityDisplayName,
    string? FailureReason,
    Guid? CorrelationId)
{
    public static AuditEventResponse From(AuditEvent e) => new(
        e.Id, e.OccurredAt, e.Action, e.ActorSubject, e.Outcome.ToString(),
        e.ApplicationId, e.EntityType, e.EntityId, e.EntityDisplayName, e.FailureReason, e.CorrelationId);
}

/// <summary>TASK-AUD-03: a page of audit results plus paging metadata.</summary>
public sealed record AuditEventSearchResponse(
    IReadOnlyList<AuditEventResponse> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    public static AuditEventSearchResponse From(AuditEventSearchResult result, int page, int pageSize) => new(
        result.Items.Select(AuditEventResponse.From).ToList(), result.TotalCount, page, pageSize);
}
