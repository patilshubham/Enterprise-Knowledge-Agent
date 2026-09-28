using EnterpriseKnowledgeAgent.Shared.Messaging;

namespace EnterpriseKnowledgeAgent.Application.Messaging;

/// <summary>
/// TASK-SQS-02: typed send for the document-processing queue. Only the
/// outbox publisher (TASK-JOB-04) may call this - request handlers create a
/// job + outbox row in one transaction instead of sending directly, to
/// preserve DB/queue consistency (Decision D1).
/// </summary>
public interface IDocumentProcessingProducer
{
    Task SendAsync(DocumentProcessingMessage message, CancellationToken cancellationToken = default);
}

/// <summary>TASK-SQS-02: typed send for the agent-execution queue (see <see cref="IDocumentProcessingProducer"/> remarks).</summary>
public interface IAgentExecutionProducer
{
    Task SendAsync(AgentExecutionMessage message, CancellationToken cancellationToken = default);
}

/// <summary>TASK-SQS-02: typed send for the deletion-jobs queue (see <see cref="IDocumentProcessingProducer"/> remarks).</summary>
public interface IDeletionProducer
{
    Task SendAsync(DeletionMessage message, CancellationToken cancellationToken = default);
}
