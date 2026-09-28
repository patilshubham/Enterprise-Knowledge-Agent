using EnterpriseKnowledgeAgent.Domain.Citations;

namespace EnterpriseKnowledgeAgent.Application.Citations;

/// <summary>TASK-CIT-01/CIT-03: persistence for <see cref="Citation"/>, scoped by output (ASDD §17.1).</summary>
public interface ICitationRepository
{
    Task<IReadOnlyList<Citation>> ListByOutputAsync(Guid generatedOutputId, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IReadOnlyList<Citation> citations, CancellationToken cancellationToken = default);
}
