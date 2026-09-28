using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Errors;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Jobs;

/// <summary>TASK-JOB-01: <see cref="ProcessingAttempt"/> is an immutable append-only history row.</summary>
public sealed class ProcessingAttemptTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Start_sets_fields_and_leaves_outcome_unset()
    {
        var jobId = Guid.NewGuid();

        var attempt = ProcessingAttempt.Start(jobId, 1, "worker-1", Now);

        attempt.ProcessingJobId.Should().Be(jobId);
        attempt.AttemptNumber.Should().Be(1);
        attempt.WorkerId.Should().Be("worker-1");
        attempt.StartedAt.Should().Be(Now);
        attempt.EndedAt.Should().BeNull();
        attempt.Outcome.Should().BeNull();
    }

    [Fact]
    public void Close_with_success_sets_outcome_and_end_time()
    {
        var attempt = ProcessingAttempt.Start(Guid.NewGuid(), 1, "worker-1", Now);

        attempt.Close(AttemptOutcome.Succeeded, Now.AddMinutes(1));

        attempt.Outcome.Should().Be(AttemptOutcome.Succeeded);
        attempt.EndedAt.Should().Be(Now.AddMinutes(1));
        attempt.FailureCategory.Should().BeNull();
        attempt.FailureMessage.Should().BeNull();
    }

    [Fact]
    public void Close_with_failure_records_category_and_message()
    {
        var attempt = ProcessingAttempt.Start(Guid.NewGuid(), 1, "worker-1", Now);

        attempt.Close(AttemptOutcome.Failed, Now.AddMinutes(1), ErrorCategory.Timeout, "boom");

        attempt.Outcome.Should().Be(AttemptOutcome.Failed);
        attempt.FailureCategory.Should().Be(ErrorCategory.Timeout);
        attempt.FailureMessage.Should().Be("boom");
    }
}
