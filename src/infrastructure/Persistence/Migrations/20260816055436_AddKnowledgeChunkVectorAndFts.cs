using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKnowledgeChunkVectorAndFts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Vector>(
                name: "embedding",
                schema: "knowledge",
                table: "knowledge_chunks",
                type: "vector(1536)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "embedding_deployment",
                schema: "knowledge",
                table: "knowledge_chunks",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "embedding_dimensions",
                schema: "knowledge",
                table: "knowledge_chunks",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "search_vector",
                schema: "knowledge",
                table: "knowledge_chunks",
                type: "tsvector",
                nullable: true,
                computedColumnSql: "setweight(to_tsvector('english', coalesce(\"HeadingPath\", '')), 'A') || setweight(to_tsvector('english', coalesce(\"Content\", '')), 'B')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_ApplicationId_FileId_IsActive",
                schema: "knowledge",
                table: "knowledge_chunks",
                columns: new[] { "ApplicationId", "FileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_knowledge_chunks_search_vector",
                schema: "knowledge",
                table: "knowledge_chunks",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_ApplicationId_FileId_IsActive",
                schema: "knowledge",
                table: "knowledge_chunks");

            migrationBuilder.DropIndex(
                name: "IX_knowledge_chunks_search_vector",
                schema: "knowledge",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "search_vector",
                schema: "knowledge",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "embedding",
                schema: "knowledge",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "embedding_deployment",
                schema: "knowledge",
                table: "knowledge_chunks");

            migrationBuilder.DropColumn(
                name: "embedding_dimensions",
                schema: "knowledge",
                table: "knowledge_chunks");
        }
    }
}
