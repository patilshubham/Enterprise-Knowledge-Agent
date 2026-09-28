using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "execution");

            migrationBuilder.EnsureSchema(
                name: "operations");

            migrationBuilder.CreateTable(
                name: "agent_runs",
                schema: "execution",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    UserRequest = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    AgentNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AgentPromptSnapshot = table.Column<string>(type: "text", nullable: false),
                    KnowledgeScopeSnapshotJson = table.Column<string>(type: "text", nullable: true),
                    DeploymentSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RequestedParametersJson = table.Column<string>(type: "text", nullable: true),
                    EffectiveParametersJson = table.Column<string>(type: "text", nullable: true),
                    RetrievalSettingsJson = table.Column<string>(type: "text", nullable: true),
                    WorkerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MessageType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Delivered = table.Column<bool>(type: "boolean", nullable: false),
                    DeliveryAttempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "processing_attempts",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessingJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_attempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "processing_jobs",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Stage = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    HeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCategory = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    FailureMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(36)", maxLength: 36, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processing_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worker_heartbeats",
                schema: "operations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkerKind = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_heartbeats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_ApplicationId_Status_CreatedAt",
                schema: "execution",
                table: "agent_runs",
                columns: new[] { "ApplicationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_Status_LeaseExpiresAt",
                schema: "execution",
                table: "agent_runs",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_outbox_messages_Delivered_CreatedAt",
                schema: "operations",
                table: "outbox_messages",
                columns: new[] { "Delivered", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_attempts_ProcessingJobId",
                schema: "operations",
                table: "processing_attempts",
                column: "ProcessingJobId");

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_ApplicationId_Status_CreatedAt",
                schema: "operations",
                table: "processing_jobs",
                columns: new[] { "ApplicationId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_processing_jobs_Status_LeaseExpiresAt",
                schema: "operations",
                table: "processing_jobs",
                columns: new[] { "Status", "LeaseExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_worker_heartbeats_LastHeartbeatAt",
                schema: "operations",
                table: "worker_heartbeats",
                column: "LastHeartbeatAt");

            migrationBuilder.CreateIndex(
                name: "IX_worker_heartbeats_WorkerId",
                schema: "operations",
                table: "worker_heartbeats",
                column: "WorkerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_runs",
                schema: "execution");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "processing_attempts",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "processing_jobs",
                schema: "operations");

            migrationBuilder.DropTable(
                name: "worker_heartbeats",
                schema: "operations");
        }
    }
}
