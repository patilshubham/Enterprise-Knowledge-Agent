namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>TASK-JOB-02: inputs for <see cref="JobCreationService.CreateAgentRunAsync"/> - the snapshot/parameter values are supplied by the caller (module 19, not yet built); this service only owns atomic job+outbox persistence.</summary>
public sealed record CreateAgentRunRequest(
    Guid ApplicationId,
    Guid AgentId,
    string RequestedBy,
    string UserRequest,
    string IdempotencyKey,
    string? OutputTitle,
    string AgentNameSnapshot,
    string AgentPromptSnapshot,
    string? KnowledgeScopeSnapshotJson,
    string? DeploymentSnapshot,
    string? RequestedParametersJson,
    string? EffectiveParametersJson,
    string? RetrievalSettingsJson);
