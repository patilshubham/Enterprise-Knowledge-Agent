namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-01 (D10): thrown when the application's stored chunk embeddings
/// were produced by a different Azure OpenAI embedding deployment than the
/// one currently configured for query embedding. Retrieval must never run a
/// vector search across mismatched embedding spaces - it fails fast instead.
/// </summary>
public sealed class RetrievalConfigurationException(string message) : Exception(message);
