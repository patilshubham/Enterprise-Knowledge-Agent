using EnterpriseKnowledgeAgent.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-AUD-01: maps <see cref="AuditEvent"/> to <c>audit.audit_events</c>, append-only.</summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events", "audit");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ActorSubject).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Outcome).IsRequired();
        builder.Property(e => e.ApplicationId);
        builder.Property(e => e.EntityType).HasMaxLength(100);
        builder.Property(e => e.EntityId).HasMaxLength(200);
        builder.Property(e => e.EntityDisplayName).HasMaxLength(500);
        builder.Property(e => e.FailureReason).HasMaxLength(200);
        builder.Property(e => e.CorrelationId);

        // TASK-AUD-01: the required "efficient scoped-by-app + time-ordered"
        // index; also add a plain occurred_at index for platform-wide search
        // (no ApplicationId filter) so that path isn't a full table scan.
        builder.HasIndex(e => new { e.ApplicationId, e.OccurredAt });
        builder.HasIndex(e => e.OccurredAt);
    }
}
