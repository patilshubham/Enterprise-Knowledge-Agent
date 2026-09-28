namespace EnterpriseKnowledgeAgent.Application.Knowledge;

/// <summary>TASK-KNW-04: thrown when a requested knowledge file does not exist (or does not belong to the expected application). The Api layer maps this to <c>404</c>.</summary>
public sealed class KnowledgeFileNotFoundException(Guid fileId) : Exception($"Knowledge file '{fileId}' was not found.")
{
}
