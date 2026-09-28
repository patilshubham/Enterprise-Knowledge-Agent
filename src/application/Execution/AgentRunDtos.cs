using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>TASK-RDSH-01: one row of the agent-run dashboard listing (always application-scoped). <see cref="AgentName"/> comes from the run's own immutable <c>AgentNameSnapshot</c> - no join needed, and correct even if the agent is later renamed/deleted.</summary>
public sealed record AgentRunSummaryResponse(
    Guid Id,
    Guid ApplicationId,
    Guid AgentId,
    string AgentName,
    string RequestedBy,
    string Status,
    string Stage,
    int Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? FailureCategory,
    string? FailureMessage,
    bool RetryEligible)
{
    public static AgentRunSummaryResponse FromEntity(AgentRun run) => new(
        run.Id,
        run.ApplicationId,
        run.AgentId,
        run.AgentNameSnapshot,
        run.RequestedBy,
        run.Status.ToString(),
        run.Stage.ToString(),
        run.Progress,
        run.CreatedAt,
        run.StartedAt,
        run.CompletedAt,
        run.FailureCategory?.ToString(),
        run.FailureMessage,
        RetryEligible: run.Status == JobStatus.Failed);
}

/// <summary>TASK-RDSH-02: one evidence packet entry supplied to the model for a run - see <c>RunEvidence</c>'s remarks on why this is always a snapshot.</summary>
public sealed record RunEvidenceResponse(
    Guid Id,
    string SourceId,
    string FileName,
    string HeadingPath,
    string Passage,
    int CharacterStart,
    int CharacterEnd,
    DateTimeOffset? ProcessedAt);

/// <summary>TASK-RDSH-02: the run detail page's payload - full snapshot/parameter fields, evidence package, and the linked output id (if the run completed and produced one).</summary>
public sealed record AgentRunDetailResponse(
    Guid Id,
    Guid ApplicationId,
    Guid AgentId,
    string AgentName,
    string AgentPromptSnapshot,
    string? KnowledgeScopeSnapshotJson,
    string? DeploymentSnapshot,
    string? RequestedParametersJson,
    string? EffectiveParametersJson,
    string? RetrievalSettingsJson,
    string RequestedBy,
    string UserRequest,
    string? OutputTitle,
    string Status,
    string Stage,
    int Progress,
    string? WorkerId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? HeartbeatAt,
    DateTimeOffset? CompletedAt,
    string? FailureCategory,
    string? FailureMessage,
    string CorrelationId,
    bool CancellationRequested,
    bool RetryEligible,
    Guid? GeneratedOutputId,
    IReadOnlyList<RunEvidenceResponse> Evidence)
{
    public static AgentRunDetailResponse FromEntity(AgentRun run, Guid? generatedOutputId, IReadOnlyList<RunEvidenceResponse> evidence) => new(
        run.Id,
        run.ApplicationId,
        run.AgentId,
        run.AgentNameSnapshot,
        run.AgentPromptSnapshot,
        run.KnowledgeScopeSnapshotJson,
        run.DeploymentSnapshot,
        run.RequestedParametersJson,
        run.EffectiveParametersJson,
        run.RetrievalSettingsJson,
        run.RequestedBy,
        run.UserRequest,
        run.OutputTitle,
        run.Status.ToString(),
        run.Stage.ToString(),
        run.Progress,
        run.WorkerId,
        run.CreatedAt,
        run.StartedAt,
        run.HeartbeatAt,
        run.CompletedAt,
        run.FailureCategory?.ToString(),
        run.FailureMessage,
        run.CorrelationId.ToString(),
        run.CancellationRequested,
        RetryEligible: run.Status == JobStatus.Failed,
        generatedOutputId,
        evidence);
}
