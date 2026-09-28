namespace EnterpriseKnowledgeAgent.Domain.Applications;

/// <summary>
/// Thrown when an <see cref="Application"/> lifecycle method is called from
/// a state that does not permit the requested transition (TASK-APP-01). The
/// Api layer maps this to a <c>409 Conflict</c> Problem Details response.
/// </summary>
public sealed class ApplicationStateConflictException : InvalidOperationException
{
    public ApplicationStateConflictException(string message) : base(message)
    {
    }
}
