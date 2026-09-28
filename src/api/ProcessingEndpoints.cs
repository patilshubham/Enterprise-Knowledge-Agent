using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-PRDSH-01/02: platform-wide and per-application processing-job
/// list/detail endpoints (FR-DSH-003/FR-PRC-*). Open to any caller - there is
/// no login/authorization in this app. See <see cref="JobsEndpoints"/> for
/// the manual-retry POST.
/// </summary>
public static class ProcessingEndpoints
{
    public static IEndpointRouteBuilder MapProcessingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/platform/processing", async (
            JobStatus? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ProcessingJobsQueryService service,
            CancellationToken cancellationToken) =>
        {
            var jobs = await service.ListAsync(applicationId: null, status, from, to, cancellationToken);
            return Results.Ok(jobs);
        })
            .WithTags("Processing");

        endpoints.MapGet("/api/platform/processing/{jobId:guid}", async (
            Guid jobId,
            ProcessingJobsQueryService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var job = await service.GetDetailAsync(applicationId: null, jobId, cancellationToken);
                return Results.Ok(job);
            }
            catch (ProcessingJobNotFoundException)
            {
                return Results.NotFound();
            }
        })
            .WithTags("Processing");

        var applications = endpoints.MapGroup("/api/applications/{applicationId:guid}/processing")
            .WithTags("Processing");

        applications.MapGet("/", async (
            Guid applicationId,
            JobStatus? status,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ApplicationsService applicationsService,
            ProcessingJobsQueryService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await applicationsService.GetAsync(applicationId, cancellationToken);

                var jobs = await service.ListAsync(applicationId, status, from, to, cancellationToken);
                return Results.Ok(jobs);
            }
            catch (ApplicationNotFoundException)
            {
                return Results.NotFound();
            }
        });

        applications.MapGet("/{jobId:guid}", async (
            Guid applicationId,
            Guid jobId,
            ApplicationsService applicationsService,
            ProcessingJobsQueryService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await applicationsService.GetAsync(applicationId, cancellationToken);

                var job = await service.GetDetailAsync(applicationId, jobId, cancellationToken);
                return Results.Ok(job);
            }
            catch (ApplicationNotFoundException)
            {
                return Results.NotFound();
            }
            catch (ProcessingJobNotFoundException)
            {
                return Results.NotFound();
            }
        });

        return endpoints;
    }
}
