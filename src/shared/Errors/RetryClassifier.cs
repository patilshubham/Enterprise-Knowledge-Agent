namespace EnterpriseKnowledgeAgent.Shared.Errors;

/// <summary>TASK-JOB-06: whether a failure should be retried automatically or is terminal.</summary>
public enum RetryDisposition
{
    Retryable,
    Terminal,
}

/// <summary>
/// TASK-JOB-06 (ASDD §12.4): the single retry-vs-terminal truth table shared
/// by both workers (document + agent), so behavior is consistent regardless
/// of which worker hits the failure. Retryable: transient
/// dependency/throttling/timeout/storage/queue blips (AOAI throttling, S3
/// timeout, DB blip, SQS transient). Terminal: everything else - invalid
/// input, unsupported encoding, empty content, context overflow, auth
/// failures, invalid configuration/deployment, embedding-dimension mismatch,
/// and any unclassified internal failure (retrying a bug without a fix risks
/// an infinite loop, so "unknown" defaults to terminal, not retryable).
/// </summary>
public static class RetryClassifier
{
    /// <summary>Default cap on automatic retry attempts before a job/run is left Failed pending manual retry.</summary>
    public const int DefaultMaxAttempts = 5;

    private static readonly HashSet<ErrorCategory> RetryableCategories =
    [
        ErrorCategory.DependencyUnavailable,
        ErrorCategory.Throttled,
        ErrorCategory.Timeout,
        ErrorCategory.StorageFailure,
        ErrorCategory.QueueFailure,
    ];

    public static RetryDisposition Classify(ErrorCategory category) =>
        RetryableCategories.Contains(category) ? RetryDisposition.Retryable : RetryDisposition.Terminal;

    public static bool IsRetryable(ErrorCategory category) => Classify(category) == RetryDisposition.Retryable;

    /// <summary>Combines category retryability with an attempt-count cap (ASDD §12.4 "retried within limits").</summary>
    public static bool ShouldAutoRetry(ErrorCategory category, int attemptNumber, int maxAttempts = DefaultMaxAttempts) =>
        IsRetryable(category) && attemptNumber < maxAttempts;
}
