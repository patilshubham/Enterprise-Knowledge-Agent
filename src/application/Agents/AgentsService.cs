using EnterpriseKnowledgeAgent.Application.Applications;
using EnterpriseKnowledgeAgent.Shared.Agents;
using EnterpriseKnowledgeAgent.Shared.Retrieval;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;

namespace EnterpriseKnowledgeAgent.Application.Agents;

/// <summary>
/// TASK-AGT-02: the application-layer orchestration behind agent CRUD +
/// lifecycle endpoints - mirrors <see cref="ApplicationsService"/>'s
/// convention (Api layer only maps HTTP to these calls and maps thrown
/// exceptions to Problem Details).
/// </summary>
public sealed class AgentsService(
    IAgentRepository repository,
    IApplicationReadOnlyGuard readOnlyGuard,
    AgentParameterValidator parameterValidator,
    TimeProvider timeProvider)
{
    public async Task<AgentResponse> CreateAsync(
        Guid applicationId, CreateAgentRequest request, string createdBy, CancellationToken cancellationToken = default)
    {
        await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

        var knowledgeScopeType = ParseKnowledgeScopeType(request.KnowledgeScopeType);
        var retrievalMode = ParseRetrievalMode(request.RetrievalMode);
        parameterValidator.ValidateGenerationParameters(request.GenerationDeployment, request.MaximumOutputTokens, request.TopP);

        var now = timeProvider.GetUtcNow();
        var agent = AgentEntity.Create(
            applicationId,
            request.Name,
            request.Description,
            request.Prompt,
            knowledgeScopeType,
            request.SelectedFileIds,
            request.GenerationDeployment,
            request.Temperature,
            request.MaximumOutputTokens,
            request.TopP,
            retrievalMode,
            request.RetrievalCount,
            request.MinimumSimilarity,
            request.MaximumEvidenceTokens,
            request.IncludeNeighbors,
            request.StrictGrounding,
            request.CitationsRequired,
            createdBy,
            now);

        await repository.AddAsync(agent, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(agent, includePrompt: true);
    }

    /// <summary>There is no login/authorization in this app - the prompt is always included.</summary>
    public async Task<IReadOnlyList<AgentResponse>> ListAsync(Guid applicationId, CancellationToken cancellationToken = default)
    {
        var agents = await repository.ListAsync(applicationId, cancellationToken);
        return [.. agents.Select(a => ToResponse(a, includePrompt: true))];
    }

    public async Task<AgentResponse> GetAsync(Guid agentId, CancellationToken cancellationToken = default)
    {
        var agent = await repository.GetAsync(agentId, cancellationToken) ?? throw new AgentNotFoundException(agentId);
        return ToResponse(agent, includePrompt: true);
    }

    public async Task<AgentResponse> UpdateAsync(
        Guid applicationId, Guid agentId, UpdateAgentRequest request, string updatedBy, CancellationToken cancellationToken = default)
    {
        await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

        var agent = await repository.GetAsync(agentId, cancellationToken) ?? throw new AgentNotFoundException(agentId);

        var knowledgeScopeType = ParseKnowledgeScopeType(request.KnowledgeScopeType);
        var retrievalMode = ParseRetrievalMode(request.RetrievalMode);
        parameterValidator.ValidateGenerationParameters(request.GenerationDeployment, request.MaximumOutputTokens, request.TopP);

        agent.Update(
            request.Name,
            request.Description,
            request.Prompt,
            knowledgeScopeType,
            request.SelectedFileIds,
            request.GenerationDeployment,
            request.Temperature,
            request.MaximumOutputTokens,
            request.TopP,
            retrievalMode,
            request.RetrievalCount,
            request.MinimumSimilarity,
            request.MaximumEvidenceTokens,
            request.IncludeNeighbors,
            request.StrictGrounding,
            request.CitationsRequired,
            updatedBy,
            timeProvider.GetUtcNow());

        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(agent, includePrompt: true);
    }

    public async Task<AgentResponse> EnableAsync(Guid applicationId, Guid agentId, string updatedBy, CancellationToken cancellationToken = default)
    {
        await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

        var agent = await repository.GetAsync(agentId, cancellationToken) ?? throw new AgentNotFoundException(agentId);
        agent.Enable(updatedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(agent, includePrompt: true);
    }

    public async Task<AgentResponse> DisableAsync(Guid applicationId, Guid agentId, string updatedBy, CancellationToken cancellationToken = default)
    {
        await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

        var agent = await repository.GetAsync(agentId, cancellationToken) ?? throw new AgentNotFoundException(agentId);
        agent.Disable(updatedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(agent, includePrompt: true);
    }

    /// <summary>Idempotent (mirrors <see cref="ApplicationsService.DeleteAsync"/>): deleting an already-deleted agent succeeds without error.</summary>
    public async Task<AgentResponse> DeleteAsync(Guid applicationId, Guid agentId, string updatedBy, CancellationToken cancellationToken = default)
    {
        await readOnlyGuard.EnsureMutableAsync(applicationId, cancellationToken);

        var agent = await repository.GetAsync(agentId, cancellationToken) ?? throw new AgentNotFoundException(agentId);
        agent.Delete(updatedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);

        return ToResponse(agent, includePrompt: true);
    }

    private static KnowledgeScopeType ParseKnowledgeScopeType(string value) =>
        Enum.TryParse<KnowledgeScopeType>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new AgentValidationException($"'{value}' is not a valid knowledge scope type.");

    private static RetrievalMode ParseRetrievalMode(string value) =>
        Enum.TryParse<RetrievalMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : throw new AgentValidationException($"'{value}' is not a valid retrieval mode.");

    private static AgentResponse ToResponse(AgentEntity agent, bool includePrompt) => new(
        agent.Id,
        agent.ApplicationId,
        agent.Name,
        agent.Description,
        includePrompt ? agent.Prompt : null,
        agent.Status.ToString(),
        agent.KnowledgeScopeType.ToString(),
        agent.SelectedFileIds,
        agent.GenerationDeployment,
        agent.Temperature,
        agent.MaximumOutputTokens,
        agent.TopP,
        agent.RetrievalMode.ToString(),
        agent.RetrievalCount,
        agent.MinimumSimilarity,
        agent.MaximumEvidenceTokens,
        agent.IncludeNeighbors,
        agent.StrictGrounding,
        agent.CitationsRequired,
        agent.CreatedBy,
        agent.CreatedAt,
        agent.UpdatedBy,
        agent.UpdatedAt,
        agent.ConcurrencyToken);
}
