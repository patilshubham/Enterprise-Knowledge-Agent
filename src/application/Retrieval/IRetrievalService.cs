namespace EnterpriseKnowledgeAgent.Application.Retrieval;

/// <summary>TASK-RET-01..05: orchestrates a single retrieval invocation for a run.</summary>
public interface IRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(RetrievalRequest request, CancellationToken cancellationToken = default);
}
