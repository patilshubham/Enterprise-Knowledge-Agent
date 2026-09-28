using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialSchemas : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // TASK-PG-01: pgvector extension + the seven Postgres schemas
        // (ASDD §10). No "identity" schema - user/role data is owned
        // entirely by Keycloak (decision D20).
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");

        foreach (var schema in EnterpriseKnowledgeAgentDbContext.Schemas)
        {
            migrationBuilder.Sql($"CREATE SCHEMA IF NOT EXISTS \"{schema}\";");
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        foreach (var schema in EnterpriseKnowledgeAgentDbContext.Schemas.Reverse())
        {
            migrationBuilder.Sql($"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE;");
        }

        migrationBuilder.Sql("DROP EXTENSION IF EXISTS vector;");
    }
}
