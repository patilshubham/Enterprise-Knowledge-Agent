namespace EnterpriseKnowledgeAgent.Shared.Errors;

/// <summary>
/// Canonical error categories used across API Problem Details responses,
/// failure records, and audit entries (ASDD §22).
/// </summary>
public enum ErrorCategory
{
    Validation,
    Authentication,
    Authorization,
    Conflict,
    Configuration,
    DependencyUnavailable,
    Throttled,
    Timeout,
    ContextOverflow,
    RetrievalEmpty,
    EmbeddingFailure,
    GenerationFailure,
    StorageFailure,
    QueueFailure,
    OutputValidationFailure,
    InternalFailure,
}
