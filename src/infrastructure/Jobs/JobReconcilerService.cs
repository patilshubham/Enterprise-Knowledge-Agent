using System.Text.Json;
using EnterpriseKnowledgeAgent.Application.Jobs;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Shared.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Infrastructure.Jobs;

/// <summary>
/// TASK-JOB-05: safety-net reconciler (Decision D1) that finds Queued
/// processing jobs/agent runs older than a staleness threshold whose own
/// outbox row is still undelivered - a sign the primary
/// <see cref="OutboxPublisherService"/> never actually reached that row
/// (e.g. it was down, crashed mid-batch, or is badly backlogged) - and
/// directly retries publishing just that row.
/// </summary>
public sealed class JobReconcilerService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<JobReconcilerService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.ReconcilerPollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReconcileOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.ReconcilerPassFailed(ex);
            }

            try
            {
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }

    private async Task ReconcileOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var processingJobRepository = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
        var agentRunRepository = scope.ServiceProvider.GetRequiredService<IAgentRunRepository>();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var documentProcessingProducer = scope.ServiceProvider.GetRequiredService<IDocumentProcessingProducer>();
        var agentExecutionProducer = scope.ServiceProvider.GetRequiredService<IAgentExecutionProducer>();

        var threshold = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(options.Value.ReconcilerStalenessThresholdSeconds);
        var republished = false;

        var staleJobs = await processingJobRepository.ListStaleUndeliveredAsync(threshold, cancellationToken);
        foreach (var job in staleJobs)
        {
            var outbox = await outboxRepository.GetAsync(job.OutboxMessageId!.Value, cancellationToken);
            if (outbox is null)
            {
                continue;
            }

            republished |= await TryRepublishAsync(outbox, () =>
                documentProcessingProducer.SendAsync(Deserialize<DocumentProcessingMessage>(outbox.PayloadJson), cancellationToken));
        }

        var staleRuns = await agentRunRepository.ListStaleUndeliveredAsync(threshold, cancellationToken);
        foreach (var run in staleRuns)
        {
            var outbox = await outboxRepository.GetAsync(run.OutboxMessageId!.Value, cancellationToken);
            if (outbox is null)
            {
                continue;
            }

            republished |= await TryRepublishAsync(outbox, () =>
                agentExecutionProducer.SendAsync(Deserialize<AgentExecutionMessage>(outbox.PayloadJson), cancellationToken));
        }

        if (republished)
        {
            await outboxRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<bool> TryRepublishAsync(OutboxMessage outbox, Func<Task> send)
    {
        try
        {
            await send();
            outbox.MarkDelivered(DateTimeOffset.UtcNow);
            logger.ReconcilerRepublished(outbox.Id);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.ReconcilerRepublishFailed(ex, outbox.Id);
            outbox.RecordDeliveryFailure(ex.Message, DateTimeOffset.UtcNow);
            return true;
        }
    }

    private static TMessage Deserialize<TMessage>(string payloadJson) =>
        JsonSerializer.Deserialize<TMessage>(payloadJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Outbox payload deserialized to null for '{typeof(TMessage).Name}'.");
}
