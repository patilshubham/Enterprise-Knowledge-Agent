namespace EnterpriseKnowledgeAgent.Application.Embeddings;

/// <summary>
/// TASK-EMB-02: thrown when an Azure OpenAI embeddings response fails
/// validation (vector count mismatch, wrong dimension count, or a non-finite
/// value). Always terminal/non-retryable (maps to
/// <see cref="EnterpriseKnowledgeAgent.Shared.Errors.ErrorCategory.EmbeddingFailure"/>) -
/// a persistent config/data problem, not a transient one (ASDD §12.4, D10).
/// </summary>
public sealed class EmbeddingValidationException(string message) : InvalidOperationException(message);
