using EnterpriseKnowledgeAgent.Domain.Citations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-CIT-01: maps <see cref="RunEvidence"/> to <c>execution.run_evidence</c> (ASDD §17.1).</summary>
public sealed class RunEvidenceConfiguration : IEntityTypeConfiguration<RunEvidence>
{
    public void Configure(EntityTypeBuilder<RunEvidence> builder)
    {
        builder.ToTable("run_evidence", "execution");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ApplicationId).IsRequired();
        builder.Property(e => e.AgentRunId).IsRequired();
        builder.Property(e => e.SourceId).HasMaxLength(20).IsRequired();
        builder.Property(e => e.KnowledgeChunkId).IsRequired();
        builder.Property(e => e.KnowledgeFileId).IsRequired();
        builder.Property(e => e.FileNameSnapshot).HasMaxLength(500).IsRequired();
        builder.Property(e => e.HeadingPathSnapshot).HasMaxLength(2000).IsRequired();
        builder.Property(e => e.PassageSnapshot).IsRequired();
        builder.Property(e => e.CharacterStartSnapshot).IsRequired();
        builder.Property(e => e.CharacterEndSnapshot).IsRequired();
        builder.Property(e => e.ProcessedAtSnapshot);
        builder.Property(e => e.CreatedAt).IsRequired();

        // TASK-CIT-02/03: one SOURCE ID per run; lookups during marker
        // validation are always scoped to (AgentRunId, SourceId).
        builder.HasIndex(e => new { e.AgentRunId, e.SourceId }).IsUnique();
        builder.HasIndex(e => new { e.ApplicationId, e.AgentRunId });
    }
}
