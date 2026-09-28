using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Infrastructure.Messaging;

/// <summary>TASK-SQS-02: typed producers wrapping <see cref="IMessageQueue"/> with the configured queue name for each message type.</summary>
public sealed class DocumentProcessingProducer(IMessageQueue queue, IOptions<SqsOptions> options) : IDocumentProcessingProducer
{
    public Task SendAsync(DocumentProcessingMessage message, CancellationToken cancellationToken = default) =>
        queue.SendAsync(options.Value.DocumentProcessingQueue, message, cancellationToken);
}

/// <summary>TASK-SQS-02: see <see cref="DocumentProcessingProducer"/> remarks.</summary>
public sealed class AgentExecutionProducer(IMessageQueue queue, IOptions<SqsOptions> options) : IAgentExecutionProducer
{
    public Task SendAsync(AgentExecutionMessage message, CancellationToken cancellationToken = default) =>
        queue.SendAsync(options.Value.AgentExecutionQueue, message, cancellationToken);
}

/// <summary>TASK-SQS-02: see <see cref="DocumentProcessingProducer"/> remarks.</summary>
public sealed class DeletionProducer(IMessageQueue queue, IOptions<SqsOptions> options) : IDeletionProducer
{
    public Task SendAsync(DeletionMessage message, CancellationToken cancellationToken = default) =>
        queue.SendAsync(options.Value.DeletionJobsQueue, message, cancellationToken);
}
