namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-01: a single retrieval invocation, scoped to one application.
/// </summary>
/// <param name="ApplicationId">Application to search within (FR-RET-001 - every candidate query is app-scoped).</param>
/// <param name="Query">The retrieval query text - the user request, optionally combined with the agent's objective for broad generation. Never the entire agent prompt (§15.3).</param>
/// <param name="Settings">Per-agent retrieval parameters (FR-PAR-002).</param>
/// <param name="SelectedFileIds">
/// TASK-RET-01: optional explicit file scope. When <see langword="null"/>,
/// all of the application's files are in scope (subject to Ready+enabled
/// filtering, TASK-RET-05). When supplied, only these files are searched,
/// and any of them that are not Ready+enabled are reported via
/// <see cref="RetrievalResult.ExcludedFileIds"/> instead of causing a failure.
/// </param>
public sealed record RetrievalRequest(
    Guid ApplicationId,
    string Query,
    RetrievalSettings Settings,
    IReadOnlyList<Guid>? SelectedFileIds = null);
