namespace EnterpriseKnowledgeAgent.Domain.Audit;

/// <summary>
/// TASK-AUD-01: an append-only, tamper-evident audit trail row (FR-17,
/// ASDD §9). Records who did what, to which application/entity, whether it
/// succeeded, and a correlation ID linking it to the request/job/run that
/// produced it. Deliberately narrow: never stores source document content,
/// prompts, generated output text, or secrets - only IDs and short, safe
/// display labels (e.g. a file name or agent name, already shown throughout
/// the UI) suitable for a human-readable audit trail.
/// </summary>
public sealed class AuditEvent
{
    public Guid Id { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>A stable action code from <c>AuditActions</c> (application layer), e.g. "ApplicationCreated".</summary>
    public string Action { get; private set; } = string.Empty;

    /// <summary>Keycloak subject id ("sub") of the acting user, or "unknown"/"system" for hooks that run outside a request.</summary>
    public string ActorSubject { get; private set; } = string.Empty;

    public AuditOutcome Outcome { get; private set; }

    /// <summary>Null for platform-level events (login, platform-wide config) not scoped to one application.</summary>
    public Guid? ApplicationId { get; private set; }

    /// <summary>Short entity kind, e.g. "KnowledgeFile", "Agent", "GeneratedOutput". Null when not applicable.</summary>
    public string? EntityType { get; private set; }

    /// <summary>The affected entity's id (as a string to accommodate non-Guid ids). Null when not applicable.</summary>
    public string? EntityId { get; private set; }

    /// <summary>Safe, already-public display label for the entity (e.g. a file/agent/application name) - never full content.</summary>
    public string? EntityDisplayName { get; private set; }

    /// <summary>Short, safe failure reason (e.g. an exception type name or fixed category) when <see cref="Outcome"/> is Failure. Never a raw message/stack trace.</summary>
    public string? FailureReason { get; private set; }

    public Guid? CorrelationId { get; private set; }

    private AuditEvent()
    {
    }

    public static AuditEvent Create(
        string action,
        string actorSubject,
        AuditOutcome outcome,
        DateTimeOffset now,
        Guid? applicationId = null,
        string? entityType = null,
        string? entityId = null,
        string? entityDisplayName = null,
        string? failureReason = null,
        Guid? correlationId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorSubject);

        return new AuditEvent
        {
            Id = Guid.NewGuid(),
            OccurredAt = now,
            Action = action,
            ActorSubject = actorSubject,
            Outcome = outcome,
            ApplicationId = applicationId,
            EntityType = entityType,
            EntityId = entityId,
            EntityDisplayName = entityDisplayName,
            FailureReason = failureReason,
            CorrelationId = correlationId,
        };
    }
}
