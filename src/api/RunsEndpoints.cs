using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-RUN-01: agent run submission. Open to any caller - there is no
/// login/authorization in this app. See <see cref="JobsEndpoints"/> for the
/// run retry endpoint (TASK-JOB-06) and <see cref="AgentRunSubmissionService"/>
/// for the snapshot/idempotency logic.
/// </summary>
public static class RunsEndpoints
{
    private const string Actor = "local-user";

    public static IEndpointRouteBuilder MapRunsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications/{applicationId:guid}/agents/{agentId:guid}/runs")
            .WithTags("Runs");

        group.MapPost("/", async (
            Guid applicationId,
            Guid agentId,
            SubmitAgentRunRequest request,
            AgentRunSubmissionService service,
            IApplicationRepository applicationRepository,
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
                var run = await service.SubmitAsync(applicationId, agentId, Actor, request, cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.AgentRunSubmitted, Actor, applicationId: applicationId,
                    entityType: "AgentRun", entityId: run.Id.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Accepted(
                    $"/api/applications/{applicationId}/agents/{agentId}/runs/{run.Id}",
                    new AgentRunSubmissionResponse(run.Id));
            }
            catch (Exception ex)
            {
                return MapRunException(ex);
            }
        });

        group.MapPost("/{runId:guid}/cancel", async (
            Guid applicationId,
            Guid agentId,
            Guid runId,
            IAgentRunRepository agentRunRepository,
            TimeProvider timeProvider,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            var run = await agentRunRepository.GetAsync(runId, cancellationToken);
            if (run is null || run.ApplicationId != applicationId || run.AgentId != agentId)
            {
                return Results.NotFound();
            }

            try
            {
                run.RequestCancellation(timeProvider.GetUtcNow());
                await agentRunRepository.SaveChangesAsync(cancellationToken);
                await auditWriter.WriteAsync(
                    AuditActions.AgentRunCancelRequested, Actor, applicationId: applicationId,
                    entityType: "AgentRun", entityId: runId.ToString(),
                    cancellationToken: cancellationToken);
                return Results.Accepted();
            }
            catch (JobStateConflictException ex)
            {
                return Results.Conflict(new { error = ex.Message });
            }
        });

        // TASK-RDSH-01/02: list/detail for the agent-run dashboard - a
        // separate route group (not nested under an agentId, unlike
        // submission/cancel above) since browsing runs is always scoped to
        // the application, filtering by agent optionally rather than
        // requiring it.
        var runsGroup = endpoints.MapGroup("/api/applications/{applicationId:guid}/runs")
            .WithTags("Runs");

        runsGroup.MapGet("/", async (
            Guid applicationId,
            JobStatus? status,
            Guid? agentId,
            string? createdBy,
            DateTimeOffset? from,
            DateTimeOffset? to,
            IApplicationRepository applicationRepository,
            AgentRunsQueryService service,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            var runs = await service.ListAsync(applicationId, status, agentId, createdBy, from, to, cancellationToken);
            return Results.Ok(runs);
        });

        runsGroup.MapGet("/{runId:guid}", async (
            Guid applicationId,
            Guid runId,
            IApplicationRepository applicationRepository,
            AgentRunsQueryService service,
            CancellationToken cancellationToken) =>
        {
            var application = await applicationRepository.GetAsync(applicationId, cancellationToken);
            if (application is null)
            {
                return Results.NotFound();
            }

            try
            {
                var run = await service.GetDetailAsync(applicationId, runId, cancellationToken);
                return Results.Ok(run);
            }
            catch (AgentRunNotFoundException)
            {
                return Results.NotFound();
            }
        });

        return endpoints;
    }

    private static IResult MapRunException(Exception exception) => exception switch
    {
        AgentNotFoundException => Results.NotFound(),
        AgentValidationException validation => Results.Problem(
            title: "Invalid agent run request",
            detail: validation.Message,
            statusCode: StatusCodes.Status400BadRequest),
        JobStateConflictException stateConflict => Results.Problem(
            title: "Agent run state conflict",
            detail: stateConflict.Message,
            statusCode: StatusCodes.Status409Conflict),
        ArgumentException argumentException => Results.Problem(
            title: "Invalid request",
            detail: argumentException.Message,
            statusCode: StatusCodes.Status400BadRequest),
        _ => throw exception,
    };
}

/// <summary>TASK-RUN-01: minimal submission-result payload (<c>202 {runId}</c> per the task's API contract).</summary>
public sealed record AgentRunSubmissionResponse(Guid RunId);
