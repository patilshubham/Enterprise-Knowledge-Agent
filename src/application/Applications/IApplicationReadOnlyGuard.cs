namespace EnterpriseKnowledgeAgent.Application.Applications;

/// <summary>
/// TASK-APP-04: the centralized "is this application still mutable?"
/// precondition, meant to be injected into future modules' command handlers
/// (uploads, agent config, run submission, output edits - modules 8/15/19+,
/// none of which exist yet). Keeping this check in one place avoids each
/// module re-deriving archived/deleted handling inconsistently (FR-APP-003).
/// The Applications module's OWN endpoints do not use this guard - their
/// lifecycle rules are enforced directly by the <c>Application</c> entity's
/// guarded methods (e.g. <c>Update</c> is intentionally still allowed while
/// Archived, only blocked once Deleted).
/// </summary>
public interface IApplicationReadOnlyGuard
{
    /// <summary>
    /// Throws <see cref="ApplicationNotFoundException"/> if <paramref name="applicationId"/>
    /// doesn't resolve to an application, or <see cref="ApplicationReadOnlyException"/>
    /// if it resolves but is Archived or Deleted. Returns normally (no value)
    /// when the application is Active and safe to mutate.
    /// </summary>
    Task EnsureMutableAsync(Guid applicationId, CancellationToken cancellationToken = default);
}
