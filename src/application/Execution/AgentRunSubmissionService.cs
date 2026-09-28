using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Agents;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Domain.Agents;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Observability;

namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>
/// TASK-RUN-01: validates and snapshots an agent run submission, then hands
/// off to <see cref="JobCreationService"/> for the atomic run+outbox
/// persistence (Decision D1). Repeated submissions with the same
/// (agentId, idempotencyKey) pair return the original run rather than
/// creating a duplicate - enforced here via a lookup before create, backed
/// by a unique database index as a safety net (<c>AgentRunConfiguration</c>).
/// </summary>
public sealed class AgentRunSubmissionService(
    IAgentRepository agentRepository,
    IAgentRunRepository agentRunRepository,
    AgentParameterValidator parameterValidator,
    JobCreationService jobCreationService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentRun> SubmitAsync(
        Guid applicationId,
        Guid agentId,
        string requestedBy,
        SubmitAgentRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.UserRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);

        Agent agent;
        // TASK-OBS-03 (ASDD §23.3): "Load agent snapshot" - loading the
        // agent's current configuration to snapshot into the new run.
        using (var activity = AppTracing.Source.StartActivity("Load agent snapshot"))
        {
            activity?.SetTag("application.id", applicationId);
            activity?.SetTag("agent.id", agentId);

            agent = await agentRepository.GetAsync(agentId, cancellationToken) ?? throw new AgentNotFoundException(agentId);
            if (agent.ApplicationId != applicationId)
            {
                throw new AgentNotFoundException(agentId);
            }
        }

        // Idempotency check first: a repeated submission of an already-queued
        // run must succeed even if the agent has since been disabled/edited -
        // it just returns the run created by the original submission.
        var existing = await agentRunRepository.GetByIdempotencyKeyAsync(agentId, request.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (agent.Status != AgentStatus.Active)
        {
            throw new AgentValidationException($"Agent '{agentId}' must be Active to run (current status: {agent.Status}).");
        }

        parameterValidator.ValidateGenerationParameters(agent.GenerationDeployment, agent.MaximumOutputTokens, agent.TopP);

        // Requested and effective parameters start identical - TASK-GEN-01
        // overwrites the effective snapshot if generation adjusts anything.
        var parametersJson = JsonSerializer.Serialize(
            new AgentRunParametersSnapshot(agent.Temperature, agent.MaximumOutputTokens, agent.TopP), JsonOptions);
        var retrievalSettingsJson = JsonSerializer.Serialize(
            new AgentRunRetrievalSettingsSnapshot(
                agent.RetrievalMode.ToString(),
                agent.RetrievalCount,
                agent.MinimumSimilarity,
                agent.MaximumEvidenceTokens,
                agent.IncludeNeighbors,
                agent.StrictGrounding,
                agent.CitationsRequired),
            JsonOptions);
        var knowledgeScopeSnapshotJson = agent.KnowledgeScopeType == KnowledgeScopeType.Selected
            ? JsonSerializer.Serialize(agent.SelectedFileIds, JsonOptions)
            : null;

        var createRequest = new CreateAgentRunRequest(
            applicationId,
            agentId,
            requestedBy,
            request.UserRequest.Trim(),
            request.IdempotencyKey,
            string.IsNullOrWhiteSpace(request.OutputTitle) ? null : request.OutputTitle.Trim(),
            agent.Name,
            agent.Prompt,
            knowledgeScopeSnapshotJson,
            agent.GenerationDeployment,
            parametersJson,
            parametersJson,
            retrievalSettingsJson);

        return await jobCreationService.CreateAgentRunAsync(createRequest, cancellationToken);
    }
}

internal sealed record AgentRunRetrievalSettingsSnapshot(
    string RetrievalMode,
    int RetrievalCount,
    double MinimumSimilarity,
    int MaximumEvidenceTokens,
    bool IncludeNeighbors,
    bool StrictGrounding,
    bool CitationsRequired);
