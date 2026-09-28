using EnterpriseKnowledgeAgent.Api.HealthChecks;
using EnterpriseKnowledgeAgent.Application.Dashboards;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Api;

/// <summary>TASK-OPS-03: a single dependency's health as reported by the readiness-tagged <see cref="Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck"/> registrations (module 31, ASDD §24).</summary>
public sealed record ComponentHealthResponse(string Name, string Status, string? Detail);

/// <summary>
/// TASK-OPS-03: <c>GET /api/operations/health</c> response (ASDD §24/module
/// 31 operations page). Deliberately excludes Azure OpenAI - deep AOAI
/// reachability checks are user-initiated/sparing via the existing
/// <c>GET /api/operations/aoai-health</c> endpoint, not part of this
/// routine aggregation.
/// </summary>
public sealed record OperationsHealthResponse(
    IReadOnlyList<ComponentHealthResponse> Components,
    IReadOnlyList<QueueDepthResponse> Queues,
    IReadOnlyList<WorkerHeartbeatResponse> Workers,
    DateTimeOffset CheckedAt);

/// <summary>
/// TASK-OPS-03: platform operations aggregation endpoint (module 31). Reuses
/// the same readiness-tagged <see cref="IHealthCheck"/> registrations backing
/// <c>/health/ready</c> (TASK-OPS-01) via <see cref="HealthCheckService"/>, so
/// there is exactly one place each dependency's health logic is implemented.
/// </summary>
public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/operations/health", async (
            HealthCheckService healthCheckService,
            IMessageQueue messageQueue,
            IWorkerHeartbeatRepository workerHeartbeatRepository,
            IOptions<SqsOptions> sqsOptions,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var report = await healthCheckService.CheckHealthAsync(
                check => check.Tags.Contains("ready"), cancellationToken);

            var components = new List<ComponentHealthResponse>
            {
                // Reaching this handler already proves the API process (and
                // its in-process SignalR hub, TASK-SIG-01) is responsive -
                // these two mirror that fact rather than a fabricated
                // separate probe.
                new("Api", nameof(HealthStatus.Healthy), null),
                new("SignalR", nameof(HealthStatus.Healthy), null),
            };
            components.AddRange(report.Entries.Select(entry =>
                new ComponentHealthResponse(ToDisplayName(entry.Key), entry.Value.Status.ToString(), entry.Value.Description)));

            string[] queueNames = [sqsOptions.Value.DocumentProcessingQueue, sqsOptions.Value.AgentExecutionQueue];
            var queues = new List<QueueDepthResponse>();
            foreach (var queueName in queueNames)
            {
                var depth = await messageQueue.GetApproximateDepthAsync(queueName, cancellationToken);
                queues.Add(new QueueDepthResponse(queueName, depth.ApproximateMessageCount, depth.DeadLetterQueueMessageCount));
            }

            var now = timeProvider.GetUtcNow();
            var workers = (await workerHeartbeatRepository.ListAllAsync(cancellationToken))
                .Select(w => new WorkerHeartbeatResponse(
                    w.WorkerId, w.WorkerKind, w.LastHeartbeatAt, w.IsStale(now)))
                .OrderBy(w => w.WorkerKind).ThenBy(w => w.WorkerId)
                .ToList();

            return Results.Ok(new OperationsHealthResponse(components, queues, workers, now));
        })
            .WithTags("Operations");

        return endpoints;
    }

    private static string ToDisplayName(string healthCheckName) => healthCheckName switch
    {
        "postgres" => "Postgres + pgvector",
        "object-storage" => "Object Storage (S3)",
        "message-queue" => "Message Queue (SQS)",
        "workers" => "Worker Heartbeats",
        _ => healthCheckName,
    };
}
