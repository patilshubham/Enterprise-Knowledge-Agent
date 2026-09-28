namespace EnterpriseKnowledgeAgent.Shared.Errors;

/// <summary>
/// TASK-ERR-01 (ASDD §22): the single source of truth mapping each
/// <see cref="ErrorCategory"/> to a safe, user-facing HTTP Problem Details
/// shape - status code, short title, a safe explanation (never derived from
/// an exception's raw <c>Message</c>, which may contain internal details),
/// and a recommended action. Retry availability is derived from the
/// existing <see cref="RetryClassifier"/> truth table so the two never
/// drift out of sync.
/// </summary>
public sealed record ErrorCategoryMapping(int StatusCode, string Title, string SafeDetail, string RecommendedAction)
{
    public bool IsRetryable { get; init; }
}

public static class ErrorCategoryHttpMapping
{
    private static readonly Dictionary<ErrorCategory, ErrorCategoryMapping> Mappings = new()
    {
        [ErrorCategory.Validation] = new(400, "Invalid request", "The request contains invalid or missing data.", "Correct the highlighted fields and try again."),
        [ErrorCategory.Authentication] = new(401, "Authentication required", "You must sign in to perform this action.", "Sign in and try again."),
        [ErrorCategory.Authorization] = new(403, "Not authorized", "You don't have permission to perform this action.", "Contact an administrator if you believe this is a mistake."),
        [ErrorCategory.Conflict] = new(409, "Conflict", "The request conflicts with the current state of the resource.", "Refresh and try again."),
        [ErrorCategory.Configuration] = new(500, "Configuration error", "The service is misconfigured.", "Contact your administrator."),
        [ErrorCategory.DependencyUnavailable] = new(503, "Dependency unavailable", "A required dependency is temporarily unavailable.", "Try again shortly."),
        [ErrorCategory.Throttled] = new(429, "Rate limited", "Too many requests were made in a short period.", "Wait a moment and try again."),
        [ErrorCategory.Timeout] = new(504, "Request timed out", "The request took too long to complete.", "Try again."),
        [ErrorCategory.ContextOverflow] = new(400, "Request too large", "The request exceeds the maximum supported size.", "Reduce the amount of content and try again."),
        [ErrorCategory.RetrievalEmpty] = new(422, "No results found", "No relevant information was found for this request.", "Refine your query or check the knowledge base."),
        [ErrorCategory.EmbeddingFailure] = new(502, "Embedding failed", "Failed to generate embeddings for this request.", "Try again; contact support if it persists."),
        [ErrorCategory.GenerationFailure] = new(502, "Generation failed", "Failed to generate a response for this request.", "Try again; contact support if it persists."),
        [ErrorCategory.StorageFailure] = new(503, "Storage error", "A storage operation failed.", "Try again shortly."),
        [ErrorCategory.QueueFailure] = new(503, "Queue error", "A queue operation failed.", "Try again shortly."),
        [ErrorCategory.OutputValidationFailure] = new(502, "Output validation failed", "The generated output failed validation.", "Try again; contact support if it persists."),
        [ErrorCategory.InternalFailure] = new(500, "Unexpected error", "An unexpected error occurred.", "Try again; contact support if it persists."),
    };

    /// <summary>Looks up the safe Problem Details shape for a category, with retry availability filled in from <see cref="RetryClassifier"/>.</summary>
    public static ErrorCategoryMapping Resolve(ErrorCategory category)
    {
        var mapping = Mappings[category];
        return mapping with { IsRetryable = RetryClassifier.IsRetryable(category) };
    }
}
