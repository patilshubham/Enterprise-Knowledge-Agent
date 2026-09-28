using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` / `dotnet ef database
/// update` can construct <see cref="EnterpriseKnowledgeAgentDbContext"/>
/// without booting the full API host. Reads the same
/// <c>ConnectionStrings__Postgres</c> environment variable convention used
/// by deploy/postgres/migrate.ps1|sh and the Api project's configuration, so
/// design-time and run-time resolve the connection string identically. Falls
/// back to the local "diagnostics" profile default when unset (e.g. a bare
/// `dotnet ef migrations add` with no environment configured), since
/// `migrations add` never actually opens a connection.
/// </summary>
public sealed class EnterpriseKnowledgeAgentDbContextFactory
    : IDesignTimeDbContextFactory<EnterpriseKnowledgeAgentDbContext>
{
    private const string DefaultLocalConnectionString =
        "Host=localhost;Port=5432;Database=enterprise_knowledge_agent;Username=eka;Password=eka-local-dev-password";

    public EnterpriseKnowledgeAgentDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
            ?? DefaultLocalConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<EnterpriseKnowledgeAgentDbContext>();
        optionsBuilder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());

        return new EnterpriseKnowledgeAgentDbContext(optionsBuilder.Options);
    }
}
