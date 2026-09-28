using EnterpriseKnowledgeAgent.Shared.Observability;

namespace EnterpriseKnowledgeAgent.Application.Citations;

/// <summary>
/// TASK-CIT-03: validates a generated output's citation markers against the
/// run's persisted <see cref="EnterpriseKnowledgeAgent.Domain.Citations.RunEvidence"/>
/// and persists a <see cref="EnterpriseKnowledgeAgent.Domain.Citations.Citation"/>
/// for each valid one. Idempotent per output (ASDD §12.1 at-least-once
/// delivery): a retried worker attempt for the same
/// <paramref name="generatedOutputId"/>-shaped call returns the
/// already-persisted citations instead of duplicating them.
/// </summary>
/// <remarks>
/// Does not enforce the agent's <c>CitationsRequired</c> policy - the caller
/// (module 19's run pipeline, TASK-RUN-02) decides how to react when
/// <see cref="CitationMarkerValidator.CitationValidationResult.Citations"/>
/// is empty for an agent that requires citations.
/// </remarks>
public sealed class CitationService(ICitationRepository citationRepository)
{
    public async Task<CitationMarkerValidator.CitationValidationResult> ValidateAndPersistAsync(
        Guid applicationId,
        Guid generatedOutputId,
        IReadOnlyList<string> citationMarkers,
        IReadOnlyList<Domain.Citations.RunEvidence> runEvidence,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var existing = await citationRepository.ListByOutputAsync(generatedOutputId, cancellationToken);
        if (existing.Count > 0)
        {
            return new CitationMarkerValidator.CitationValidationResult(existing, InvalidMarkers: []);
        }

        var result = CitationMarkerValidator.Validate(applicationId, generatedOutputId, citationMarkers, runEvidence, now);
        if (result.InvalidMarkers.Count > 0)
        {
            AppMetrics.InvalidCitations.Add(result.InvalidMarkers.Count);
        }

        if (result.Citations.Count > 0)
        {
            await citationRepository.AddRangeAsync(result.Citations, cancellationToken);
        }

        return result;
    }
}
