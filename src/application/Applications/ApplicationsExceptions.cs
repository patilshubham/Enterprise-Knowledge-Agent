namespace EnterpriseKnowledgeAgent.Application.Applications;

/// <summary>TASK-APP-02: thrown when an applicationId doesn't resolve to an existing application. The Api layer maps this to <c>404 Not Found</c>.</summary>
public sealed class ApplicationNotFoundException(Guid applicationId) : Exception($"Application '{applicationId}' was not found.")
{
    public Guid ApplicationId { get; } = applicationId;
}

/// <summary>TASK-APP-01: thrown when a create/rename would collide with another non-deleted application's name. The Api layer maps this to <c>409 Conflict</c>.</summary>
public sealed class ApplicationNameConflictException(string name) : Exception($"An application named '{name}' already exists.")
{
    public string Name { get; } = name;
}

/// <summary>
/// TASK-APP-04: thrown by <see cref="IApplicationReadOnlyGuard"/> when a
/// state-changing operation in ANOTHER module (uploads, agent config, run
/// submission, output edits) targets an archived or deleted application.
/// The centralized guard exists so every future module enforces this
/// consistently rather than re-deriving the check ad hoc. The owning
/// module's endpoint maps this to <c>409 Conflict</c>, matching
/// <see cref="Domain.Applications.ApplicationStateConflictException"/>'s
/// convention for the Applications module's own lifecycle guards.
/// </summary>
public sealed class ApplicationReadOnlyException(Guid applicationId, string status)
    : Exception($"Application '{applicationId}' is read-only (status: {status}) and cannot accept new mutations.")
{
    public Guid ApplicationId { get; } = applicationId;
    public string Status { get; } = status;
}
