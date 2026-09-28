using EnterpriseKnowledgeAgent.Shared.Correlation;
using Serilog.Context;

namespace EnterpriseKnowledgeAgent.Api.Correlation;

/// <summary>
/// TASK-OBS-01 (ASDD §23.1): the first hop of the browser→API→job→queue→
/// worker→AOAI→output→SignalR correlation chain. Reads an inbound
/// <c>X-Correlation-Id</c> header (if the caller already has one - e.g. a
/// prior response, or a future caller-supplied value) and reuses it
/// verbatim; otherwise generates a fresh <see cref="CorrelationId"/>. The
/// resolved value is (a) echoed back as a response header so the caller can
/// log/display it, (b) published to <see cref="CorrelationContext"/> so any
/// downstream code in this request (job creation, the global exception
/// handler, in-process SignalR publishing) can read it without an extra
/// parameter, and (c) pushed onto the Serilog <see cref="LogContext"/> so
/// every structured log line emitted while handling this request carries
/// the same id. Registered first in the pipeline (before
/// <c>UseExceptionHandler</c>) so even an escaped exception's Problem
/// Details response reports the real correlation id.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = TryGetFromHeader(context) ?? CorrelationId.New();

        CorrelationContext.CorrelationId = correlationId;
        context.Response.Headers[CorrelationContext.HeaderName] = correlationId.ToString();

        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            await next(context);
        }
    }

    private static CorrelationId? TryGetFromHeader(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationContext.HeaderName, out var values)
            && CorrelationId.TryParse(values.ToString(), out var parsed))
        {
            return parsed;
        }

        return null;
    }
}
