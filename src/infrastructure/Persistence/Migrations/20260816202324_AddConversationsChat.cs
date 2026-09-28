using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationsChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "chat");

            migrationBuilder.CreateTable(
                name: "conversation_message_evidence",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    KnowledgeChunkId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeFileId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileNameSnapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HeadingPathSnapshot = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    PassageSnapshot = table.Column<string>(type: "text", nullable: false),
                    CharacterStartSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CharacterEndSnapshot = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_message_evidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    KnowledgeScopeType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    SelectedFileIdsJson = table.Column<string>(type: "text", nullable: true),
                    GenerationDeployment = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Temperature = table.Column<double>(type: "double precision", nullable: false),
                    RetrievalMode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RetrievalCount = table.Column<int>(type: "integer", nullable: false),
                    MinimumSimilarity = table.Column<double>(type: "double precision", nullable: false),
                    MaximumEvidenceTokens = table.Column<int>(type: "integer", nullable: false),
                    IncludeNeighbors = table.Column<bool>(type: "boolean", nullable: false),
                    CitationsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    SystemPrompt = table.Column<string>(type: "character varying(100000)", maxLength: 100000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "conversation_messages",
                schema: "chat",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CitedSourceIdsJson = table.Column<string>(type: "text", nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversation_messages_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalSchema: "chat",
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_conversation_message_evidence_ConversationId",
                schema: "chat",
                table: "conversation_message_evidence",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_message_evidence_ConversationMessageId_SourceId",
                schema: "chat",
                table: "conversation_message_evidence",
                columns: new[] { "ConversationMessageId", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversation_messages_ConversationId_Sequence",
                schema: "chat",
                table: "conversation_messages",
                columns: new[] { "ConversationId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_conversations_ApplicationId_IsDeleted_LastMessageAt",
                schema: "chat",
                table: "conversations",
                columns: new[] { "ApplicationId", "IsDeleted", "LastMessageAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_message_evidence",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "conversation_messages",
                schema: "chat");

            migrationBuilder.DropTable(
                name: "conversations",
                schema: "chat");
        }
    }
}
