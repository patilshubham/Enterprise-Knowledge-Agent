using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ApplicationEntity = EnterpriseKnowledgeAgent.Domain.Applications.Application;

namespace EnterpriseKnowledgeAgent.Infrastructure.Persistence.Configurations;

/// <summary>
/// TASK-APP-01: maps <see cref="ApplicationEntity"/> to <c>platform.applications</c>.
/// </summary>
public sealed class ApplicationConfiguration : IEntityTypeConfiguration<ApplicationEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.ToTable("applications", "platform");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name)
            .HasMaxLength(ApplicationEntity.MaxNameLength)
            .IsRequired();

        builder.Property(a => a.Description)
            .HasMaxLength(ApplicationEntity.MaxDescriptionLength);

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(a => a.AllowViewerExecution)
            .IsRequired();

        builder.Property(a => a.CreatedBy)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt);
        builder.Property(a => a.ArchivedAt);
        builder.Property(a => a.DeletedAt);

        builder.HasIndex(a => a.Status);

        // Uniqueness policy (TASK-APP-01): non-deleted application names are
        // unique case-insensitively; a deleted application's name is free to
        // be reused by a new application. A Postgres computed column keeps
        // the case-folding authoritative in the database (not just at the
        // repository layer), closing the race a plain application-level
        // check alone could leave open.
        builder.Property<string>("NormalizedName")
            .HasComputedColumnSql("lower(\"Name\")", stored: true);

        builder.HasIndex("NormalizedName")
            .IsUnique()
            .HasFilter("\"Status\" <> 'Deleted'");
    }
}
