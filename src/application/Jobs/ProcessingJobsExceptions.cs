namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>TASK-PRDSH-01/02: thrown when a processing job doesn't exist, or exists but isn't visible in the requested scope (cross-application access - 404-masking, mirrors <c>OutputNotFoundException</c>).</summary>
public sealed class ProcessingJobNotFoundException(Guid jobId) : Exception($"Processing job '{jobId}' was not found.")
{
}
