using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Knowledge;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Shared.Agents;

namespace EnterpriseKnowledgeAgent.Application.Dashboards;

/// <summary>
/// TASK-ADSH-01: orchestrates the single-application dashboard aggregation
/// (FR-APP-004, ASDD §21.2). Every count is computed fresh on each call - no
/// caching (decision D9). There is no login/authorization in this app, so
/// this performs no authorization itself, mirroring
/// <see cref="ApplicationsService"/>'s convention.
/// </summary>
public sealed class ApplicationDashboardService(
    IApplicationRepository applicationRepository,
    IKnowledgeFileRepository knowledgeFileRepository,
    IProcessingJobRepository processingJobRepository,
    IAgentRunRepository agentRunRepository,
    IAgentRepository agentRepository,
    IGeneratedOutputRepository generatedOutputRepository)
{
    public async Task<ApplicationDashboardResponse> GetAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var application = await applicationRepository.GetAsync(applicationId, cancellationToken)
            ?? throw new ApplicationNotFoundException(applicationId);

        var fileCounts = await knowledgeFileRepository.CountByStatusAsync(applicationId, cancellationToken);
        var jobCounts = await processingJobRepository.CountByStatusAsync(applicationId, cancellationToken);
        var runCounts = await agentRunRepository.CountByStatusAsync(applicationId, cancellationToken);
        var agents = await agentRepository.ListAsync(applicationId, cancellationToken);
        var outputs = await generatedOutputRepository.ListAsync(applicationId, cancellationToken: cancellationToken);

        var processingJobCounts = JobStatusCountsResponse.FromCounts(jobCounts);
        var agentRunCounts = JobStatusCountsResponse.FromCounts(runCounts);
        var activeAgents = agents.Count(a => a.Status == AgentStatus.Active);
        var totalAgents = agents.Count(a => a.Status != AgentStatus.Deleted);

        var attentionItems = new List<DashboardAttentionItem>();
        if (processingJobCounts.Failed > 0)
        {
            attentionItems.Add(new DashboardAttentionItem("ProcessingJobsFailed", "Processing jobs have failed", processingJobCounts.Failed));
        }
        if (agentRunCounts.Failed > 0)
        {
            attentionItems.Add(new DashboardAttentionItem("AgentRunsFailed", "Agent runs have failed", agentRunCounts.Failed));
        }

        return new ApplicationDashboardResponse(
            application.Id,
            application.Name,
            KnowledgeFileCountsResponse.FromCounts(fileCounts),
            processingJobCounts,
            agentRunCounts,
            activeAgents,
            totalAgents,
            outputs.Count,
            attentionItems);
    }
}
