using EnterpriseKnowledgeAgent.Shared.Correlation;

namespace EnterpriseKnowledgeAgent.Shared.Messaging;

/// <summary>
/// Message sent to the agent-execution SQS queue after a run is submitted
/// (FR-RUN-002). Carries identifiers only; the agent worker loads the agent
/// configuration and the user's request text from durable storage using
/// <see cref="RunId"/> - the prompt/request text is never placed on the queue.
/// </summary>
public sealed record AgentExecutionMessage(
    Guid MessageId,
    Guid ApplicationId,
    Guid AgentId,
    Guid RunId,
    CorrelationId CorrelationId,
    int AttemptNumber) : IQueueMessage;
