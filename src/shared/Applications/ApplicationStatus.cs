namespace EnterpriseKnowledgeAgent.Shared.Applications;

/// <summary>
/// Lifecycle state of an <c>Application</c> (TASK-APP-01, FRD §4/FR-APP-001,
/// ASDD §9). Applications never hard-delete: <see cref="Deleted"/> is a
/// terminal soft-delete state that keeps historical outputs referenceable
/// (FR-CIT-004).
/// </summary>
public enum ApplicationStatus
{
    Active,
    Archived,
    Deleted,
}
