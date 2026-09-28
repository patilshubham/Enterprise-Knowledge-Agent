using System.Diagnostics;
using EnterpriseKnowledgeAgent.Shared.Observability;

namespace EnterpriseKnowledgeAgent.Api.Observability;

/// <summary>
/// TASK-OBS-02 (ASDD §23.2): records API request latency
/// (<see cref="AppMetrics.ApiRequestDurationMs"/>) and error rate
/// (<see cref="AppMetrics.ApiRequestErrors"/>) for every request. Uses the
/// matched endpoint's route pattern (not the raw request path) as the
/// label so cardinality stays low and no path-embedded identifiers/content
/// leak into metric labels.
/// </summary>
public sealed class RequestMetricsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched";
            var tags = new TagList
            {
                { "http.method", context.Request.Method },
                { "http.route", route },
                { "http.status_code", context.Response.StatusCode },
            };

            AppMetrics.ApiRequestDurationMs.Record(stopwatch.Elapsed.TotalMilliseconds, tags);
            if (context.Response.StatusCode >= 400)
            {
                AppMetrics.ApiRequestErrors.Add(1, tags);
            }
        }
    }
}
