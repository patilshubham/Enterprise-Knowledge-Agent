using EnterpriseKnowledgeAgent.Domain.Audit;

namespace EnterpriseKnowledgeAgent.Application.Audit;

/// <summary>
/// TASK-AUD-01: writes a safe, structured <see cref="AuditEvent"/> row for
/// any auditable action (FR-17). Implementations MUST NOT let a write
/// failure block the caller's primary action - failures are caught and
/// logged internally, never rethrown (per the task's "audit write failures
/// never block the primary action" acceptance criterion).
/// </summary>
public interface IAuditWriter
{
    Task WriteAsync(
        string action,
        string actorSubject,
        AuditOutcome outcome = AuditOutcome.Success,
        Guid? applicationId = null,
        string? entityType = null,
        string? entityId = null,
        string? entityDisplayName = null,
        string? failureReason = null,
        Guid? correlationId = null,
        CancellationToken cancellationToken = default);
}
