using System.Text;
using EnterpriseKnowledgeAgent.Application.Storage;
using EnterpriseKnowledgeAgent.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// TASK-S3-01/02/04: put/get/delete/exists/health round-trip against the
/// real LocalStack S3 (see <see cref="LocalStackFixture"/> for why this uses
/// the compose stack rather than Testcontainers.LocalStack). Requires
/// `docker compose --profile diagnostics up -d localstack` to be running.
/// </summary>
public sealed class S3ObjectStorageTests
{
    private readonly S3ObjectStorage storage;

    public S3ObjectStorageTests()
    {
        var options = Options.Create(new S3Options { SourceBucket = LocalStackFixture.SourceBucket });
        storage = new S3ObjectStorage(LocalStackFixture.CreateS3Client(), options);
    }

    [Fact]
    public async Task PutAsync_then_GetAsync_round_trips_content()
    {
        var key = ObjectKeyFactory.Source(Guid.NewGuid(), Guid.NewGuid());
        var content = Encoding.UTF8.GetBytes("hello world");

        await using (var stream = new MemoryStream(content))
        {
            await storage.PutAsync(key, stream, "text/plain");
        }

        await using var downloaded = await storage.GetAsync(key);
        using var reader = new StreamReader(downloaded);
        var text = await reader.ReadToEndAsync();

        text.Should().Be("hello world");
    }

    [Fact]
    public async Task ExistsAsync_reflects_put_and_delete()
    {
        var key = ObjectKeyFactory.Source(Guid.NewGuid(), Guid.NewGuid());

        (await storage.ExistsAsync(key)).Should().BeFalse();

        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("data")))
        {
            await storage.PutAsync(key, stream, "text/plain");
        }

        (await storage.ExistsAsync(key)).Should().BeTrue();

        await storage.DeleteAsync(key);

        (await storage.ExistsAsync(key)).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_is_idempotent_for_a_key_that_never_existed()
    {
        var key = ObjectKeyFactory.Source(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await storage.DeleteAsync(key);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAsync_for_a_missing_key_throws_ObjectNotFoundException()
    {
        var key = ObjectKeyFactory.Source(Guid.NewGuid(), Guid.NewGuid());

        var act = async () => await storage.GetAsync(key);

        await act.Should().ThrowAsync<ObjectNotFoundException>();
    }

    [Fact]
    public async Task CheckHealthAsync_reports_available_when_LocalStack_is_reachable()
    {
        var health = await storage.CheckHealthAsync();

        health.IsAvailable.Should().BeTrue();
    }
}
