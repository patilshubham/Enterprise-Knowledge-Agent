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
/// TASK-JOB-04: polls <see cref="IOutboxRepository"/> for undelivered rows
/// and publishes each to its typed SQS producer (Decision D1). Runs inside
/// the API host; per-poll <see cref="IServiceScopeFactory"/> scope is used
/// because <c>DbContext</c> is scoped but this service itself is a
/// singleton-lifetime <see cref="BackgroundService"/>.
/// </summary>
public sealed class OutboxPublisherService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxOptions> options,
    ILogger<OutboxPublisherService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = null };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(Math.Max(1, options.Value.PublisherPollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.PublisherPollFailed(ex);
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

    private async Task PublishOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var documentProcessingProducer = scope.ServiceProvider.GetRequiredService<IDocumentProcessingProducer>();
        var agentExecutionProducer = scope.ServiceProvider.GetRequiredService<IAgentExecutionProducer>();
        var deletionProducer = scope.ServiceProvider.GetRequiredService<IDeletionProducer>();

        var batch = await outboxRepository.ListUndeliveredAsync(options.Value.PublisherBatchSize, cancellationToken);

        foreach (var row in batch)
        {
            var now = DateTimeOffset.UtcNow;
            try
            {
                await SendAsync(row, documentProcessingProducer, agentExecutionProducer, deletionProducer, cancellationToken);
                row.MarkDelivered(now);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.PublishFailed(ex, row.Id, row.MessageType.ToString());
                row.RecordDeliveryFailure(ex.Message, now);
            }
        }

        if (batch.Count > 0)
        {
            await outboxRepository.SaveChangesAsync(cancellationToken);
        }
    }

    private static Task SendAsync(
        OutboxMessage row,
        IDocumentProcessingProducer documentProcessingProducer,
        IAgentExecutionProducer agentExecutionProducer,
        IDeletionProducer deletionProducer,
        CancellationToken cancellationToken) => row.MessageType switch
        {
            OutboxMessageType.DocumentProcessing => documentProcessingProducer.SendAsync(
                Deserialize<DocumentProcessingMessage>(row.PayloadJson), cancellationToken),
            OutboxMessageType.AgentExecution => agentExecutionProducer.SendAsync(
                Deserialize<AgentExecutionMessage>(row.PayloadJson), cancellationToken),
            OutboxMessageType.Deletion => deletionProducer.SendAsync(
                Deserialize<DeletionMessage>(row.PayloadJson), cancellationToken),
            _ => throw new InvalidOperationException($"Unknown outbox message type '{row.MessageType}'."),
        };

    private static TMessage Deserialize<TMessage>(string payloadJson) =>
        JsonSerializer.Deserialize<TMessage>(payloadJson, SerializerOptions)
            ?? throw new InvalidOperationException($"Outbox payload deserialized to null for '{typeof(TMessage).Name}'.");
}
