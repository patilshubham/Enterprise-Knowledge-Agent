using System.Diagnostics;

namespace EnterpriseKnowledgeAgent.Shared.Observability;

/// <summary>
/// TASK-OBS-03 (ASDD §23.3): central <see cref="ActivitySource"/> for the
/// hand-authored trace spans across the agent-run submission (Api), the
/// agent-run pipeline (agent worker), and the document-processing pipeline
/// (document worker). Mirrors <see cref="AppMetrics"/>'s "one shared
/// instrument surface" pattern and lives in Shared for the same reason -
/// call sites span the Api/Application/Infrastructure layers.
/// </summary>
/// <remarks>
/// Span names and tag values must never contain user-supplied content
/// (query text, prompts, document text, file names) - only small
/// closed-set labels and identifiers (application id, run id, agent id,
/// job id) so traces stay safe to export/inspect.
/// </remarks>
public static class AppTracing
{
    public const string SourceName = "EnterpriseKnowledgeAgent";

    public static readonly ActivitySource Source = new(SourceName, "1.0.0");
}
