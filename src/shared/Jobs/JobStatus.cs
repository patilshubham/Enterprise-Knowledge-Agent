namespace EnterpriseKnowledgeAgent.Shared.Jobs;

/// <summary>
/// Coarse lifecycle status shared by <c>ProcessingJob</c> and <c>AgentRun</c>
/// (ASDD §9, §12.2). The finer-grained pipeline stage (<c>ProcessingStage</c>
/// for jobs, <c>RunStage</c> for runs) is tracked separately from this status.
/// </summary>
public enum JobStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled,
}
