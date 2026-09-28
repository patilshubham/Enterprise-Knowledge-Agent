using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-OUT-02/03/04/05 + TASK-CIT-04: output library/detail/edit/actions
/// endpoints and the citation evidence-drawer endpoints. Open to any caller -
/// there is no login/authorization in this app. The application is loaded
/// first purely to confirm it exists (and, transitively, so a
/// cross-application outputId always 404s).
/// </summary>
public static class OutputsEndpoints
{
    private const string Actor = "local-user";

    public static IEndpointRouteBuilder MapOutputsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications/{applicationId:guid}/outputs")
            .WithTags("Outputs");

        group.MapGet("/", async (
            Guid applicationId,
            string? search,
            Guid? agentId,
            string? createdBy,
            DateTimeOffset? from,
            DateTimeOffset? to,
            JobStatus? status,
            OutputsService service,
            IApplicationRepository applicationRepository,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            var results = await service.ListAsync(applicationId, search, agentId, createdBy, from, to, status, cancellationToken);
            return Results.Ok(results);
        });

        group.MapGet("/{outputId:guid}", async (
            Guid applicationId,
            Guid outputId,
            OutputsService service,
            IApplicationRepository applicationRepository,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                return Results.Ok(await service.GetDetailAsync(applicationId, outputId, cancellationToken));
            }
            catch (Exception ex)
            {
                return MapOutputException(ex);
            }
        });

        group.MapPut("/{outputId:guid}", async (
            Guid applicationId,
            Guid outputId,
            EditOutputRequest request,
            OutputsService service,
            IApplicationRepository applicationRepository,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                var result = await service.EditAsync(applicationId, outputId, request.CurrentMarkdown, Actor, cancellationToken);
                logger.OutputEdited(applicationId, outputId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.OutputEdited, Actor, applicationId: applicationId,
                    entityType: "GeneratedOutput", entityId: outputId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return MapOutputException(ex);
            }
        });

        group.MapPost("/{outputId:guid}/regenerate", async (
            Guid applicationId,
            Guid outputId,
            OutputsService service,
            IApplicationRepository applicationRepository,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                var result = await service.RegenerateAsync(applicationId, outputId, Actor, cancellationToken);
                logger.OutputRegenerated(applicationId, outputId, result.RunId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.OutputRegenerated, Actor, applicationId: applicationId,
                    entityType: "GeneratedOutput", entityId: outputId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Accepted(uri: null, result);
            }
            catch (Exception ex)
            {
                return MapOutputException(ex);
            }
        });

        group.MapDelete("/{outputId:guid}", async (
            Guid applicationId,
            Guid outputId,
            OutputsService service,
            IApplicationRepository applicationRepository,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                await service.DeleteAsync(applicationId, outputId, cancellationToken);
                logger.OutputDeleted(applicationId, outputId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.OutputDeleted, Actor, applicationId: applicationId,
                    entityType: "GeneratedOutput", entityId: outputId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return MapOutputException(ex);
            }
        });

        group.MapGet("/{outputId:guid}/citations", async (
            Guid applicationId,
            Guid outputId,
            OutputsService service,
            IApplicationRepository applicationRepository,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                return Results.Ok(await service.GetCitationsAsync(applicationId, outputId, cancellationToken));
            }
            catch (Exception ex)
            {
                return MapOutputException(ex);
            }
        });

        group.MapGet("/{outputId:guid}/citations/{citationId:guid}", async (
            Guid applicationId,
            Guid outputId,
            Guid citationId,
            OutputsService service,
            IApplicationRepository applicationRepository,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                return Results.Ok(await service.GetCitationAsync(applicationId, outputId, citationId, cancellationToken));
            }
            catch (Exception ex)
            {
                return MapOutputException(ex);
            }
        });

        return endpoints;
    }

    private static IResult MapOutputException(Exception exception) => exception switch
    {
        OutputNotFoundException => Results.NotFound(),
        CitationNotFoundException => Results.NotFound(),
        ArgumentException argumentException => Results.Problem(
            title: "Invalid request",
            detail: argumentException.Message,
            statusCode: StatusCodes.Status400BadRequest),
        _ => throw exception,
    };
}
