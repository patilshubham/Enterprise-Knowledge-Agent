namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>
/// TASK-RET-04/05: the outcome of a single <see cref="RetrievalService.RetrieveAsync"/>
/// call.
/// </summary>
/// <param name="NoEvidence">
/// TASK-RET-05: true when no candidate qualified (met <see cref="RetrievalSettings.MinimumSimilarity"/>
/// and fit within <see cref="RetrievalSettings.EvidenceTokenBudget"/>), so <see cref="Sources"/> is empty.
/// The consuming execution step (module 20/21) must treat this as a controlled
/// no-information result when <see cref="RetrievalSettings.StrictGrounding"/> is set - never
/// ask the model to invent content.
/// </param>
/// <param name="Sources">The assembled evidence package, in citation order (<c>SOURCE-001</c> first).</param>
/// <param name="ExcludedFileIds">
/// TASK-RET-05: files that were in the requested scope (<see cref="RetrievalRequest.SelectedFileIds"/>,
/// or all application files when that was <see langword="null"/>) but were excluded from the
/// search because they are not Ready and enabled (e.g. still processing, disabled, or deleted) -
/// for UI disclosure before execution.
/// </param>
public sealed record RetrievalResult(
    bool NoEvidence,
    IReadOnlyList<EvidenceSource> Sources,
    IReadOnlyList<Guid> ExcludedFileIds)
{
    /// <summary>Convenience accessor - true when the run has at least one usable evidence source.</summary>
    public bool HasEvidence => !NoEvidence && Sources.Count > 0;
}
