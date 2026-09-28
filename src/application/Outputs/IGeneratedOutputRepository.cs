using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Shared.Jobs;

namespace EnterpriseKnowledgeAgent.Application.Outputs;

/// <summary>TASK-OUT-01: persistence for <see cref="GeneratedOutput"/>.</summary>
public interface IGeneratedOutputRepository
{
    Task AddAsync(GeneratedOutput output, CancellationToken cancellationToken = default);

    Task<GeneratedOutput?> GetAsync(Guid outputId, CancellationToken cancellationToken = default);

    /// <summary>TASK-RUN-02: idempotency lookup so a redelivered/duplicate run completion never persists a second output for the same run.</summary>
    Task<GeneratedOutput?> GetByAgentRunAsync(Guid agentRunId, CancellationToken cancellationToken = default);

    /// <summary>
    /// TASK-OUT-05: the output library listing - newest first, excludes
    /// soft-deleted rows, and returns the whole (already application-scoped)
    /// result set unpaged, mirroring <c>IKnowledgeFileRepository.ListAsync</c>'s
    /// convention of leaving pagination to the frontend's table component.
    /// <paramref name="agentId"/> and <paramref name="runStatus"/> filter via
    /// the owning <c>AgentRun</c> (module 19) since <see cref="GeneratedOutput"/>
    /// itself does not store either.
    /// </summary>
    Task<IReadOnlyList<GeneratedOutput>> ListAsync(
        Guid applicationId,
        string? search = null,
        Guid? agentId = null,
        string? createdBy = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        JobStatus? runStatus = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
