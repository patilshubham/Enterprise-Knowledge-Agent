using EnterpriseKnowledgeAgent.Domain.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-JOB-01: maps <see cref="AgentRun"/> to <c>execution.agent_runs</c> (ASDD §9.4).</summary>
public sealed class AgentRunConfiguration : IEntityTypeConfiguration<AgentRun>
{
    public void Configure(EntityTypeBuilder<AgentRun> builder)
    {
        builder.ToTable("agent_runs", "execution");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.ApplicationId).IsRequired();
        builder.Property(r => r.AgentId).IsRequired();
        builder.Property(r => r.OutboxMessageId);
        builder.Property(r => r.RequestedBy).HasMaxLength(255).IsRequired();
        builder.Property(r => r.UserRequest).IsRequired();
        builder.Property(r => r.IdempotencyKey).HasMaxLength(200).IsRequired();
        builder.Property(r => r.OutputTitle).HasMaxLength(300);

        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(r => r.Stage).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(r => r.Progress).IsRequired();

        builder.Property(r => r.AgentNameSnapshot).HasMaxLength(200).IsRequired();
        builder.Property(r => r.AgentPromptSnapshot).IsRequired();
        builder.Property(r => r.KnowledgeScopeSnapshotJson);
        builder.Property(r => r.DeploymentSnapshot).HasMaxLength(200);
        builder.Property(r => r.RequestedParametersJson);
        builder.Property(r => r.EffectiveParametersJson);
        builder.Property(r => r.RetrievalSettingsJson);

        builder.Property(r => r.WorkerId).HasMaxLength(200);
        builder.Property(r => r.LeaseExpiresAt);
        builder.Property(r => r.AttemptNumber).IsRequired();
        builder.Property(r => r.HeartbeatAt);
        builder.Property(r => r.StartedAt);
        builder.Property(r => r.CompletedAt);

        builder.Property(r => r.FailureCategory).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.FailureMessage).HasMaxLength(2000);

        builder.Property(r => r.CorrelationId)
            .HasConversion(id => id.ToString(), value => ParseCorrelationId(value))
            .HasMaxLength(36)
            .IsRequired();

        builder.Property(r => r.CancellationRequested).IsRequired();

        builder.Property(r => r.CreatedAt).IsRequired();

        // TASK-SIG-02: per-entity monotonic sequence for SignalR stale-event rejection (module 25).
        builder.Property(r => r.EventSequence).IsRequired();

        // ASDD §10: agent_runs(application_id, status, created_at).
        builder.HasIndex(r => new { r.ApplicationId, r.Status, r.CreatedAt });
        builder.HasIndex(r => new { r.Status, r.LeaseExpiresAt });

        // TASK-RUN-01: repeated submissions with the same idempotency key for
        // the same agent must resolve to the same run - enforced at the
        // database level, not just in application-layer lookup logic.
        builder.HasIndex(r => new { r.AgentId, r.IdempotencyKey }).IsUnique();
    }

    private static Shared.Correlation.CorrelationId ParseCorrelationId(string value) =>
        Shared.Correlation.CorrelationId.TryParse(value, out var id)
            ? id
            : throw new InvalidOperationException($"Invalid correlation id '{value}' stored for an agent run.");
}
