using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Realtime;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using EnterpriseKnowledgeAgent.Shared.Realtime;

namespace EnterpriseKnowledgeAgent.Application.Jobs;

/// <summary>
/// TASK-JOB-02: creates <see cref="ProcessingJob"/>/<see cref="AgentRun"/>
/// records together with their outbox message intent in one transaction
/// (Decision D1, ASDD §12.3) - the SQS send itself happens later, out of
/// this atomic unit, via the outbox publisher (TASK-JOB-04). Also used by
/// TASK-JOB-06's manual retry endpoint to re-queue a Failed job/run with a
/// freshly-created outbox row.
/// </summary>
public sealed class JobCreationService(
    IProcessingJobRepository processingJobRepository,
    IAgentRunRepository agentRunRepository,
    IOutboxRepository outboxRepository,
    IRealtimeEventPublisher realtimePublisher,
    TimeProvider timeProvider)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    public async Task<ProcessingJob> CreateProcessingJobAsync(Guid applicationId, Guid fileId, CorrelationId correlationId, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var job = ProcessingJob.Create(applicationId, fileId, correlationId, now);
        await processingJobRepository.AddAsync(job, cancellationToken);

        var outbox = CreateDocumentProcessingOutboxRow(applicationId, fileId, correlationId, attemptNumber: 1, now);
        await outboxRepository.AddAsync(outbox, cancellationToken);
        job.AttachOutboxMessage(outbox.Id);

        // Single SaveChanges: both the job and outbox rows are tracked on the
        // same DbContext, so EF commits them in one database transaction.
        await processingJobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileQueuedAsync(job, cancellationToken);
        return job;
    }

    public async Task<AgentRun> CreateAgentRunAsync(CreateAgentRunRequest request, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        // TASK-OBS-01: propagate the current HTTP request's correlation id
        // (set by CorrelationIdMiddleware) instead of generating an
        // unrelated fresh one, so the run can be traced end-to-end.
        var correlationId = CorrelationContext.CorrelationIdOrNew();

        var run = AgentRun.Create(
            request.ApplicationId,
            request.AgentId,
            request.RequestedBy,
            request.UserRequest,
            request.IdempotencyKey,
            request.OutputTitle,
            request.AgentNameSnapshot,
            request.AgentPromptSnapshot,
            request.KnowledgeScopeSnapshotJson,
            request.DeploymentSnapshot,
            request.RequestedParametersJson,
            request.EffectiveParametersJson,
            request.RetrievalSettingsJson,
            correlationId,
            now);
        await agentRunRepository.AddAsync(run, cancellationToken);

        var outbox = CreateAgentExecutionOutboxRow(request.ApplicationId, request.AgentId, run.Id, correlationId, attemptNumber: 1, now);
        await outboxRepository.AddAsync(outbox, cancellationToken);
        run.AttachOutboxMessage(outbox.Id);

        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunQueuedAsync(run, cancellationToken);
        return run;
    }

    /// <summary>TASK-JOB-06: re-queues a Failed processing job with a new outbox row (next attempt number).</summary>
    public async Task<ProcessingJob> RetryProcessingJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var job = await processingJobRepository.GetAsync(jobId, cancellationToken)
            ?? throw new InvalidOperationException($"Processing job '{jobId}' was not found.");

        var now = timeProvider.GetUtcNow();
        var outbox = CreateDocumentProcessingOutboxRow(job.ApplicationId, job.FileId, job.CorrelationId, job.AttemptNumber + 1, now);
        await outboxRepository.AddAsync(outbox, cancellationToken);

        job.RequeueForRetry(outbox.Id, now);
        await processingJobRepository.SaveChangesAsync(cancellationToken);
        await PublishFileQueuedAsync(job, cancellationToken);
        return job;
    }

    /// <summary>TASK-JOB-06: re-queues a Failed agent run with a new outbox row (next attempt number).</summary>
    public async Task<AgentRun> RetryAgentRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await agentRunRepository.GetAsync(runId, cancellationToken)
            ?? throw new InvalidOperationException($"Agent run '{runId}' was not found.");

        var now = timeProvider.GetUtcNow();
        var outbox = CreateAgentExecutionOutboxRow(run.ApplicationId, run.AgentId, run.Id, run.CorrelationId, run.AttemptNumber + 1, now);
        await outboxRepository.AddAsync(outbox, cancellationToken);

        run.RequeueForRetry(outbox.Id, now);
        await agentRunRepository.SaveChangesAsync(cancellationToken);
        await PublishRunQueuedAsync(run, cancellationToken);
        return run;
    }

    private Task PublishFileQueuedAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<FileQueuedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.FileQueued, job.ApplicationId, job.FileId,
            timeProvider.GetUtcNow(), job.EventSequence, new FileQueuedPayload(job.FileId));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.FileQueued, [RealtimeGroups.Application(job.ApplicationId)], envelope, cancellationToken);
    }

    private Task PublishRunQueuedAsync(AgentRun run, CancellationToken cancellationToken)
    {
        var envelope = new SignalREvent<RunQueuedPayload>(
            Guid.NewGuid(), RealtimeEventTypes.RunQueued, run.ApplicationId, run.Id,
            timeProvider.GetUtcNow(), run.EventSequence, new RunQueuedPayload(run.Id, run.AgentId));
        return realtimePublisher.PublishAsync(
            RealtimeEventTypes.RunQueued,
            [RealtimeGroups.Run(run.Id), RealtimeGroups.Application(run.ApplicationId)],
            envelope,
            cancellationToken);
    }

    private static OutboxMessage CreateDocumentProcessingOutboxRow(Guid applicationId, Guid fileId, CorrelationId correlationId, int attemptNumber, DateTimeOffset now)
    {
        var message = new DocumentProcessingMessage(Guid.NewGuid(), applicationId, fileId, correlationId, attemptNumber);
        return OutboxMessage.Create(OutboxMessageType.DocumentProcessing, JsonSerializer.Serialize(message, SerializerOptions), now);
    }

    private static OutboxMessage CreateAgentExecutionOutboxRow(Guid applicationId, Guid agentId, Guid runId, CorrelationId correlationId, int attemptNumber, DateTimeOffset now)
    {
        var message = new AgentExecutionMessage(Guid.NewGuid(), applicationId, agentId, runId, correlationId, attemptNumber);
        return OutboxMessage.Create(OutboxMessageType.AgentExecution, JsonSerializer.Serialize(message, SerializerOptions), now);
    }
}
