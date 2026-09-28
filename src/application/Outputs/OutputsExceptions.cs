namespace EnterpriseKnowledgeAgent.Application.Outputs;

/// <summary>
/// TASK-OUT-02/03/04: thrown when an outputId doesn't resolve to an existing,
/// non-deleted output (or doesn't belong to the requested application). The
/// Api layer maps this to <c>404 Not Found</c>.
/// </summary>
public sealed class OutputNotFoundException(Guid outputId) : Exception($"Output '{outputId}' was not found.")
{
    public Guid OutputId { get; } = outputId;
}

/// <summary>
/// TASK-CIT-04: thrown when a citationId doesn't resolve to an existing
/// citation for the requested output. The Api layer maps this to <c>404 Not Found</c>.
/// </summary>
public sealed class CitationNotFoundException(Guid citationId) : Exception($"Citation '{citationId}' was not found.")
{
    public Guid CitationId { get; } = citationId;
}
