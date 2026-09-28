namespace EnterpriseKnowledgeAgent.Domain.Audit;

/// <summary>TASK-AUD-01: whether an audited action succeeded or failed (FR-17).</summary>
public enum AuditOutcome
{
    Success = 0,
    Failure = 1,
}
