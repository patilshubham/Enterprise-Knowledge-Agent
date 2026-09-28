using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Repositories;
using FluentAssertions;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-JOB-04: transactional outbox persistence tests against a real Postgres container.</summary>
public sealed class OutboxRepositoryTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task AddAsync_then_GetAsync_round_trips_all_fields()
    {
        var message = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, """{"fileId":"abc"}""", DateTimeOffset.UtcNow);

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new OutboxRepository(writeContext);
            await repository.AddAsync(message);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var loaded = await new OutboxRepository(readContext).GetAsync(message.Id);

        loaded.Should().NotBeNull();
        loaded!.MessageType.Should().Be(OutboxMessageType.DocumentProcessing);
        loaded.PayloadJson.Should().Be("""{"fileId":"abc"}""");
        loaded.Delivered.Should().BeFalse();
    }

    [Fact]
    public async Task ListUndeliveredAsync_excludes_delivered_rows_and_orders_by_created_at()
    {
        var now = DateTimeOffset.UtcNow;
        var older = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, "{}", now.AddMinutes(-10));
        var newer = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, "{}", now.AddMinutes(-5));
        var delivered = OutboxMessage.Create(OutboxMessageType.DocumentProcessing, "{}", now.AddMinutes(-20));
        delivered.MarkDelivered(now.AddMinutes(-19));

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new OutboxRepository(writeContext);
            // Insert out of chronological order to prove the query orders by CreatedAt, not insertion order.
            await repository.AddAsync(newer);
            await repository.AddAsync(delivered);
            await repository.AddAsync(older);
            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var undelivered = await new OutboxRepository(readContext).ListUndeliveredAsync(batchSize: 10);

        var ids = undelivered.Select(m => m.Id).ToList();
        ids.Should().NotContain(delivered.Id);
        ids.IndexOf(older.Id).Should().BeLessThan(ids.IndexOf(newer.Id));
    }

    [Fact]
    public async Task ListUndeliveredAsync_respects_batch_size()
    {
        var now = DateTimeOffset.UtcNow;
        var messages = Enumerable.Range(0, 5)
            .Select(i => OutboxMessage.Create(OutboxMessageType.Deletion, "{}", now.AddSeconds(i)))
            .ToList();

        await using (var writeContext = fixture.CreateDbContext())
        {
            var repository = new OutboxRepository(writeContext);
            foreach (var message in messages)
            {
                await repository.AddAsync(message);
            }

            await repository.SaveChangesAsync();
        }

        await using var readContext = fixture.CreateDbContext();
        var page = await new OutboxRepository(readContext).ListUndeliveredAsync(batchSize: 2);

        page.Should().HaveCountLessThanOrEqualTo(2);
    }
}
