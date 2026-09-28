using EnterpriseKnowledgeAgent.Application.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.HealthChecks;

/// <summary>TASK-OPS-01 (ASDD §24 readiness): wraps <see cref="IObjectStorage.CheckHealthAsync"/> (S3/LocalStack reachability, TASK-S3-04).</summary>
public sealed class ObjectStorageHealthCheck(IObjectStorage objectStorage) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var health = await objectStorage.CheckHealthAsync(cancellationToken);
        return health.IsAvailable
            ? HealthCheckResult.Healthy("Object storage reachable.")
            : HealthCheckResult.Unhealthy(health.Detail ?? "Object storage unreachable.");
    }
}
