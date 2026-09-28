using System.Data;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseKnowledgeAgent.Api.HealthChecks;

/// <summary>
/// TASK-OPS-01 (ASDD §24 readiness): Postgres connectivity + the pgvector
/// extension actually being installed (not just declared in the EF model -
/// `HasPostgresExtension` only affects migrations, it does not guarantee
/// the extension exists in a database that was provisioned out-of-band).
/// </summary>
public sealed class PostgresHealthCheck(EnterpriseKnowledgeAgentDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Unhealthy("Cannot connect to Postgres.");
            }

            var connection = dbContext.Database.GetDbConnection();
            var openedHere = connection.State != ConnectionState.Open;
            if (openedHere)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1 FROM pg_extension WHERE extname = 'vector'";
                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result is not null
                    ? HealthCheckResult.Healthy("Postgres reachable; pgvector extension installed.")
                    : HealthCheckResult.Unhealthy("pgvector extension is not installed.");
            }
            finally
            {
                if (openedHere)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Postgres readiness check failed.", ex);
        }
    }
}
