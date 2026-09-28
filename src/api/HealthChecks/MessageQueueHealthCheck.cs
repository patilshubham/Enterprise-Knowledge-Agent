using EnterpriseKnowledgeAgent.Application.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.HealthChecks;

/// <summary>TASK-OPS-01 (ASDD §24 readiness): wraps <see cref="IMessageQueue.CheckHealthAsync"/> (SQS/LocalStack reachability, TASK-SQS-04).</summary>
public sealed class MessageQueueHealthCheck(IMessageQueue messageQueue) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var health = await messageQueue.CheckHealthAsync(cancellationToken);
        return health.IsAvailable
            ? HealthCheckResult.Healthy("Message queue reachable.")
            : HealthCheckResult.Unhealthy(health.Detail ?? "Message queue unreachable.");
    }
}
