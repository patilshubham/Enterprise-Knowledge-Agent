namespace EnterpriseKnowledgeAgent.Domain.Operations;

/// <summary>Which typed producer (TASK-SQS-02) an <see cref="OutboxMessage"/> should be delivered through.</summary>
public enum OutboxMessageType
{
    DocumentProcessing,
    AgentExecution,
    Deletion,
}
