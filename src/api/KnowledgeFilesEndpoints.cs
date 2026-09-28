using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Domain.Audit;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-KNW-03/04/05: knowledge file upload/list/preview/disable/enable/
/// delete/reprocess endpoints. Open to any caller - there is no
/// login/authorization in this app. Mutations still require an Active
/// (non-archived/deleted) application (<see cref="IApplicationReadOnlyGuard"/>, TASK-APP-04).
/// </summary>
public static class KnowledgeFilesEndpoints
{
    private const string Actor = "local-user";

    public static IEndpointRouteBuilder MapKnowledgeFilesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications/{applicationId:guid}/files").WithTags("Knowledge");

        group.MapPost("/", async (
            Guid applicationId,
            IFormFileCollection files,
            bool? allowDuplicates,
            KnowledgeFilesService service,
            IApplicationReadOnlyGuard readOnlyGuard,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

                var candidates = files.Select(f => new UploadCandidate(f.FileName, f.ContentType, f.OpenReadStream())).ToList();
                var results = await service.UploadAsync(applicationId, candidates, Actor, allowDuplicates ?? false, cancellationToken);
                foreach (var result in results)
                {
                    await auditWriter.WriteAsync(
                        AuditActions.KnowledgeFileUploaded, Actor,
                        outcome: result.Outcome == UploadOutcome.Rejected ? AuditOutcome.Failure : AuditOutcome.Success,
                        applicationId: applicationId,
                        entityType: "KnowledgeFile", entityId: result.FileId?.ToString(), entityDisplayName: result.FileName,
                        failureReason: result.Outcome == UploadOutcome.Rejected ? result.Reason : null,
                        cancellationToken: cancellationToken);
                }

                return Results.Ok(results);
            }
            catch (Exception ex)
            {
                return MapKnowledgeException(ex);
            }
        }).DisableAntiforgery(); // ASP.NET Core auto-tags IFormFile-binding endpoints with antiforgery
                                  // metadata; the antiforgery middleware was removed along with CSRF/auth,
                                  // so this must be explicitly disabled or every upload 500s.

        group.MapPost("/{fileId:guid}/replace", async (
            Guid applicationId,
            Guid fileId,
            IFormFile file,
            KnowledgeFilesService service,
            IApplicationReadOnlyGuard readOnlyGuard,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var existing = await service.GetAsync(fileId, cancellationToken);
                if (existing.ApplicationId != applicationId)
                {
                    return Results.NotFound();
                }

                await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

                var candidate = new UploadCandidate(file.FileName, file.ContentType, file.OpenReadStream());
                var updated = await service.ReplaceAsync(fileId, candidate, cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.KnowledgeFileReplaced, Actor, applicationId: applicationId,
                    entityType: "KnowledgeFile", entityId: fileId.ToString(), entityDisplayName: updated.DisplayName,
                    cancellationToken: cancellationToken);
                return Results.Ok(updated);
            }
            catch (Exception ex)
            {
                return MapKnowledgeException(ex);
            }
        }).DisableAntiforgery();

        group.MapGet("/", async (
            Guid applicationId,
            string? search,
            UserFacingFileStatus? status,
            KnowledgeFilesService service,
            CancellationToken cancellationToken) =>
        {
            var list = await service.ListAsync(applicationId, search, status, cancellationToken);
            return Results.Ok(list);
        });

        group.MapGet("/{fileId:guid}/preview", async (
            Guid applicationId,
            Guid fileId,
            KnowledgeFilesService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var file = await service.GetAsync(fileId, cancellationToken);
                if (file.ApplicationId != applicationId)
                {
                    return Results.NotFound();
                }

                var (_, content) = await service.PreviewAsync(fileId, cancellationToken);
                return Results.Text(content, "text/plain");
            }
            catch (Exception ex)
            {
                return MapKnowledgeException(ex);
            }
        });

        group.MapPost("/{fileId:guid}/disable", (Guid applicationId, Guid fileId, KnowledgeFilesService service, IApplicationReadOnlyGuard guard, IAuditWriter auditWriter, CancellationToken ct) =>
            MutateAsync(applicationId, fileId, service, guard, f => service.DisableAsync(fileId, ct), auditWriter, AuditActions.KnowledgeFileDisabled, ct));

        group.MapPost("/{fileId:guid}/enable", (Guid applicationId, Guid fileId, KnowledgeFilesService service, IApplicationReadOnlyGuard guard, IAuditWriter auditWriter, CancellationToken ct) =>
            MutateAsync(applicationId, fileId, service, guard, f => service.EnableAsync(fileId, ct), auditWriter, AuditActions.KnowledgeFileEnabled, ct));

        group.MapPost("/{fileId:guid}/reprocess", (Guid applicationId, Guid fileId, KnowledgeFilesService service, IApplicationReadOnlyGuard guard, IAuditWriter auditWriter, CancellationToken ct) =>
            MutateAsync(applicationId, fileId, service, guard, f => service.ReprocessAsync(fileId, ct), auditWriter, AuditActions.KnowledgeFileRetryRequested, ct));

        group.MapDelete("/{fileId:guid}", async (
            Guid applicationId,
            Guid fileId,
            KnowledgeFilesService service,
            IApplicationReadOnlyGuard readOnlyGuard,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var file = await service.GetAsync(fileId, cancellationToken);
                if (file.ApplicationId != applicationId)
                {
                    return Results.NotFound();
                }

                await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);
                await service.DeleteAsync(fileId, cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.KnowledgeFileDeleted, Actor, applicationId: applicationId,
                    entityType: "KnowledgeFile", entityId: fileId.ToString(), entityDisplayName: file.DisplayName,
                    cancellationToken: cancellationToken);
                return Results.NoContent();
            }
            catch (Exception ex)
            {
                return MapKnowledgeException(ex);
            }
        });

        return endpoints;
    }

    private static async Task<IResult> MutateAsync(
        Guid applicationId,
        Guid fileId,
        KnowledgeFilesService service,
        IApplicationReadOnlyGuard readOnlyGuard,
        Func<KnowledgeFileResponse, Task<KnowledgeFileResponse>> action,
        IAuditWriter auditWriter,
        string auditAction,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = await service.GetAsync(fileId, cancellationToken);
            if (file.ApplicationId != applicationId)
            {
                return Results.NotFound();
            }

            await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);
            var updated = await action(file);
            await auditWriter.WriteAsync(
                auditAction, Actor, applicationId: applicationId,
                entityType: "KnowledgeFile", entityId: fileId.ToString(), entityDisplayName: updated.DisplayName,
                cancellationToken: cancellationToken);
            return Results.Ok(updated);
        }
        catch (Exception ex)
        {
            return MapKnowledgeException(ex);
        }
    }

    private static IResult MapKnowledgeException(Exception exception) => exception switch
    {
        KnowledgeFileNotFoundException => Results.NotFound(),
        ApplicationNotFoundException => Results.NotFound(),
        KnowledgeFileStateConflictException stateConflict => Results.Problem(
            title: "Knowledge file state conflict",
            detail: stateConflict.Message,
            statusCode: StatusCodes.Status409Conflict),
        FileValidationFailedException validationFailed => Results.Problem(
            title: "File failed validation",
            detail: validationFailed.Message,
            statusCode: StatusCodes.Status400BadRequest),
        ApplicationReadOnlyException readOnlyConflict => Results.Problem(
            title: "Application is read-only",
            detail: readOnlyConflict.Message,
            statusCode: StatusCodes.Status409Conflict),
        // The file's own record can outlive its backing blob (e.g. storage-side data loss,
        // an out-of-band bucket wipe) - surface this as a clean, categorized error instead of
        // an unhandled 500 (mirrors how DocumentProcessingOrchestrator already classifies this
        // as ErrorCategory.InternalFailure rather than letting it crash the worker).
        ObjectNotFoundException => Results.Problem(
            title: "Source content unavailable",
            detail: "The file's content could not be retrieved from storage. Try reprocessing the file.",
            statusCode: StatusCodes.Status404NotFound),
        _ => throw exception,
    };
}
