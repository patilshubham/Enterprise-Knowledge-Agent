using EnterpriseKnowledgeAgent.Domain.Knowledge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>TASK-KNW-01: maps <see cref="KnowledgeFile"/> to <c>knowledge.knowledge_files</c>.</summary>
public sealed class KnowledgeFileConfiguration : IEntityTypeConfiguration<KnowledgeFile>
{
    public void Configure(EntityTypeBuilder<KnowledgeFile> builder)
    {
        builder.ToTable("knowledge_files", "knowledge");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.ApplicationId).IsRequired();

        builder.Property(f => f.DisplayName).HasMaxLength(500).IsRequired();
        builder.Property(f => f.ObjectKey).HasMaxLength(1000).IsRequired();
        builder.Property(f => f.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(f => f.FileSize).IsRequired();
        builder.Property(f => f.MimeType).HasMaxLength(200).IsRequired();
        builder.Property(f => f.Encoding).HasMaxLength(50).IsRequired();

        builder.Property(f => f.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.IsEnabled).IsRequired();

        builder.Property(f => f.UploadedBy).HasMaxLength(255).IsRequired();
        builder.Property(f => f.UploadedAt).IsRequired();
        builder.Property(f => f.LastProcessedAt);

        builder.Property(f => f.ReplacementObjectKey).HasMaxLength(1000);
        builder.Property(f => f.ReplacementStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.ReplacementContentHash).HasMaxLength(64);
        builder.Property(f => f.ReplacementFileSize);
        builder.Property(f => f.ReplacementMimeType).HasMaxLength(200);
        builder.Property(f => f.ReplacementEncoding).HasMaxLength(50);
        builder.Property(f => f.DeletedAt);

        builder.Ignore(f => f.EffectiveStatus);

        // ASDD §10: (application_id, status, is_enabled) for listing/retrieval scoping.
        builder.HasIndex(f => new { f.ApplicationId, f.Status, f.IsEnabled });

        // ASDD §10: (application_id, content_hash) for duplicate detection.
        builder.HasIndex(f => new { f.ApplicationId, f.ContentHash });
    }
}
