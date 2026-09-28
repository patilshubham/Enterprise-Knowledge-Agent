using EnterpriseKnowledgeAgent.Api.HealthChecks;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>TASK-OPS-01: readiness up/down for Postgres + pgvector against a real Postgres container.</summary>
public sealed class PostgresHealthCheckTests(PostgresDatabaseFixture fixture) : IClassFixture<PostgresDatabaseFixture>
{
    [Fact]
    public async Task CheckHealthAsync_returns_healthy_when_postgres_is_reachable_and_pgvector_is_installed()
    {
        await using var dbContext = fixture.CreateDbContext();
        var check = new PostgresHealthCheck(dbContext);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_returns_unhealthy_when_postgres_is_unreachable()
    {
        // Deliberately unreachable: a valid connection-string shape pointing
        // at a port nothing listens on, with a short timeout so the test
        // fails fast instead of hanging on Npgsql's default connect timeout.
        var options = new DbContextOptionsBuilder<EnterpriseKnowledgeAgentDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=eka_test;Username=eka_test;Password=eka_test;Timeout=1;Command Timeout=1",
                npgsql => npgsql.UseVector())
            .Options;
        await using var dbContext = new EnterpriseKnowledgeAgentDbContext(options);
        var check = new PostgresHealthCheck(dbContext);

        var result = await check.CheckHealthAsync(new HealthCheckContext());

        result.Status.Should().Be(HealthStatus.Unhealthy);
    }
}
