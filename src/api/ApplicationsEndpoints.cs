using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Domain.Applications;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-APP-02: Application CRUD + lifecycle endpoints. Open to any caller -
/// there is no login/authorization in this app (Keycloak/RBAC removed;
/// single implicit local user).
/// </summary>
public static class ApplicationsEndpoints
{
    private const string Actor = "local-user";

    public static IEndpointRouteBuilder MapApplicationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var applications = endpoints.MapGroup("/api/applications").WithTags("Applications");

        applications.MapPost("/", async (
            CreateApplicationRequest request,
            ApplicationsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var created = await service.CreateAsync(request, Actor, cancellationToken);
                logger.Created(created.Id, created.Name, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.ApplicationCreated, Actor, applicationId: created.Id,
                    entityType: "Application", entityId: created.Id.ToString(), entityDisplayName: created.Name,
                    cancellationToken: cancellationToken);
                return Results.Created($"/api/applications/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return MapApplicationException(ex);
            }
        });

        applications.MapGet("/", async (
            ApplicationsService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForCallerAsync(cancellationToken)));

        applications.MapGet("/{applicationId:guid}", async (
            Guid applicationId,
            ApplicationsService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var application = await service.GetAsync(applicationId, cancellationToken);
                return Results.Ok(application);
            }
            catch (Exception ex)
            {
                return MapApplicationException(ex);
            }
        });

        applications.MapPut("/{applicationId:guid}", async (
            Guid applicationId,
            UpdateApplicationRequest request,
            ApplicationsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updated = await service.UpdateAsync(applicationId, request, cancellationToken);
                logger.Updated(applicationId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.ApplicationUpdated, Actor, applicationId: applicationId,
                    entityType: "Application", entityId: applicationId.ToString(), entityDisplayName: updated.Name,
                    cancellationToken: cancellationToken);
                return Results.Ok(updated);
            }
            catch (Exception ex)
            {
                return MapApplicationException(ex);
            }
        });

        applications.MapPost("/{applicationId:guid}/archive", async (
            Guid applicationId,
            ApplicationsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var archived = await service.ArchiveAsync(applicationId, cancellationToken);
                logger.Archived(applicationId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.ApplicationArchived, Actor, applicationId: applicationId,
                    entityType: "Application", entityId: applicationId.ToString(), entityDisplayName: archived.Name,
                    cancellationToken: cancellationToken);
                return Results.Ok(archived);
            }
            catch (Exception ex)
            {
                return MapApplicationException(ex);
            }
        });

        applications.MapPost("/{applicationId:guid}/restore", async (
            Guid applicationId,
            ApplicationsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var restored = await service.RestoreAsync(applicationId, cancellationToken);
                logger.Restored(applicationId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.ApplicationRestored, Actor, applicationId: applicationId,
                    entityType: "Application", entityId: applicationId.ToString(), entityDisplayName: restored.Name,
                    cancellationToken: cancellationToken);
                return Results.Ok(restored);
            }
            catch (Exception ex)
            {
                return MapApplicationException(ex);
            }
        });

        applications.MapDelete("/{applicationId:guid}", async (
            Guid applicationId,
            ApplicationsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var deleted = await service.DeleteAsync(applicationId, cancellationToken);
                logger.Deleted(applicationId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.ApplicationDeleted, Actor, applicationId: applicationId,
                    entityType: "Application", entityId: applicationId.ToString(), entityDisplayName: deleted.Name,
                    cancellationToken: cancellationToken);
                return Results.Ok(deleted);
            }
            catch (Exception ex)
            {
                return MapApplicationException(ex);
            }
        });

        return endpoints;
    }

    private static IResult MapApplicationException(Exception exception) => exception switch
    {
        ApplicationNotFoundException => Results.NotFound(),
        ApplicationNameConflictException nameConflict => Results.Problem(
            title: "Application name conflict",
            detail: nameConflict.Message,
            statusCode: StatusCodes.Status409Conflict),
        ApplicationStateConflictException stateConflict => Results.Problem(
            title: "Application state conflict",
            detail: stateConflict.Message,
            statusCode: StatusCodes.Status409Conflict),
        ApplicationReadOnlyException readOnlyConflict => Results.Problem(
            title: "Application is read-only",
            detail: readOnlyConflict.Message,
            statusCode: StatusCodes.Status409Conflict),
        ArgumentException argumentException => Results.Problem(
            title: "Invalid request",
            detail: argumentException.Message,
            statusCode: StatusCodes.Status400BadRequest),
        _ => throw exception,
    };
}
