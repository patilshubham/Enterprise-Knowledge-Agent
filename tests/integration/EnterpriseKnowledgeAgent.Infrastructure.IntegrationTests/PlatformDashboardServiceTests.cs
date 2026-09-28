using EnterpriseKnowledgeAgent.Application.AzureOpenAI;
using EnterpriseKnowledgeAgent.Application.Dashboards;
using EnterpriseKnowledgeAgent.Application.Messaging;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using EnterpriseKnowledgeAgent.Shared.Applications;
using EnterpriseKnowledgeAgent.Shared.Jobs;
using EnterpriseKnowledgeAgent.Shared.Knowledge;
using FluentAssertions;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-PDSH-01: end-to-end aggregation tests for <see cref="PlatformDashboardService"/>
/// against a real Postgres container. <see cref="IMessageQueue"/> and
/// <see cref="IAoaiHealthService"/> are faked - no live LocalStack/Azure
/// OpenAI dependency is available here (mirrors
/// <see cref="RetrievalServiceIntegrationTests"/>'s convention of faking
/// only the external-network dependency); every repository is real.
/// </summary>
public sealed class PlatformDashboardServiceTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    private sealed class FakeMessageQueue : IMessageQueue
    {
        public Dictionary<string, QueueDepth> Depths { get; } = [];

        public Task<string> SendAsync<TMessage>(string queueName, TMessage message, CancellationToken cancellationToken = default)
            where TMessage : Shared.Messaging.IQueueMessage =>
            throw new NotSupportedException("Not exercised by dashboard tests.");

        public Task<IReadOnlyList<QueueMessageEnvelope<TMessage>>> ReceiveAsync<TMessage>(
            string queueName, int maxMessages, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default)
            where TMessage : Shared.Messaging.IQueueMessage =>
            throw new NotSupportedException("Not exercised by dashboard tests.");

        public Task DeleteAsync(string queueName, string receiptHandle, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by dashboard tests.");

        public Task ExtendVisibilityAsync(string queueName, string receiptHandle, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not exercised by dashboard tests.");

        public Task<QueueDepth> GetApproximateDepthAsync(string queueName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Depths.GetValueOrDefault(queueName, new QueueDepth(0, 0, 0)));

        public Task<QueueHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new QueueHealth(true, null));
    }

    private sealed class FakeAoaiHealthService(bool reachable) : IAoaiHealthService
    {
        public Task<IReadOnlyList<DeploymentHealthStatus>> CheckAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeploymentHealthStatus>>(
                [new DeploymentHealthStatus("gpt-4o", DeploymentPurpose.Chat, reachable, DateTimeOffset.UtcNow)]);
    }

    private static PlatformDashboardService CreateService(Persistence.EnterpriseKnowledgeAgentDbContext dbContext, FakeMessageQueue queue, bool aoaiReachable = true) =>
        new(
            new ApplicationRepository(dbContext),
            new KnowledgeFileRepository(dbContext),
            new ProcessingJobRepository(dbContext),
            new AgentRunRepository(dbContext),
            new WorkerHeartbeatRepository(dbContext),
            queue,
            new FakeAoaiHealthService(aoaiReachable),
            TimeProvider.System);

    [Fact]
    public async Task GetAsync_aggregates_counts_across_repositories()
    {
        var applicationId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var writeContext = fixture.CreateDbContext())
        {
            var app = ApplicationEntity.Create($"Dashboard-{Guid.NewGuid()}", null, "sub-1", now);
            await new ApplicationRepository(writeContext).AddAsync(app);

            var file = Domain.Knowledge.KnowledgeFile.Create(applicationId, "notes.txt", $"hash-{Guid.NewGuid()}", 10, "text/plain", "utf-8", "sub-1", now);
            await new KnowledgeFileRepository(writeContext).AddAsync(file);

            var job = Domain.Jobs.ProcessingJob.Create(applicationId, file.Id, Shared.Correlation.CorrelationId.New(), now);
            await new ProcessingJobRepository(writeContext).AddAsync(job);

            await new WorkerHeartbeatRepository(writeContext).UpsertAsync("worker-1", "document-worker", now.AddMinutes(-10), CancellationToken.None);

            await writeContext.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var queue = new FakeMessageQueue();
        queue.Depths["document-processing"] = new QueueDepth(3, 1, 2);
        var service = CreateService(readContext, queue, aoaiReachable: false);

        var dashboard = await service.GetAsync(["document-processing", "agent-execution"]);

        dashboard.Applications.Active.Should().BeGreaterThanOrEqualTo(1);
        dashboard.KnowledgeFiles.Queued.Should().BeGreaterThanOrEqualTo(1);
        dashboard.ProcessingJobs.Queued.Should().BeGreaterThanOrEqualTo(1);
        dashboard.Workers.Should().Contain(w => w.WorkerId == "worker-1" && w.IsStale);
        dashboard.AoaiAvailable.Should().BeFalse();
        dashboard.Queues.Should().ContainSingle(q => q.QueueName == "document-processing" && q.ApproximateMessageCount == 3 && q.DeadLetterQueueMessageCount == 2);
        dashboard.AttentionItems.Should().Contain(i => i.Kind == "WorkersStale");
        dashboard.AttentionItems.Should().Contain(i => i.Kind == "AoaiUnavailable");
        dashboard.AttentionItems.Should().Contain(i => i.Kind == "DeadLetterMessages");
    }

    [Fact]
    public async Task GetAsync_reports_no_attention_items_when_everything_is_healthy()
    {
        await using var dbContext = fixture.CreateDbContext();
        var queue = new FakeMessageQueue();
        var service = CreateService(dbContext, queue, aoaiReachable: true);

        var dashboard = await service.GetAsync(["document-processing", "agent-execution"]);

        dashboard.AttentionItems.Should().NotContain(i => i.Kind == "AoaiUnavailable");
        dashboard.AttentionItems.Should().NotContain(i => i.Kind == "DeadLetterMessages");
        dashboard.AoaiAvailable.Should().BeTrue();
    }
}
