namespace EnterpriseKnowledgeAgent.Domain.Jobs;

/// <summary>
/// Thrown when a <c>ProcessingJob</c>/<c>AgentRun</c> lifecycle method is
/// called from a state that does not permit the requested transition
/// (TASK-JOB-01/03/06). The Api layer maps this to a <c>409 Conflict</c>
/// Problem Details response (same convention as <c>ApplicationStateConflictException</c>).
/// </summary>
public sealed class JobStateConflictException(string message) : InvalidOperationException(message)
{
}
