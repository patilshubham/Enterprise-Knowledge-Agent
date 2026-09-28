using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Domain.Operations;

namespace EnterpriseKnowledgeAgent.Application.Dashboards;

/// <summary>
/// TASK-PDSH-01: orchestrates the cross-application platform dashboard
/// aggregation (FR-DSH-001, ASDD §21.1). Every count is computed fresh on
/// each call - no caching (decision D9), relying on the indexes the
/// individual repositories' count queries already use. Restricted to
/// platform roles by the caller (the <c>CanViewPlatformProcessing</c>
/// policy on the endpoint) - this service performs no authorization itself.
/// </summary>
public sealed class PlatformDashboardService(
    IApplicationRepository applicationRepository,
    IKnowledgeFileRepository knowledgeFileRepository,
    IProcessingJobRepository processingJobRepository,
    IAgentRunRepository agentRunRepository,
    IWorkerHeartbeatRepository workerHeartbeatRepository,
    IMessageQueue messageQueue,
    IAoaiHealthService aoaiHealthService,
    TimeProvider timeProvider)
{
    public async Task<PlatformDashboardResponse> GetAsync(IReadOnlyCollection<string> queueNames, CancellationToken cancellationToken = default)
    {
        var applicationCounts = await applicationRepository.CountByStatusAsync(cancellationToken);
        var fileCounts = await knowledgeFileRepository.CountByStatusAsync(applicationId: null, cancellationToken);
        var jobCounts = await processingJobRepository.CountByStatusAsync(applicationId: null, cancellationToken);
        var runCounts = await agentRunRepository.CountByStatusAsync(applicationId: null, cancellationToken);
        var workers = await workerHeartbeatRepository.ListAllAsync(cancellationToken);
        var deployments = await aoaiHealthService.CheckAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        var workerResponses = workers
            .Select(w => new WorkerHeartbeatResponse(w.WorkerId, w.WorkerKind, w.LastHeartbeatAt, w.IsStale(now)))
            .OrderBy(w => w.WorkerKind).ThenBy(w => w.WorkerId)
            .ToList();

        var queues = new List<QueueDepthResponse>();
        foreach (var queueName in queueNames)
        {
            var depth = await messageQueue.GetApproximateDepthAsync(queueName, cancellationToken);
            queues.Add(new QueueDepthResponse(queueName, depth.ApproximateMessageCount, depth.DeadLetterQueueMessageCount));
        }

        var aoaiAvailable = deployments.Count == 0 || deployments.All(d => d.Reachable);

        var processingJobCounts = JobStatusCountsResponse.FromCounts(jobCounts);
        var agentRunCounts = JobStatusCountsResponse.FromCounts(runCounts);
        var staleWorkerCount = workerResponses.Count(w => w.IsStale);
        var deadLetterTotal = queues.Sum(q => q.DeadLetterQueueMessageCount);

        var attentionItems = new List<DashboardAttentionItem>();
        if (processingJobCounts.Failed > 0)
        {
            attentionItems.Add(new DashboardAttentionItem("ProcessingJobsFailed", "Processing jobs have failed", processingJobCounts.Failed));
        }
        if (agentRunCounts.Failed > 0)
        {
            attentionItems.Add(new DashboardAttentionItem("AgentRunsFailed", "Agent runs have failed", agentRunCounts.Failed));
        }
        if (staleWorkerCount > 0)
        {
            attentionItems.Add(new DashboardAttentionItem("WorkersStale", "Workers have missed heartbeats", staleWorkerCount));
        }
        if (!aoaiAvailable)
        {
            attentionItems.Add(new DashboardAttentionItem("AoaiUnavailable", "An Azure OpenAI deployment is unreachable", deployments.Count(d => !d.Reachable)));
        }
        if (deadLetterTotal > 0)
        {
            attentionItems.Add(new DashboardAttentionItem("DeadLetterMessages", "Messages are stuck in a dead-letter queue", (int)Math.Min(deadLetterTotal, int.MaxValue)));
        }

        return new PlatformDashboardResponse(
            ApplicationCountsResponse.FromCounts(applicationCounts),
            KnowledgeFileCountsResponse.FromCounts(fileCounts),
            processingJobCounts,
            agentRunCounts,
            workerResponses,
            aoaiAvailable,
            queues,
            attentionItems);
    }
}
