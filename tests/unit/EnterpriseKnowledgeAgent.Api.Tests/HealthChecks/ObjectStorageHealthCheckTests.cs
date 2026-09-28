using EnterpriseKnowledgeAgent.Api.HealthChecks;
using EnterpriseKnowledgeAgent.Application.Storage;
using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.Tests.HealthChecks;

/// <summary>TASK-OPS-01: <see cref="ObjectStorageHealthCheck"/> maps <see cref="IObjectStorage.CheckHealthAsync"/> to a readiness result.</summary>
public sealed class ObjectStorageHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_object_storage_reports_available()
    {
        var check = new ObjectStorageHealthCheck(new FakeObjectStorage(new ObjectStorageHealth(true, null)));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_with_detail_when_object_storage_reports_unavailable()
    {
        var check = new ObjectStorageHealthCheck(new FakeObjectStorage(new ObjectStorageHealth(false, "bucket unreachable")));

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Be("bucket unreachable");
    }

    private sealed class FakeObjectStorage(ObjectStorageHealth health) : IObjectStorage
    {
        public Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Stream> GetAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ObjectStorageHealth> CheckHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(health);
    }
}
