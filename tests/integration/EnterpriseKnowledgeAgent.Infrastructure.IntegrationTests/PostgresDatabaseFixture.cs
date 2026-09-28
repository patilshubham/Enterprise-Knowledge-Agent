using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace EnterpriseKnowledgeAgent.Infrastructure.IntegrationTests;

/// <summary>
/// Spins up a real Postgres (pgvector/pgvector:pg16, matching
/// docker-compose.yml's "postgres" service) via Testcontainers and applies
/// every EF Core migration, so integration tests exercise the exact schema
/// production uses - not an in-memory/SQLite substitute.
/// </summary>
public sealed class PostgresDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("eka_test")
        .WithUsername("eka_test")
        .WithPassword("eka_test")
        .Build();

    public async Task InitializeAsync()
    {
        await container.StartAsync();

        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public EnterpriseKnowledgeAgentDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EnterpriseKnowledgeAgentDbContext>()
            .UseNpgsql(container.GetConnectionString(), npgsql => npgsql.UseVector())
            .Options;

        return new EnterpriseKnowledgeAgentDbContext(options);
    }

    public async Task DisposeAsync() => await container.DisposeAsync();
}
