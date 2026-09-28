using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "knowledge");

            migrationBuilder.CreateTable(
                name: "knowledge_chunks",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    HeadingPath = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    TokenCount = table.Column<int>(type: "integer", nullable: false),
                    CharacterStart = table.Column<int>(type: "integer", nullable: false),
                    CharacterEnd = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_chunks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "knowledge_files",
                schema: "knowledge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSize = table.Column<long>(type: "bigint", nullable: false),
                    MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Encoding = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplacementObjectKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReplacementStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_knowledge_files", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_ApplicationId_IsActive",
                schema: "knowledge",
                table: "knowledge_chunks",
                columns: new[] { "ApplicationId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_FileId_SequenceNumber",
                schema: "knowledge",
                table: "knowledge_chunks",
                columns: new[] { "FileId", "SequenceNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_files_ApplicationId_ContentHash",
                schema: "knowledge",
                table: "knowledge_files",
                columns: new[] { "ApplicationId", "ContentHash" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_files_ApplicationId_Status_IsEnabled",
                schema: "knowledge",
                table: "knowledge_files",
                columns: new[] { "ApplicationId", "Status", "IsEnabled" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "knowledge_chunks",
                schema: "knowledge");

            migrationBuilder.DropTable(
                name: "knowledge_files",
                schema: "knowledge");
        }
    }
}
