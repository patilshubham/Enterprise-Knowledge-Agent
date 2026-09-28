using EnterpriseKnowledgeAgent.Domain.Audit;
using EnterpriseKnowledgeAgent.Domain.Citations;
using EnterpriseKnowledgeAgent.Domain.Conversations;
using EnterpriseKnowledgeAgent.Domain.Execution;
using EnterpriseKnowledgeAgent.Domain.Jobs;
using EnterpriseKnowledgeAgent.Domain.Knowledge;
using EnterpriseKnowledgeAgent.Domain.Operations;
using EnterpriseKnowledgeAgent.Domain.Outputs;
using EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using AgentEntity = EnterpriseKnowledgeAgent.Domain.Agents.Agent;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Enterprise Knowledge Agent Postgres database
/// (TASK-PG-01). Owns the pgvector extension and the seven Postgres schemas
/// used across the platform (ASDD §10):
///
///   platform   - applications, connections, cross-cutting configuration
///   knowledge  - knowledge bases, documents, chunks, embeddings
///   agents     - agent definitions, tools, prompts
///   execution  - agent runs, steps, traces
///   outputs    - generated artifacts/exports
///   operations - background jobs, queue processing state
///   audit      - audit log entries
///
/// There is deliberately NO "identity" schema: user/role data is owned
/// entirely by Keycloak (decision D20). Entities are added schema-by-schema
/// by the modules that own them (Modules 4, 6-13, 15-36); this context and
/// its base migration establish only the schemas + pgvector extension so
/// later modules have a place to land their `ToTable("x", "schema")` maps.
/// </summary>
public sealed class EnterpriseKnowledgeAgentDbContext(
    DbContextOptions<EnterpriseKnowledgeAgentDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Postgres schemas provisioned by the initial migration. Kept as a
    /// single source of truth so later migrations/tests can reference the
    /// same list instead of re-typing schema names.
    /// </summary>
    public static readonly IReadOnlyList<string> Schemas =
    [
        "platform",
        "knowledge",
        "agents",
        "execution",
        "outputs",
        "operations",
        "audit",
    ];

    /// <summary>TASK-APP-01: the Application aggregate (platform.applications).</summary>
    public DbSet<ApplicationEntity> Applications => Set<ApplicationEntity>();

    /// <summary>TASK-JOB-01: document-processing jobs (operations.processing_jobs).</summary>
    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    /// <summary>TASK-JOB-01: per-attempt history for a processing job (operations.processing_attempts).</summary>
    public DbSet<ProcessingAttempt> ProcessingAttempts => Set<ProcessingAttempt>();

    /// <summary>TASK-JOB-01: agent runs (execution.agent_runs).</summary>
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();

    /// <summary>TASK-JOB-04: transactional outbox rows (operations.outbox_messages).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>TASK-JOB-06: worker heartbeats (operations.worker_heartbeats).</summary>
    public DbSet<WorkerHeartbeat> WorkerHeartbeats => Set<WorkerHeartbeat>();

    /// <summary>TASK-KNW-01: knowledge files (knowledge.knowledge_files).</summary>
    public DbSet<KnowledgeFile> KnowledgeFiles => Set<KnowledgeFile>();

    /// <summary>TASK-CHK-02: knowledge chunks (knowledge.knowledge_chunks).</summary>
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    /// <summary>TASK-AGT-01: agent configurations (agents.agents).</summary>
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();

    /// <summary>TASK-CIT-01: SOURCE-NNN evidence snapshots per run (execution.run_evidence).</summary>
    public DbSet<RunEvidence> RunEvidences => Set<RunEvidence>();

    /// <summary>TASK-CIT-01: validated output-to-evidence links (outputs.citations).</summary>
    public DbSet<Citation> Citations => Set<Citation>();

    /// <summary>TASK-OUT-01: generated Markdown outputs (outputs.generated_outputs).</summary>
    public DbSet<GeneratedOutput> GeneratedOutputs => Set<GeneratedOutput>();

    /// <summary>TASK-AUD-01: the append-only audit trail (audit.audit_events).</summary>
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    /// <summary>Module CHAT: shared knowledge-base conversations (chat.conversations).</summary>
    public DbSet<Conversation> Conversations => Set<Conversation>();

    /// <summary>Module CHAT: conversation messages (chat.conversation_messages).</summary>
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();

    /// <summary>Module CHAT: SOURCE-NNN evidence snapshots per assistant message (chat.conversation_message_evidence).</summary>
    public DbSet<ConversationMessageEvidence> ConversationMessageEvidences => Set<ConversationMessageEvidence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Default schema for entities that don't explicitly call
        // `.ToTable("x", "schema")` for one of the other six schemas above.
        modelBuilder.HasDefaultSchema("platform");

        // TASK-PG-02: registers the "vector" Postgres extension with the EF
        // model (in addition to CREATE EXTENSION IF NOT EXISTS in the
        // migration itself) - required by Pgvector.EntityFrameworkCore so
        // the "vector" column type resolves to a known Npgsql data type;
        // without this, writing a Vector value throws
        // "Cannot resolve 'vector' to a fully qualified datatype name."
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfiguration(new ApplicationConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessingJobConfiguration());
        modelBuilder.ApplyConfiguration(new ProcessingAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new AgentRunConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
        modelBuilder.ApplyConfiguration(new WorkerHeartbeatConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeFileConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeChunkConfiguration());
        modelBuilder.ApplyConfiguration(new AgentConfiguration());
        modelBuilder.ApplyConfiguration(new RunEvidenceConfiguration());
        modelBuilder.ApplyConfiguration(new CitationConfiguration());
        modelBuilder.ApplyConfiguration(new GeneratedOutputConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEventConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationMessageConfiguration());
        modelBuilder.ApplyConfiguration(new ConversationMessageEvidenceConfiguration());
    }
}
