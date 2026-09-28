using EnterpriseKnowledgeAgent.Shared.Errors;

namespace EnterpriseKnowledgeAgent.Domain.Jobs;

/// <summary>
/// TASK-JOB-01: an immutable history record for a single claim/attempt of a
/// <see cref="ProcessingJob"/> (ASDD §9). One row is written each time a
/// worker claims the job, and closed out (outcome + end time) when the
/// attempt finishes - regardless of whether it succeeded, failed, or was
/// abandoned by a lease expiry.
/// </summary>
public sealed class ProcessingAttempt
{
    public Guid Id { get; private set; }

    public Guid ProcessingJobId { get; private set; }

    public int AttemptNumber { get; private set; }

    public string WorkerId { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public AttemptOutcome? Outcome { get; private set; }

    public ErrorCategory? FailureCategory { get; private set; }

    public string? FailureMessage { get; private set; }

    private ProcessingAttempt()
    {
    }

    public static ProcessingAttempt Start(Guid processingJobId, int attemptNumber, string workerId, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        ProcessingJobId = processingJobId,
        AttemptNumber = attemptNumber,
        WorkerId = workerId,
        StartedAt = now,
    };

    public void Close(AttemptOutcome outcome, DateTimeOffset now, ErrorCategory? failureCategory = null, string? failureMessage = null)
    {
        Outcome = outcome;
        EndedAt = now;
        FailureCategory = failureCategory;
        FailureMessage = failureMessage;
    }
}
