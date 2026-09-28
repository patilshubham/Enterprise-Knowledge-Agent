using EnterpriseKnowledgeAgent.Application.AzureOpenAI;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>
/// TASK-AOI-01/TASK-AOI-05: read-only Azure OpenAI configuration/health
/// endpoints. Open to any caller - there is no login/authorization in this
/// app. <c>/api/deployments</c> is used by the agent editor's parameter
/// validation/token display (module 17) and never includes secrets.
/// <c>/api/operations/aoai-health</c> (module 31's operations page) is
/// short-lived-cached server-side (<see cref="AoaiHealthService"/>) to avoid
/// burning model quota on frequent polling.
/// </summary>
public static class AzureOpenAiEndpoints
{
    public static IEndpointRouteBuilder MapAzureOpenAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/deployments", (IDeploymentRegistry registry) =>
                Results.Ok(registry.ListDeployments()))
            .WithTags("AzureOpenAI");

        endpoints.MapGet("/api/operations/aoai-health", async (IAoaiHealthService healthService, CancellationToken cancellationToken) =>
                Results.Ok(await healthService.CheckAsync(cancellationToken)))
            .WithTags("AzureOpenAI");

        return endpoints;
    }
}
