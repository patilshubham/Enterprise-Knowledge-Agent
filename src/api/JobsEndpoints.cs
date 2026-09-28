using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-JOB-06: manual retry endpoints for a Failed processing job or agent
/// run. Open to any caller - there is no login/authorization in this app.
/// </summary>
public static class JobsEndpoints
{
    private const string Actor = "local-user";

    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var applications = endpoints.MapGroup("/api/applications").WithTags("Jobs");

        applications.MapPost("/{applicationId:guid}/processing/{jobId:guid}/retry", async (
            Guid applicationId,
            Guid jobId,
            IProcessingJobRepository processingJobRepository,
            JobCreationService jobCreationService,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var job = await processingJobRepository.GetAsync(jobId, cancellationToken);
            if (job is null || job.ApplicationId != applicationId)
            {
                return Results.NotFound();
            }

            try
            {
                var retried = await jobCreationService.RetryProcessingJobAsync(jobId, cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.KnowledgeFileRetryRequested, Actor, applicationId: applicationId,
                    entityType: "ProcessingJob", entityId: jobId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Ok(new JobRetryResponse(retried.Id, retried.Status.ToString(), retried.AttemptNumber));
            }
            catch (JobStateConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        applications.MapPost("/{applicationId:guid}/runs/{runId:guid}/retry", async (
            Guid applicationId,
            Guid runId,
            IAgentRunRepository agentRunRepository,
            JobCreationService jobCreationService,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var run = await agentRunRepository.GetAsync(runId, cancellationToken);
            if (run is null || run.ApplicationId != applicationId)
            {
                return Results.NotFound();
            }

            try
            {
                var retried = await jobCreationService.RetryAgentRunAsync(runId, cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.AgentRunRetryRequested, Actor, applicationId: applicationId,
                    entityType: "AgentRun", entityId: runId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Ok(new JobRetryResponse(retried.Id, retried.Status.ToString(), retried.AttemptNumber));
            }
            catch (JobStateConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        return endpoints;
    }
}

/// <summary>TASK-JOB-06: minimal retry-result payload.</summary>
public sealed record JobRetryResponse(Guid Id, string Status, int AttemptNumber);
