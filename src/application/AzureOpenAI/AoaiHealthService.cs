namespace EnterpriseKnowledgeAgent.Application.AzureOpenAI;

/// <summary>
/// TASK-AOI-05: caches the last reachability check for
/// <see cref="TimeToLive"/> so the operations page (module 31) can poll
/// freely without each request burning Azure OpenAI quota (ASDD §24 - "deep
/// checks should be sparing").
/// </summary>
public sealed class AoaiHealthService(
    IEmbeddingService embeddingService,
    ITextGenerationService textGenerationService,
    IDeploymentRegistry deploymentRegistry,
    TimeProvider timeProvider) : IAoaiHealthService, IDisposable
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<DeploymentHealthStatus>? _cached;
    private DateTimeOffset _cachedAt;

    public void Dispose() => _lock.Dispose();

    public async Task<IReadOnlyList<DeploymentHealthStatus>> CheckAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = timeProvider.GetUtcNow();
            if (_cached is not null && now - _cachedAt < TimeToLive)
            {
                return _cached;
            }

            var results = new List<DeploymentHealthStatus>();
            foreach (var deployment in deploymentRegistry.ListDeployments())
            {
                var reachable = deployment.Purpose == DeploymentPurpose.Embedding
                    ? await embeddingService.CheckHealthAsync(cancellationToken).ConfigureAwait(false)
                    : await textGenerationService.CheckHealthAsync(cancellationToken).ConfigureAwait(false);
                results.Add(new DeploymentHealthStatus(deployment.Name, deployment.Purpose, reachable, now));
            }

            _cached = results;
            _cachedAt = now;
            return results;
        }
        finally
        {
            _lock.Release();
        }
    }
}
