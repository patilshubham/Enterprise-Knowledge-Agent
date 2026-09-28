using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Dashboards;
using EnterpriseKnowledgeAgent.Domain.Applications;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-PDSH-01/TASK-ADSH-01: platform-wide and per-application dashboard
/// aggregation endpoints (FR-DSH-001/FR-APP-004). Open to any caller - there
/// is no login/authorization in this app. Both are read-only GETs. The
/// queue names passed to <see cref="PlatformDashboardService"/> come from
/// <see cref="SqsOptions"/> (Infrastructure-layer config) here in the Api
/// layer, since the Application-layer service must not depend on
/// Infrastructure directly.
/// </summary>
public static class DashboardsEndpoints
{
    public static IEndpointRouteBuilder MapDashboardsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/platform/dashboard", async (
            PlatformDashboardService service,
            IOptions<SqsOptions> sqsOptions,
            CancellationToken cancellationToken) =>
        {
            string[] queueNames = [sqsOptions.Value.DocumentProcessingQueue, sqsOptions.Value.AgentExecutionQueue];
            var dashboard = await service.GetAsync(queueNames, cancellationToken);
            return Results.Ok(dashboard);
        })
            .WithTags("Dashboards");

        endpoints.MapGet("/api/applications/{applicationId:guid}/dashboard", async (
            Guid applicationId,
            ApplicationsService applicationsService,
            ApplicationDashboardService dashboardService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                await applicationsService.GetAsync(applicationId, cancellationToken);

                var dashboard = await dashboardService.GetAsync(applicationId, cancellationToken);
                return Results.Ok(dashboard);
            }
            catch (ApplicationNotFoundException)
            {
                return Results.NotFound();
            }
        })
            .WithTags("Dashboards");

        return endpoints;
    }
}