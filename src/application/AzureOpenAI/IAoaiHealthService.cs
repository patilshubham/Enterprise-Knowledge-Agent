namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>Point-in-time reachability status for one configured deployment.</summary>
public sealed record DeploymentHealthStatus(string Deployment, DeploymentPurpose Purpose, bool Reachable, DateTimeOffset CheckedAt);

/// <summary>TASK-AOI-05: reports embedding/generation deployment reachability for the operations view (module 31), caching results briefly to avoid burning model quota on frequent polling.</summary>
public interface IAoaiHealthService
{
    Task<IReadOnlyList<DeploymentHealthStatus>> CheckAsync(CancellationToken cancellationToken = default);
}
