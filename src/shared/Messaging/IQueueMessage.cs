using EnterpriseKnowledgeAgent.Shared.Correlation;

namespace EnterpriseKnowledgeAgent.Shared.Messaging;

/// <summary>
/// Common envelope fields present on every queue message in the system.
/// Implementations MUST carry identifiers only - never raw file content,
/// prompts, generated text, or secrets (FR-PRC-001: "File content shall not
/// be placed in the queue message"). This constraint is enforced by a
/// reflection-based unit test in the Shared test project.
/// </summary>
public interface IQueueMessage
{
    /// <summary>Unique id for this specific message delivery attempt, used for idempotency (FR-PRC-006).</summary>
    Guid MessageId { get; }

    /// <summary>Correlation id propagated from the originating HTTP request.</summary>
    CorrelationId CorrelationId { get; }

    /// <summary>1-based count of delivery/processing attempts for this logical job.</summary>
    int AttemptNumber { get; }
}
