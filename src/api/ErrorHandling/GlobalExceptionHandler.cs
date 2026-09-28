using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseKnowledgeAgent.Api.ErrorHandling;

/// <summary>
/// TASK-ERR-01 (ASDD §22): last-resort safety net for any exception that
/// escapes an endpoint's local try/catch (or a code path with no local
/// handling at all). Endpoint-local catch blocks remain responsible for
/// shaping well-known domain exceptions with endpoint-specific titles
/// (not-found → 404, name/state conflicts → 409, validation → 400 - see
/// e.g. <c>ApplicationsEndpoints.MapApplicationException</c>); this handler
/// only needs to (a) classify AOAI/agent-run failures that already carry an
/// <see cref="ErrorCategory"/>, and (b) guarantee that literally anything
/// else still comes back as a safe, category-mapped Problem Details
/// response - title/detail/recommended action/retry availability/
/// correlation id - instead of leaking a stack trace or an ASP.NET default
/// error page. The raw exception (with stack trace) is logged server-side
/// only, never included in the response.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var category = Categorize(exception);
        var mapping = ErrorCategoryHttpMapping.Resolve(category);
        var correlationId = CorrelationContext.CorrelationIdOrNew().ToString();

        logger.UnhandledException(exception, correlationId, category.ToString());

        httpContext.Response.StatusCode = mapping.StatusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Title = mapping.Title,
                Detail = mapping.SafeDetail,
                Status = mapping.StatusCode,
                Extensions =
                {
                    ["correlationId"] = correlationId,
                    ["errorCategory"] = category.ToString(),
                    ["retryable"] = mapping.IsRetryable,
                    ["recommendedAction"] = mapping.RecommendedAction,
                },
            },
        });
    }

    /// <summary>
    /// TASK-ERR-01: exceptions that already self-report an <see cref="ErrorCategory"/>
    /// (<see cref="AzureOpenAiException"/>, <see cref="AgentRunExecutionException"/>) use it
    /// directly; a few common BCL exception types get a reasonable default; anything
    /// unrecognized is treated as an unclassified internal failure (never retried
    /// automatically - see <see cref="RetryClassifier"/>).
    /// </summary>
    internal static ErrorCategory Categorize(Exception exception) => exception switch
    {
        AzureOpenAiException aoai => aoai.Category,
        AgentRunExecutionException run => run.Category,
        ArgumentException => ErrorCategory.Validation,
        UnauthorizedAccessException => ErrorCategory.Authorization,
        _ => ErrorCategory.InternalFailure,
    };
}
