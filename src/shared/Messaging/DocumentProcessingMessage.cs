using EnterpriseKnowledgeAgent.Shared.Correlation;

namespace EnterpriseKnowledgeAgent.Shared.Messaging;

/// <summary>
/// Message sent to the document-processing SQS queue after a file is
/// uploaded (FR-PRC-001). Carries identifiers only; the document worker
/// loads the actual file content from S3 using <see cref="KnowledgeFileId"/>.
/// </summary>
public sealed record DocumentProcessingMessage(
    Guid MessageId,
    Guid ApplicationId,
    Guid KnowledgeFileId,
    CorrelationId CorrelationId,
    int AttemptNumber) : IQueueMessage;
