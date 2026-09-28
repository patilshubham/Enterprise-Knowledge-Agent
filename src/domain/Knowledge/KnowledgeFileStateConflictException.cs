namespace EnterpriseKnowledgeAgent.Domain.Knowledge;

/// <summary>
/// Thrown when a <see cref="KnowledgeFile"/> lifecycle method is called from
/// a state that does not permit the requested transition (TASK-KNW-01). The
/// Api layer maps this to a <c>409 Conflict</c> Problem Details response.
/// </summary>
public sealed class KnowledgeFileStateConflictException : InvalidOperationException
{
    public KnowledgeFileStateConflictException(string message) : base(message)
    {
    }
}
