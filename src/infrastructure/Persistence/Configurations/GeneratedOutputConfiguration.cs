using EnterpriseKnowledgeAgent.Domain.Outputs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// TASK-OUT-01: maps <see cref="GeneratedOutput"/> to <c>outputs.generated_outputs</c>
/// (ASDD §9.5). <see cref="GeneratedOutput.AgentRunId"/> has no FK constraint,
/// matching this codebase's existing convention of plain indexed reference
/// columns (see <see cref="ProcessingAttemptConfiguration"/>) rather than
/// enforced EF relationships; a unique index still enforces one output per run.
/// </summary>
public sealed class GeneratedOutputConfiguration : IEntityTypeConfiguration<GeneratedOutput>
{
    public void Configure(EntityTypeBuilder<GeneratedOutput> builder)
    {
        builder.ToTable("generated_outputs", "outputs");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.ApplicationId).IsRequired();
        builder.Property(o => o.AgentRunId).IsRequired();
        builder.Property(o => o.Title).IsRequired().HasMaxLength(300);
        builder.Property(o => o.OriginalMarkdown).IsRequired();
        builder.Property(o => o.CurrentMarkdown).IsRequired();
        builder.Property(o => o.IsEdited).IsRequired();
        builder.Property(o => o.IsDeleted).IsRequired();
        builder.Property(o => o.CreatedBy).IsRequired().HasMaxLength(200);
        builder.Property(o => o.CreatedAt).IsRequired();
        builder.Property(o => o.LastEditedBy).HasMaxLength(200);

        builder.HasIndex(o => o.AgentRunId).IsUnique();
        builder.HasIndex(o => new { o.ApplicationId, o.CreatedAt });
    }
}
