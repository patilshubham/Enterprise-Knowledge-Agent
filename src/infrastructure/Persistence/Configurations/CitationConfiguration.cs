using EnterpriseKnowledgeAgent.Domain.Citations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// TASK-CIT-01: maps <see cref="Citation"/> to <c>outputs.citations</c>
/// (ASDD §17.1). Neither <see cref="Citation.GeneratedOutputId"/> nor
/// <see cref="Citation.RunEvidenceId"/> has an FK constraint, matching this
/// codebase's existing convention of plain indexed reference columns (see
/// <see cref="ProcessingAttemptConfiguration"/>) rather than enforced EF
/// relationships.
/// </summary>
public sealed class CitationConfiguration : IEntityTypeConfiguration<Citation>
{
    public void Configure(EntityTypeBuilder<Citation> builder)
    {
        builder.ToTable("citations", "outputs");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.ApplicationId).IsRequired();
        builder.Property(c => c.GeneratedOutputId).IsRequired();
        builder.Property(c => c.RunEvidenceId).IsRequired();
        builder.Property(c => c.CreatedAt).IsRequired();

        builder.HasIndex(c => c.GeneratedOutputId);
        builder.HasIndex(c => c.RunEvidenceId);
    }
}
