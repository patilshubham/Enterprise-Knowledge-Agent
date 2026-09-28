namespace EnterpriseKnowledgeAgent.Application.Processing;

/// <summary>TASK-PRC-03: thrown when normalized content parses to nothing (empty/whitespace-only after normalization).</summary>
public sealed class EmptyDocumentException(string message) : InvalidOperationException(message)
{
}
