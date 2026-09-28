using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeFileReplacementAndDeletionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                schema: "knowledge",
                table: "knowledge_files",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementContentHash",
                schema: "knowledge",
                table: "knowledge_files",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementEncoding",
                schema: "knowledge",
                table: "knowledge_files",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ReplacementFileSize",
                schema: "knowledge",
                table: "knowledge_files",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacementMimeType",
                schema: "knowledge",
                table: "knowledge_files",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletedAt",
                schema: "knowledge",
                table: "knowledge_files");

            migrationBuilder.DropColumn(
                name: "ReplacementContentHash",
                schema: "knowledge",
                table: "knowledge_files");

            migrationBuilder.DropColumn(
                name: "ReplacementEncoding",
                schema: "knowledge",
                table: "knowledge_files");

            migrationBuilder.DropColumn(
                name: "ReplacementFileSize",
                schema: "knowledge",
                table: "knowledge_files");

            migrationBuilder.DropColumn(
                name: "ReplacementMimeType",
                schema: "knowledge",
                table: "knowledge_files");
        }
    }
}
