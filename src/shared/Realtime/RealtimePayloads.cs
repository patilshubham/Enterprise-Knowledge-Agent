using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Knowledge;

namespace EnterpriseKnowledgeAgent.Shared.Realtime;

/// <summary>
/// TASK-SIG-02: safe payload types for the <see cref="RealtimeEventTypes"/>
/// catalog - identifiers, enums, and small counters only, never file/output
/// content, prompts, or secrets (ASDD §18.1/§18.3).
/// </summary>
public sealed record FileQueuedPayload(Guid KnowledgeFileId);

public sealed record FileStageChangedPayload(Guid KnowledgeFileId, ProcessingStage Stage, int Progress);

public sealed record FileCompletedPayload(Guid KnowledgeFileId);

public sealed record FileFailedPayload(Guid KnowledgeFileId, ErrorCategory FailureCategory);

public sealed record RunQueuedPayload(Guid AgentRunId, Guid AgentId);

public sealed record RunStageChangedPayload(Guid AgentRunId, RunStage Stage, int Progress);

public sealed record RunCompletedPayload(Guid AgentRunId);

public sealed record RunFailedPayload(Guid AgentRunId, ErrorCategory FailureCategory);

public sealed record RunCancelledPayload(Guid AgentRunId);

/// <summary>Deliberately carries no counters itself (ASDD §18.4: REST stays authoritative) - recipients just invalidate/refetch their dashboard query.</summary>
public sealed record DashboardChangedPayload(string Reason);
