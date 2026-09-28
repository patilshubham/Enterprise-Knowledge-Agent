using System.ClientModel;
using System.Diagnostics;
using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Observability;

namespace EnterpriseKnowledgeAgent.Infrastructure.AzureOpenAI;

/// <summary>
/// TASK-AOI-03: shared bounded exponential-retry + error-mapping policy for
/// every Azure OpenAI call (embeddings and chat alike). Retries only
/// transient failures (429/5xx/timeout), honors a server-supplied
/// Retry-After header when present, and applies an explicit per-attempt
/// timeout from <see cref="AoiOptions.RequestTimeoutSeconds"/>.
/// </summary>
internal static class AzureOpenAiRetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        AoiOptions options,
        string operationName,
        CancellationToken cancellationToken)
    {
        var attempt = 0;
        var stopwatch = Stopwatch.StartNew();
        var outcome = "success";

        try
        {
            while (true)
            {
                attempt++;
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.RequestTimeoutSeconds));

                try
                {
                    return await operation(timeoutCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // The linked CTS fired from CancelAfter, not the caller - this attempt timed out.
                    if (attempt > options.MaxRetries)
                    {
                        outcome = "timeout";
                        throw new AzureOpenAiException(
                            ErrorCategory.Timeout,
                            $"Azure OpenAI request timed out after {options.RequestTimeoutSeconds}s ({attempt} attempt(s)).");
                    }

                    await Task.Delay(ComputeBackoffDelay(attempt), cancellationToken).ConfigureAwait(false);
                }
                catch (ClientResultException ex) when (IsRetryable(ex.Status) && attempt <= options.MaxRetries)
                {
                    if (ex.Status == 429)
                    {
                        AppMetrics.AoaiThrottles.Add(1, new TagList { { "operation", operationName } });
                    }

                    await Task.Delay(ComputeDelay(ex, attempt), cancellationToken).ConfigureAwait(false);
                }
                catch (ClientResultException ex)
                {
                    outcome = ex.Status == 429 ? "throttled" : "error";
                    throw Map(ex);
                }
            }
        }
        finally
        {
            AppMetrics.AoaiRequestDurationMs.Record(
                stopwatch.Elapsed.TotalMilliseconds, new TagList { { "operation", operationName }, { "outcome", outcome } });
        }
    }

    /// <summary>
    /// Module CHAT: maps a raw Azure OpenAI client failure to the platform's
    /// <see cref="AzureOpenAiException"/>. The streaming generation path emits
    /// tokens lazily and therefore cannot run inside <see cref="ExecuteAsync{T}"/>'s
    /// retry loop, so it reuses this mapping directly for consistent error
    /// categories.
    /// </summary>
    public static AzureOpenAiException MapClientException(ClientResultException ex) => Map(ex);

    private static bool IsRetryable(int status) => status == 429 || status is >= 500 and <= 599;

    private static TimeSpan ComputeBackoffDelay(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(200 * Math.Pow(2, attempt - 1), 8_000));

    private static TimeSpan ComputeDelay(ClientResultException ex, int attempt)
    {
        var response = ex.GetRawResponse();
        if (response is not null && response.Headers.TryGetValue("Retry-After", out var retryAfterValue) &&
            double.TryParse(retryAfterValue, out var retryAfterSeconds) && retryAfterSeconds >= 0)
        {
            return TimeSpan.FromSeconds(retryAfterSeconds);
        }

        return ComputeBackoffDelay(attempt);
    }

    private static AzureOpenAiException Map(ClientResultException ex)
    {
        if (ex.Status == 400 && IsContextOverflow(ex.Message))
        {
            return new AzureOpenAiException(ErrorCategory.ContextOverflow, "Azure OpenAI rejected the request: the prompt exceeds the deployment's context window.", ex);
        }

        return ex.Status switch
        {
            429 => new AzureOpenAiException(ErrorCategory.Throttled, "Azure OpenAI throttled the request (429).", ex),
            >= 500 and <= 599 => new AzureOpenAiException(ErrorCategory.DependencyUnavailable, $"Azure OpenAI returned a server error ({ex.Status}).", ex),
            401 or 403 => new AzureOpenAiException(ErrorCategory.Configuration, "Azure OpenAI rejected the request's credentials - check AzureOpenAI auth configuration.", ex),
            404 => new AzureOpenAiException(ErrorCategory.Configuration, "Azure OpenAI deployment was not found - check the configured deployment name.", ex),
            _ => new AzureOpenAiException(ErrorCategory.Configuration, $"Azure OpenAI request failed ({ex.Status}).", ex),
        };
    }

    private static bool IsContextOverflow(string message) =>
        message.Contains("maximum context length", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("context_length_exceeded", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("context window", StringComparison.OrdinalIgnoreCase);
}
