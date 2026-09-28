namespace EnterpriseKnowledgeAgent.Domain.Jobs;

/// <summary>Outcome of a single processing attempt (TASK-JOB-01), used by retry classification (TASK-JOB-06) and diagnostics.</summary>
public enum AttemptOutcome
{
    Succeeded,
    Failed,
    Abandoned,
}
