namespace EnterpriseKnowledgeAgent.Shared.Correlation;

/// <summary>
/// TASK-OBS-01 (ASDD §23.1): ambient, <see cref="AsyncLocal{T}"/>-based holder
/// for the <see cref="CorrelationId"/> of the request or queue message
/// currently being processed. Set once at the start of each unit of work -
/// the API's correlation middleware for HTTP requests, each worker's
/// message-processing loop for queue messages - and read anywhere
/// downstream in the same async call chain (job creation, outbox writes,
/// realtime publishing, the global exception handler) without threading an
/// extra parameter through every method signature. Mirrors the same pattern
/// as <c>System.Diagnostics.Activity.Current</c>/<c>HttpContext</c>
/// ambient-context conventions already familiar in ASP.NET Core.
/// </summary>
public static class CorrelationContext
{
    /// <summary>
    /// The HTTP header name used to propagate a correlation id across
    /// process boundaries (browser→API, worker→API internal realtime
    /// relay). Shared as a constant so every producer/consumer agrees on
    /// the exact casing/spelling.
    /// </summary>
    public const string HeaderName = "X-Correlation-Id";

    private static readonly AsyncLocal<CorrelationId?> Ambient = new();

    /// <summary>The correlation id for the current logical call chain, or <see langword="null"/> if none has been set.</summary>
    public static CorrelationId? CorrelationId
    {
        get => Ambient.Value;
        set => Ambient.Value = value;
    }

    /// <summary>
    /// Returns the ambient correlation id, or a freshly generated one if
    /// none is set. The fallback should not normally be hit once the API's
    /// correlation middleware and each worker's message loop are wired up -
    /// it exists purely as a defensive default (e.g. for code paths invoked
    /// outside of any request/message scope, such as design-time tooling).
    /// </summary>
    public static CorrelationId CorrelationIdOrNew() => Ambient.Value ?? global::EnterpriseKnowledgeAgent.Shared.Correlation.CorrelationId.New();
}
