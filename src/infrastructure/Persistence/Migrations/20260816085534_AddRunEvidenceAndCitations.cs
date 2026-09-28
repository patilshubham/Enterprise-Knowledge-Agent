using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRunEvidenceAndCitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "outputs");

            migrationBuilder.CreateTable(
                name: "citations",
                schema: "outputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    GeneratedOutputId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunEvidenceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_citations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "run_evidence",
                schema: "execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KnowledgeChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNameSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HeadingPathSnapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PassageSnapshot = table.Column<string>(type: "text", nullable: false),
                    CharacterStartSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CharacterEndSnapshot = table.Column<int>(type: "integer", nullable: false),
                    ProcessedAtSnapshot = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_run_evidence", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_citations_GeneratedOutputId",
                schema: "outputs",
                table: "citations",
                column: "GeneratedOutputId");

            migrationBuilder.CreateIndex(
                name: "IX_citations_RunEvidenceId",
                schema: "outputs",
                table: "citations",
                column: "RunEvidenceId");

            migrationBuilder.CreateIndex(
                name: "IX_run_evidence_AgentRunId_SourceId",
                schema: "execution",
                table: "run_evidence",
                columns: new[] { "AgentRunId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_run_evidence_ApplicationId_AgentRunId",
                schema: "execution",
                table: "run_evidence",
                columns: new[] { "ApplicationId", "AgentRunId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "citations",
                schema: "outputs");

            migrationBuilder.DropTable(
                name: "run_evidence",
                schema: "execution");
        }
    }
}
