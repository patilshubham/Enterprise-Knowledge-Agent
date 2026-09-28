using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "agents");

            migrationBuilder.CreateTable(
                name: "agents",
                schema: "agents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Prompt = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KnowledgeScopeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SelectedFileIdsJson = table.Column<string>(type: "text", nullable: true),
                    GenerationDeployment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    MaximumOutputTokens = table.Column<int>(type: "integer", nullable: false),
                    TopP = table.Column<double>(type: "double precision", nullable: true),
                    RetrievalMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RetrievalCount = table.Column<int>(type: "integer", nullable: false),
                    MinimumSimilarity = table.Column<double>(type: "double precision", nullable: false),
                    MaximumEvidenceTokens = table.Column<int>(type: "integer", nullable: false),
                    IncludeNeighbors = table.Column<bool>(type: "boolean", nullable: false),
                    StrictGrounding = table.Column<bool>(type: "boolean", nullable: false),
                    CitationsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agents_ApplicationId_Status",
                schema: "agents",
                table: "agents",
                columns: new[] { "ApplicationId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agents",
                schema: "agents");
        }
    }
}
