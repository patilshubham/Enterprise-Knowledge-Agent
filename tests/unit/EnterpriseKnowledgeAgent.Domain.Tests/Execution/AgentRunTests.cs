using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Errors;
using EnterpriseKnowledgeAgent.Shared.Execution;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Domain.Tests.Execution;

/// <summary>TASK-JOB-01/03/06: state-transition truth table for <see cref="AgentRun"/>.</summary>
public sealed class AgentRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AgentRun CreateRun() =>
        AgentRun.Create(
            applicationId: Guid.NewGuid(),
            agentId: Guid.NewGuid(),
            requestedBy: "keycloak-sub-1",
            userRequest: "Summarize the Q3 report",
            idempotencyKey: Guid.NewGuid().ToString(),
            outputTitle: null,
            agentNameSnapshot: "Support Agent",
            agentPromptSnapshot: "You are a helpful assistant.",
            knowledgeScopeSnapshotJson: null,
            deploymentSnapshot: "gpt-4o",
            requestedParametersJson: null,
            effectiveParametersJson: null,
            retrievalSettingsJson: null,
            correlationId: CorrelationId.New(),
            now: Now);

    [Fact]
    public void Create_sets_queued_status_and_snapshot_fields()
    {
        var run = CreateRun();

        run.Status.Should().Be(JobStatus.Queued);
        run.Stage.Should().Be(RunStage.Queued);
        run.Progress.Should().Be(0);
        run.AttemptNumber.Should().Be(0);
        run.OutboxMessageId.Should().BeNull();
        run.AgentNameSnapshot.Should().Be("Support Agent");
        run.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void AttachOutboxMessage_sets_the_id()
    {
        var run = CreateRun();
        var outboxId = Guid.NewGuid();

        run.AttachOutboxMessage(outboxId);

        run.OutboxMessageId.Should().Be(outboxId);
    }

    [Fact]
    public void Claim_from_queued_moves_to_running_preparing_stage()
    {
        var run = CreateRun();

        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.Status.Should().Be(JobStatus.Running);
        run.Stage.Should().Be(RunStage.Preparing);
        run.WorkerId.Should().Be("worker-1");
        run.LeaseExpiresAt.Should().Be(Now.AddMinutes(5));
        run.AttemptNumber.Should().Be(1);
    }

    [Fact]
    public void Claim_when_not_queued_throws_conflict()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        var act = () => run.Claim("worker-2", Now, TimeSpan.FromMinutes(5));

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void Heartbeat_while_running_extends_lease()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.Heartbeat(Now.AddMinutes(2), TimeSpan.FromMinutes(5));

        run.HeartbeatAt.Should().Be(Now.AddMinutes(2));
        run.LeaseExpiresAt.Should().Be(Now.AddMinutes(7));
    }

    [Fact]
    public void Heartbeat_when_not_running_throws_conflict()
    {
        var run = CreateRun();

        var act = () => run.Heartbeat(Now, TimeSpan.FromMinutes(5));

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void UpdateProgress_while_running_clamps_to_0_100()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.UpdateProgress(RunStage.Generating, 150, Now.AddMinutes(1));
        run.Progress.Should().Be(100);

        run.UpdateProgress(RunStage.Generating, -10, Now.AddMinutes(1));
        run.Progress.Should().Be(0);
    }

    [Fact]
    public void RecordEffectiveParameters_while_running_overwrites_the_snapshot()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.RecordEffectiveParameters("""{"temperature":0.7,"maximumOutputTokens":2048,"topP":null}""");

        run.EffectiveParametersJson.Should().Be("""{"temperature":0.7,"maximumOutputTokens":2048,"topP":null}""");
    }

    [Fact]
    public void RecordEffectiveParameters_when_not_running_throws_conflict()
    {
        var run = CreateRun();

        var act = () => run.RecordEffectiveParameters("""{}""");

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void Complete_from_running_sets_completed_and_full_progress()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.Complete(Now.AddMinutes(3));

        run.Status.Should().Be(JobStatus.Completed);
        run.Stage.Should().Be(RunStage.Completed);
        run.Progress.Should().Be(100);
        run.CompletedAt.Should().Be(Now.AddMinutes(3));
    }

    [Fact]
    public void Fail_from_running_records_category_and_stage()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.Fail(ErrorCategory.GenerationFailure, "boom", Now.AddMinutes(1));

        run.Status.Should().Be(JobStatus.Failed);
        run.Stage.Should().Be(RunStage.Failed);
        run.FailureCategory.Should().Be(ErrorCategory.GenerationFailure);
        run.FailureMessage.Should().Be("boom");
    }

    [Fact]
    public void Fail_from_completed_throws_conflict()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.Complete(Now);

        var act = () => run.Fail(ErrorCategory.GenerationFailure, "boom", Now);

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void Cancel_before_claim_succeeds()
    {
        var run = CreateRun();

        run.Cancel(Now.AddMinutes(1));

        run.Status.Should().Be(JobStatus.Cancelled);
        run.Stage.Should().Be(RunStage.Cancelled);
        run.CompletedAt.Should().Be(Now.AddMinutes(1));
    }

    [Fact]
    public void Cancel_while_preparing_succeeds()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.Cancel(Now.AddMinutes(1));

        run.Status.Should().Be(JobStatus.Cancelled);
        run.Stage.Should().Be(RunStage.Cancelled);
    }

    [Fact]
    public void Cancel_once_generation_has_started_throws_conflict()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.UpdateProgress(RunStage.Generating, 10, Now.AddMinutes(1));

        var act = () => run.Cancel(Now.AddMinutes(2));

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void RequestCancellation_while_queued_transitions_immediately_to_cancelled()
    {
        var run = CreateRun();

        run.RequestCancellation(Now.AddMinutes(1));

        run.Status.Should().Be(JobStatus.Cancelled);
        run.Stage.Should().Be(RunStage.Cancelled);
        run.CancellationRequested.Should().BeFalse(); // No flag needed - nothing was in flight to race.
    }

    [Fact]
    public void RequestCancellation_while_preparing_only_sets_the_flag_and_leaves_status_running()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.RequestCancellation(Now.AddMinutes(1));

        run.CancellationRequested.Should().BeTrue();
        run.Status.Should().Be(JobStatus.Running); // The worker itself performs the actual transition.
        run.Stage.Should().Be(RunStage.Preparing);
    }

    [Fact]
    public void RequestCancellation_while_retrieving_or_buildingContext_only_sets_the_flag()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.UpdateProgress(RunStage.BuildingContext, 40, Now.AddMinutes(1));

        run.RequestCancellation(Now.AddMinutes(2));

        run.CancellationRequested.Should().BeTrue();
        run.Status.Should().Be(JobStatus.Running);
    }

    [Fact]
    public void RequestCancellation_once_generation_has_started_throws_conflict()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.UpdateProgress(RunStage.Generating, 60, Now.AddMinutes(1));

        var act = () => run.RequestCancellation(Now.AddMinutes(2));

        act.Should().Throw<JobStateConflictException>();
        run.CancellationRequested.Should().BeFalse();
    }

    [Fact]
    public void RequestCancellation_on_a_terminal_run_throws_conflict()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.Complete(Now.AddMinutes(1));

        var act = () => run.RequestCancellation(Now.AddMinutes(2));

        act.Should().Throw<JobStateConflictException>();
    }

    [Fact]
    public void ReleaseExpiredLease_from_running_returns_to_queued()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));

        run.ReleaseExpiredLease(Now.AddMinutes(10));

        run.Status.Should().Be(JobStatus.Queued);
        run.Stage.Should().Be(RunStage.Queued);
        run.WorkerId.Should().BeNull();
        run.LeaseExpiresAt.Should().BeNull();
    }

    [Fact]
    public void RequeueForRetry_from_failed_resets_to_queued_with_new_outbox_id()
    {
        var run = CreateRun();
        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.Fail(ErrorCategory.GenerationFailure, "boom", Now.AddMinutes(1));
        var newOutboxId = Guid.NewGuid();

        run.RequeueForRetry(newOutboxId, Now.AddMinutes(2));

        run.Status.Should().Be(JobStatus.Queued);
        run.Stage.Should().Be(RunStage.Queued);
        run.Progress.Should().Be(0);
        run.FailureCategory.Should().BeNull();
        run.OutboxMessageId.Should().Be(newOutboxId);
    }

    [Fact]
    public void RequeueForRetry_when_not_failed_throws_conflict()
    {
        var run = CreateRun();

        var act = () => run.RequeueForRetry(Guid.NewGuid(), Now);

        act.Should().Throw<JobStateConflictException>();
    }

    /// <summary>TASK-SIG-02/03: EventSequence is the per-entity monotonic counter carried on every realtime envelope (Shared.Realtime.SignalREvent.Sequence) so clients can ignore stale/out-of-order events.</summary>
    [Fact]
    public void EventSequence_increments_on_every_mutating_transition_and_never_resets()
    {
        var run = CreateRun();
        run.EventSequence.Should().Be(0);

        run.Claim("worker-1", Now, TimeSpan.FromMinutes(5));
        run.EventSequence.Should().Be(1);

        run.UpdateProgress(RunStage.Retrieving, 20, Now.AddMinutes(1));
        run.EventSequence.Should().Be(2);

        run.Cancel(Now.AddMinutes(2));
        run.EventSequence.Should().Be(3);
    }
}
