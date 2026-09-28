using EnterpriseKnowledgeAgent.Shared.Correlation;

namespace EnterpriseKnowledgeAgent.Shared.Messaging;

/// <summary>
/// Message sent to the deletion-jobs SQS queue to asynchronously clean up
/// stored content and vectors for a removed knowledge file (FR-KNL: file
/// deletion). Carries identifiers only.
/// </summary>
public sealed record DeletionMessage(
    Guid MessageId,
    Guid ApplicationId,
    Guid KnowledgeFileId,
    CorrelationId CorrelationId,
    int AttemptNumber) : IQueueMessage;
