using EnterpriseKnowledgeAgent.Application.Citations;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Outputs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>
/// TASK-RDSH-01/02: the application-layer orchestration behind the
/// per-application agent-run dashboard's list/detail endpoints - mirrors
/// <c>OutputsService</c>'s convention (Api layer only maps HTTP to these
/// calls and maps thrown exceptions to Problem Details).
/// </summary>
public sealed class AgentRunsQueryService(
    IAgentRunRepository agentRunRepository,
    IRunEvidenceRepository runEvidenceRepository,
    IGeneratedOutputRepository generatedOutputRepository)
{
    public async Task<IReadOnlyList<AgentRunSummaryResponse>> ListAsync(
        Guid applicationId,
        JobStatus? status,
        Guid? agentId,
        string? createdBy,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var runs = await agentRunRepository.ListAsync(applicationId, status, agentId, createdBy, from, to, cancellationToken);
        return runs.Select(AgentRunSummaryResponse.FromEntity).ToList();
    }

    /// <summary>404-masks cross-application access - the run must belong to <paramref name="applicationId"/>.</summary>
    public async Task<AgentRunDetailResponse> GetDetailAsync(Guid applicationId, Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await agentRunRepository.GetAsync(runId, cancellationToken);
        if (run is null || run.ApplicationId != applicationId)
        {
            throw new AgentRunNotFoundException(runId);
        }

        var evidence = await runEvidenceRepository.ListByRunAsync(runId, cancellationToken);
        var output = await generatedOutputRepository.GetByAgentRunAsync(runId, cancellationToken);

        var evidenceResponses = evidence
            .Select(e => new RunEvidenceResponse(
                e.Id, e.SourceId, e.FileNameSnapshot, e.HeadingPathSnapshot, e.PassageSnapshot,
                e.CharacterStartSnapshot, e.CharacterEndSnapshot, e.ProcessedAtSnapshot))
            .ToList();

        return AgentRunDetailResponse.FromEntity(run, output?.Id, evidenceResponses);
    }
}
