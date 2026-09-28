using EnterpriseKnowledgeAgent.Domain.Citations;

namespace EnterpriseKnowledgeAgent.Application.Citations;

/// <summary>TASK-CIT-01/CIT-02: persistence for <see cref="RunEvidence"/>, scoped by run (ASDD §17.1).</summary>
public interface IRunEvidenceRepository
{
    /// <summary>Ordered by <see cref="RunEvidence.SourceId"/> (SOURCE-001, SOURCE-002, ...).</summary>
    Task<IReadOnlyList<RunEvidence>> ListByRunAsync(Guid agentRunId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<RunEvidence> evidence, CancellationToken cancellationToken = default);
}
