using EnterpriseKnowledgeAgent.Domain.Citations;

namespace EnterpriseKnowledgeAgent.Application.Citations;

/// <summary>
/// TASK-CIT-03: parses citation markers extracted by TASK-GEN-02
/// (<c>[SOURCE-NNN]</c>, per <see cref="EnterpriseKnowledgeAgent.Application.PromptAssembly.PromptAssembler.ProtocolInstructions"/>)
/// and validates each references a SOURCE ID present in the run's own
/// <see cref="RunEvidence"/> set. Unknown markers - including any naming a
/// source from another application, which can never appear in this run's
/// evidence because retrieval (module 15/16) is already application-scoped -
/// are rejected, never trusting the model's own claims about which files it
/// used. Pure/static so it is directly unit testable without a database.
/// </summary>
public static class CitationMarkerValidator
{
    /// <param name="Citations">One <see cref="Citation"/> per marker that referenced a valid, in-run SOURCE ID.</param>
    /// <param name="InvalidMarkers">Markers that referenced an unknown (or, equivalently, cross-application) SOURCE ID - not persisted, only counted for observability.</param>
    public sealed record CitationValidationResult(IReadOnlyList<Citation> Citations, IReadOnlyList<string> InvalidMarkers);

    public static CitationValidationResult Validate(
        Guid applicationId,
        Guid generatedOutputId,
        IReadOnlyList<string> citationMarkers,
        IReadOnlyList<RunEvidence> runEvidence,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(citationMarkers);
        ArgumentNullException.ThrowIfNull(runEvidence);

        var evidenceBySourceId = runEvidence.ToDictionary(e => e.SourceId, e => e);
        var citations = new List<Citation>();
        var invalidMarkers = new List<string>();

        foreach (var marker in citationMarkers)
        {
            var sourceId = ExtractSourceId(marker);
            if (sourceId is not null && evidenceBySourceId.TryGetValue(sourceId, out var evidence))
            {
                citations.Add(Citation.Create(applicationId, generatedOutputId, evidence.Id, now));
            }
            else
            {
                invalidMarkers.Add(marker);
            }
        }

        return new CitationValidationResult(citations, invalidMarkers);
    }

    /// <summary>Strips the enclosing brackets from a <c>[SOURCE-NNN]</c> marker.</summary>
    private static string? ExtractSourceId(string marker)
    {
        if (marker.Length < 3 || marker[0] != '[' || marker[^1] != ']')
        {
            return null;
        }

        return marker[1..^1];
    }
}
