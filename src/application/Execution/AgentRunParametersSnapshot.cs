namespace EnterpriseKnowledgeAgent.Application.Execution;

/// <summary>
/// TASK-RUN-01/GEN-01: the requested-vs-effective generation parameter
/// snapshot stored (as JSON) on <c>AgentRun.RequestedParametersJson</c> /
/// <c>AgentRun.EffectiveParametersJson</c>. Requested and effective are
/// identical at submission time; TASK-GEN-01 overwrites only the effective
/// snapshot if generation-time capability checks adjust anything (e.g.
/// <see cref="TopP"/> dropped to <see langword="null"/> because the
/// deployment doesn't support it).
/// </summary>
public sealed record AgentRunParametersSnapshot(double Temperature, int MaximumOutputTokens, double? TopP);
