using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Jobs;

/// <summary>TASK-JOB-01/03/06: state-transition truth table for <see cref="ProcessingJob"/>.</summary>
public sealed class ProcessingJobTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ProcessingJob CreateJob() =>
        ProcessingJob.Create(Guid.NewGuid(), Guid.NewGuid(), CorrelationId.New(), Now);

    [Fact]
    public void Create_sets_queued_status_and_zeroed_progress()
    {
        var job = CreateJob();

        job.Status.Should().Be(JobStatus.Queued);
        job.Stage.Should().Be(ProcessingStage.Queued);
        job.Progress.Should().Be(0);
        job.AttemptNumber.Should().Be(0);
        job.OutboxMessageId.Should().BeNull();
        job.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void AttachOutboxMessage_sets_the_id()
    {
        var job = CreateJob();
        var outboxId = Guid.NewGuid();

        job.AttachOutboxMessage(outboxId);

        job.OutboxMessageId.Should().Be(outboxId);
    }

    [Fact]
    public void Claim_from_queued_moves_to_running_and_increments_attempt()
    {
        var job = CreateJob();

        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        job.Status.Should().Be(JobStatus.Running);
        job.Stage.Should().Be(ProcessingStage.Downloading);
        job.WorkerId.Should().Be("worker-1");
        job.StartedAt.Should().Be(Now);
        job.LeaseExpiresAt.Should().Be(Now.AddMinutes(5));
        job.AttemptNumber.Should().Be(1);
        job.HeartbeatAt.Should().Be(Now);
    }

    [Fact]
    public void Claim_when_not_queued_throws_conflict()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        var act = () => job.Claim("worker-2", Now, TimeSpan.FromMinutes(5));

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void Heartbeat_while_running_extends_lease()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        job.Heartbeat(Now.AddMinutes(2), TimeSpan.FromMinutes(5));

        job.HeartbeatAt.Should().Be(Now.AddMinutes(2));
        job.LeaseExpiresAt.Should().Be(Now.AddMinutes(7));
    }

    [Fact]
    public void Heartbeat_when_not_running_throws_conflict()
    {
        var job = CreateJob();

        var act = () => job.Heartbeat(Now, TimeSpan.FromMinutes(5));

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void UpdateProgress_while_running_clamps_to_0_100()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        job.UpdateProgress(ProcessingStage.Chunking, 150, Now.AddMinutes(1));
        job.Progress.Should().Be(100);

        job.UpdateProgress(ProcessingStage.Chunking, -10, Now.AddMinutes(1));
        job.Progress.Should().Be(0);
    }

    [Fact]
    public void UpdateProgress_when_not_running_throws_conflict()
    {
        var job = CreateJob();

        var act = () => job.UpdateProgress(ProcessingStage.Chunking, 50, Now);

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void Complete_from_running_sets_completed_and_full_progress()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        job.Complete(Now.AddMinutes(3));

        job.Status.Should().Be(JobStatus.Completed);
        job.Stage.Should().Be(ProcessingStage.Completed);
        job.Progress.Should().Be(100);
        job.CompletedAt.Should().Be(Now.AddMinutes(3));
    }

    [Fact]
    public void Complete_when_not_running_throws_conflict()
    {
        var job = CreateJob();

        var act = () => job.Complete(Now);

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void Fail_from_running_records_category_and_message()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        job.Fail(ErrorCategory.Timeout, "boom", Now.AddMinutes(1));

        job.Status.Should().Be(JobStatus.Failed);
        job.FailureCategory.Should().Be(ErrorCategory.Timeout);
        job.FailureMessage.Should().Be("boom");
        job.CompletedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Fail_from_completed_throws_conflict()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        job.Complete(Now);

        var act = () => job.Fail(ErrorCategory.Timeout, "boom", Now);

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void ReleaseExpiredLease_from_running_returns_to_queued_and_clears_worker_state()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        job.ReleaseExpiredLease(Now.AddMinutes(10));

        job.Status.Should().Be(JobStatus.Queued);
        job.Stage.Should().Be(ProcessingStage.Queued);
        job.WorkerId.Should().BeNull();
        job.StartedAt.Should().BeNull();
        job.LeaseExpiresAt.Should().BeNull();
        job.HeartbeatAt.Should().BeNull();
        // AttemptNumber intentionally preserved so history isn't lost across lease expiry.
        job.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public void ReleaseExpiredLease_when_not_running_throws_conflict()
    {
        var job = CreateJob();

        var act = () => job.ReleaseExpiredLease(Now);

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void RequeueForRetry_from_failed_resets_to_queued_with_new_outbox_id()
    {
        var job = CreateJob();
        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        job.Fail(ErrorCategory.Timeout, "boom", Now.AddMinutes(1));
        var newOutboxId = Guid.NewGuid();

        job.RequeueForRetry(newOutboxId, Now.AddMinutes(2));

        job.Status.Should().Be(JobStatus.Queued);
        job.Stage.Should().Be(ProcessingStage.Queued);
        job.Progress.Should().Be(0);
        job.WorkerId.Should().BeNull();
        job.FailureCategory.Should().BeNull();
        job.FailureMessage.Should().BeNull();
        job.CompletedAt.Should().BeNull();
        job.OutboxMessageId.Should().Be(newOutboxId);
    }

    [Fact]
    public void RequeueForRetry_when_not_failed_throws_conflict()
    {
        var job = CreateJob();

        var act = () => job.RequeueForRetry(Guid.NewGuid(), Now);

        act.Should().Throw<JobStateConflictException>();
    }

    /// <summary>TASK-SIG-02/03: EventSequence is the per-entity monotonic counter carried on every realtime envelope (Shared.Realtime.SignalREvent.Sequence) so clients can ignore stale/out-of-order events.</summary>
    [Fact]
    public void EventSequence_increments_on_every_mutating_transition_and_never_resets()
    {
        var job = CreateJob();
        job.EventSequence.Should().Be(0);

        job.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        job.EventSequence.Should().Be(1);

        job.UpdateProgress(ProcessingStage.Chunking, 50, Now.AddMinutes(1));
        job.EventSequence.Should().Be(2);

        job.Fail(ErrorCategory.Timeout, "boom", Now.AddMinutes(2));
        job.EventSequence.Should().Be(3);

        // RequeueForRetry re-claims/re-runs the same job - sequence keeps climbing, never resets to 0.
        job.RequeueForRetry(Guid.NewGuid(), Now.AddMinutes(3));
        job.Claim("worker-1", Now.AddMinutes(4), TimeSpan.FromMinutes(5));
        job.Complete(Now.AddMinutes(5));
        job.EventSequence.Should().Be(5);
    }
}
