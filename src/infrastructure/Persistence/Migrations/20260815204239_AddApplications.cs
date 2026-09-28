using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddApplications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "platform");

        migrationBuilder.CreateTable(
            name: "applications",
            schema: "platform",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                AllowViewerExecution = table.Column<bool>(type: "boolean", nullable: false),
                CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ArchivedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                NormalizedName = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(\"Name\")", stored: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_applications", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_applications_NormalizedName",
            schema: "platform",
            table: "applications",
            column: "NormalizedName",
            unique: true,
            filter: "\"Status\" <> 'Deleted'");

        migrationBuilder.CreateIndex(
            name: "IX_applications_Status",
            schema: "platform",
            table: "applications",
            column: "Status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "applications",
            schema: "platform");
    }
}
