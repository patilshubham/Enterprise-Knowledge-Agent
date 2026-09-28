using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Audit;
using EnterpriseKnowledgeAgent.Domain.Agents;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-AGT-02: agent CRUD + enable/disable/delete endpoints. Open to any
/// caller - there is no login/authorization in this app. Mutations still
/// require an Active (non-archived/deleted) application
/// (<see cref="IApplicationReadOnlyGuard"/>, TASK-APP-04).
/// </summary>
public static class AgentsEndpoints
{
    private const string Actor = "local-user";

    public static IEndpointRouteBuilder MapAgentsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/applications/{applicationId:guid}/agents").WithTags("Agents");

        group.MapPost("/", async (
            Guid applicationId,
            CreateAgentRequest request,
            AgentsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var created = await service.CreateAsync(applicationId, request, Actor, cancellationToken);
                logger.Created(applicationId, created.Id, created.Name, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.AgentCreated, Actor, applicationId: applicationId,
                    entityType: "Agent", entityId: created.Id.ToString(), entityDisplayName: created.Name,
                    cancellationToken: cancellationToken);
                return Results.Created($"/api/applications/{applicationId}/agents/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return MapAgentException(ex);
            }
        });

        group.MapGet("/", async (
            Guid applicationId,
            AgentsService service,
            CancellationToken cancellationToken) =>
        {
            var list = await service.ListAsync(applicationId, cancellationToken);
            return Results.Ok(list);
        });

        group.MapGet("/{agentId:guid}", async (
            Guid applicationId,
            Guid agentId,
            AgentsService service,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var agent = await service.GetAsync(agentId, cancellationToken);
                if (agent.ApplicationId != applicationId)
                {
                    return Results.NotFound();
                }

                return Results.Ok(agent);
            }
            catch (Exception ex)
            {
                return MapAgentException(ex);
            }
        });

        group.MapPut("/{agentId:guid}", async (
            Guid applicationId,
            Guid agentId,
            UpdateAgentRequest request,
            AgentsService service,
            ILogger<Program> logger,
            IAuditWriter auditWriter,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updated = await service.UpdateAsync(applicationId, agentId, request, Actor, cancellationToken);
                logger.Updated(applicationId, agentId, Actor);
                await auditWriter.WriteAsync(
                    AuditActions.AgentUpdated, Actor, applicationId: applicationId,
                    entityType: "Agent", entityId: agentId.ToString(), entityDisplayName: updated.Name,
                    cancellationToken: cancellationToken);
                return Results.Ok(updated);
            }
            catch (Exception ex)
            {
                return MapAgentException(ex);
            }
        });

        group.MapPost("/{agentId:guid}/enable", (Guid applicationId, Guid agentId, AgentsService service, ILogger<Program> logger, IAuditWriter auditWriter, CancellationToken ct) =>
            MutateAsync(applicationId, agentId, service, (id, actor, c) => service.EnableAsync(applicationId, id, actor, c), logger.Enabled, auditWriter, AuditActions.AgentEnabled, ct));

        group.MapPost("/{agentId:guid}/disable", (Guid applicationId, Guid agentId, AgentsService service, ILogger<Program> logger, IAuditWriter auditWriter, CancellationToken ct) =>
            MutateAsync(applicationId, agentId, service, (id, actor, c) => service.DisableAsync(applicationId, id, actor, c), logger.Disabled, auditWriter, AuditActions.AgentDisabled, ct));

        group.MapDelete("/{agentId:guid}", (Guid applicationId, Guid agentId, AgentsService service, ILogger<Program> logger, IAuditWriter auditWriter, CancellationToken ct) =>
            MutateAsync(applicationId, agentId, service, (id, actor, c) => service.DeleteAsync(applicationId, id, actor, c), logger.Deleted, auditWriter, AuditActions.AgentDeleted, ct));

        return endpoints;
    }

    private static async Task<IResult> MutateAsync(
        Guid applicationId,
        Guid agentId,
        AgentsService service,
        Func<Guid, string, CancellationToken, Task<AgentResponse>> action,
        Action<Guid, Guid, string> logAudit,
        IAuditWriter auditWriter,
        string auditAction,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await action(agentId, Actor, cancellationToken);
            logAudit(applicationId, agentId, Actor);
            await auditWriter.WriteAsync(
                auditAction, Actor, applicationId: applicationId,
                entityType: "Agent", entityId: agentId.ToString(), entityDisplayName: updated.Name,
                cancellationToken: cancellationToken);
            return Results.Ok(updated);
        }
        catch (Exception ex)
        {
            return MapAgentException(ex);
        }
    }

    private static IResult MapAgentException(Exception exception) => exception switch
    {
        AgentNotFoundException => Results.NotFound(),
        ApplicationNotFoundException => Results.NotFound(),
        AgentValidationException validation => Results.Problem(
            title: "Invalid agent configuration",
            detail: validation.Message,
            statusCode: StatusCodes.Status400BadRequest),
        AgentStateConflictException stateConflict => Results.Problem(
            title: "Agent state conflict",
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
