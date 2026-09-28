using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Application.Processing;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace EnterpriseKnowledgeAgent.DocumentWorker;

/// <summary>
/// TASK-PRC-01: hosts the document-processing consumer loop. Per-poll work
/// runs in its own DI scope (scoped <c>DbContext</c>/repositories) while the
/// worker itself is a singleton-lifetime <see cref="BackgroundService"/>
/// (mirrors <c>OutboxPublisherService</c>/<c>LeaseReaperService</c>). Claim
/// and heartbeat close over the job looked up by the message's
/// <see cref="DocumentProcessingMessage.KnowledgeFileId"/> (there is no
/// job id on the message itself - TASK-KNW-04's <c>GetLatestByFileAsync</c>
/// resolves it); the actual multi-stage pipeline is delegated to
/// <see cref="DocumentProcessingOrchestrator"/> (Application layer, so it
/// stays testable without AWS/EF).
/// </summary>
public sealed partial class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<SqsOptions> sqsOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    private readonly string workerId = $"document-worker-{Environment.MachineName}-{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var leaseDuration = TimeSpan.FromSeconds(Math.Max(1, sqsOptions.Value.DefaultVisibilityTimeoutSeconds));
        var consumerOptions = new QueueConsumerOptions
        {
            VisibilityTimeout = leaseDuration,
            HeartbeatInterval = TimeSpan.FromSeconds(Math.Max(5, sqsOptions.Value.DefaultVisibilityTimeoutSeconds / 3)),
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnceAsync(leaseDuration, consumerOptions, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogPollFailed(ex);
            }

            try
            {
                await PollDeletionsOnceAsync(consumerOptions, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogDeletionPollFailed(ex);
            }

            try
            {
                await Task.Delay(PollDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task PollOnceAsync(TimeSpan leaseDuration, QueueConsumerOptions consumerOptions, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IMessageQueue>();
        var jobRepository = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
        var heartbeatRepository = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<DocumentProcessingOrchestrator>();
        var consumerLogger = scope.ServiceProvider.GetRequiredService<ILogger<IdempotentQueueConsumer<DocumentProcessingMessage>>>();

        await heartbeatRepository.UpsertAsync(workerId, "document-worker", DateTimeOffset.UtcNow, stoppingToken);

        var consumer = new IdempotentQueueConsumer<DocumentProcessingMessage>(
            queue, sqsOptions.Value.DocumentProcessingQueue, consumerOptions, consumerLogger);

        await consumer.ProcessNextBatchAsync(
            tryClaimAsync: async (message, ct) =>
            {
                var job = await jobRepository.GetLatestByFileAsync(message.KnowledgeFileId, ct);
                return job is not null
                    && await jobRepository.TryClaimAsync(job.Id, workerId, DateTimeOffset.UtcNow, leaseDuration, ct);
            },
            processAsync: (message, ct) => ProcessWithCorrelationAsync(message.CorrelationId, () => orchestrator.ProcessAsync(message, workerId, ct)),
            heartbeatAsync: async (message, ct) =>
            {
                var job = await jobRepository.GetLatestByFileAsync(message.KnowledgeFileId, ct);
                if (job is not null)
                {
                    await jobRepository.HeartbeatAsync(job.Id, workerId, DateTimeOffset.UtcNow, leaseDuration, ct);
                }
            },
            stoppingToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Document worker poll failed; will retry on next interval.")]
    partial void LogPollFailed(Exception exception);

    /// <summary>
    /// TASK-REP-03: polls the deletion-jobs queue. There is no per-message
    /// job row to atomically claim (unlike document processing) - claiming
    /// always succeeds, and safety under duplicate/retried delivery instead
    /// comes from <see cref="DeletionOrchestrator"/>'s own idempotent checks
    /// (already-<c>Deleted</c> short-circuit, idempotent storage/chunk
    /// deletes). Reuses the same <see cref="IdempotentQueueConsumer{TMessage}"/>
    /// infrastructure purely for its long-poll/visibility-extension/delete-
    /// on-success behavior.
    /// </summary>
    private async Task PollDeletionsOnceAsync(QueueConsumerOptions consumerOptions, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var queue = scope.ServiceProvider.GetRequiredService<IMessageQueue>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<DeletionOrchestrator>();
        var consumerLogger = scope.ServiceProvider.GetRequiredService<ILogger<IdempotentQueueConsumer<DeletionMessage>>>();

        var consumer = new IdempotentQueueConsumer<DeletionMessage>(
            queue, sqsOptions.Value.DeletionJobsQueue, consumerOptions, consumerLogger);

        await consumer.ProcessNextBatchAsync(
            tryClaimAsync: (_, _) => Task.FromResult(true),
            processAsync: (message, ct) => ProcessWithCorrelationAsync(message.CorrelationId, () => orchestrator.ProcessAsync(message, ct)),
            heartbeatAsync: (_, _) => Task.CompletedTask,
            stoppingToken);
    }

    /// <summary>
    /// TASK-OBS-01 (ASDD §23.1): publishes the queue message's correlation id
    /// as the ambient <see cref="CorrelationContext"/> value and a Serilog
    /// <see cref="LogContext"/> property for the lifetime of processing that
    /// single message, so every log line - and any in-process/relayed
    /// SignalR publish - carries the same id as the API request that
    /// originally enqueued it.
    /// </summary>
    private static async Task ProcessWithCorrelationAsync(CorrelationId correlationId, Func<Task> process)
    {
        CorrelationContext.CorrelationId = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            await process();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Deletion queue poll failed; will retry on next interval.")]
    partial void LogDeletionPollFailed(Exception exception);
}
