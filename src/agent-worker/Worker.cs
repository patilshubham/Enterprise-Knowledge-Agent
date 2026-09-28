using EnterpriseKnowledgeAgent.Application.Execution;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Infrastructure.Messaging;
using EnterpriseKnowledgeAgent.Shared.Correlation;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace EnterpriseKnowledgeAgent.AgentWorker;

/// <summary>
/// TASK-RUN-02: hosts the agent-run consumer loop, mirroring
/// <c>document-worker</c>'s <c>Worker</c> shape - per-poll work runs in its
/// own DI scope (scoped <c>DbContext</c>/repositories) while the worker
/// itself is a singleton-lifetime <see cref="BackgroundService"/>. Claim and
/// heartbeat operate directly on <see cref="IAgentRunRepository"/> keyed by
/// the message's own <see cref="AgentExecutionMessage.RunId"/> (unlike
/// document processing, the run id IS the queued identifier - no extra
/// by-file lookup is needed); the actual multi-stage pipeline is delegated
/// to <see cref="AgentRunOrchestrator"/> (Application layer, so it stays
/// testable without AWS/EF).
/// </summary>
public sealed partial class Worker(
    IServiceScopeFactory scopeFactory,
    IOptions<SqsOptions> sqsOptions,
    ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);

    private readonly string workerId = $"agent-worker-{Environment.MachineName}-{Guid.NewGuid():N}";

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
        var runRepository = scope.ServiceProvider.GetRequiredService<IAgentRunRepository>();
        var heartbeatRepository = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatRepository>();
        var orchestrator = scope.ServiceProvider.GetRequiredService<AgentRunOrchestrator>();
        var consumerLogger = scope.ServiceProvider.GetRequiredService<ILogger<IdempotentQueueConsumer<AgentExecutionMessage>>>();

        await heartbeatRepository.UpsertAsync(workerId, "agent-worker", DateTimeOffset.UtcNow, stoppingToken);

        var consumer = new IdempotentQueueConsumer<AgentExecutionMessage>(
            queue, sqsOptions.Value.AgentExecutionQueue, consumerOptions, consumerLogger);

        await consumer.ProcessNextBatchAsync(
            tryClaimAsync: (message, ct) =>
                runRepository.TryClaimAsync(message.RunId, workerId, DateTimeOffset.UtcNow, leaseDuration, ct),
            processAsync: (message, ct) => ProcessWithCorrelationAsync(message.CorrelationId, () => orchestrator.ProcessAsync(message, workerId, ct)),
            heartbeatAsync: (message, ct) =>
                runRepository.HeartbeatAsync(message.RunId, workerId, DateTimeOffset.UtcNow, leaseDuration, ct),
            stoppingToken);
    }

    /// <summary>
    /// TASK-OBS-01 (ASDD §23.1): publishes the queue message's correlation id
    /// as the ambient <see cref="CorrelationContext"/> value and a Serilog
    /// <see cref="LogContext"/> property for the lifetime of processing that
    /// single message, so every log line - and any in-process/relayed
    /// SignalR publish - carries the same id as the API request that
    /// originally enqueued the run.
    /// </summary>
    private static async Task ProcessWithCorrelationAsync(CorrelationId correlationId, Func<Task> process)
    {
        CorrelationContext.CorrelationId = correlationId;
        using (LogContext.PushProperty("CorrelationId", correlationId.ToString()))
        {
            await process();
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Agent worker poll failed; will retry on next interval.")]
    partial void LogPollFailed(Exception exception);
}
