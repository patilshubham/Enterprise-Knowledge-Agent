using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentRunIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "execution",
                table: "agent_runs",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OutputTitle",
                schema: "execution",
                table: "agent_runs",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_AgentId_IdempotencyKey",
                schema: "execution",
                table: "agent_runs",
                columns: new[] { "AgentId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agent_runs_AgentId_IdempotencyKey",
                schema: "execution",
                table: "agent_runs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "execution",
                table: "agent_runs");

            migrationBuilder.DropColumn(
                name: "OutputTitle",
                schema: "execution",
                table: "agent_runs");
        }
    }
}
